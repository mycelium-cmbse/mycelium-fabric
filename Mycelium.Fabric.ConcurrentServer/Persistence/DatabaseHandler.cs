// ------------------------------------------------------------------------------------------------
//  <copyright file="DatabaseHandler.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    /// <summary>
    /// The <see cref="IDatabaseHandler"/> of one request, to be registered with a scoped lifetime.
    /// </summary>
    public sealed class DatabaseHandler : IDatabaseHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseHandler"/> class.
        /// </summary>
        /// <param name="transactionFactory">The <see cref="IDataStoreTransactionFactory"/> that opens the transactions.</param>
        /// <exception cref="ArgumentNullException"><paramref name="transactionFactory"/> is <c>null</c>.</exception>
        public DatabaseHandler(IDataStoreTransactionFactory transactionFactory)
        {
            this.TransactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
        }

        /// <summary>
        /// Gets a value indicating whether a transaction is open for the current request.
        /// </summary>
        public bool HasActiveTransaction => this.ActiveScope != null;

        /// <summary>
        /// Gets or sets the scope that opened the transaction of the current request, or <c>null</c> when none is open.
        /// </summary>
        private OwningTransactionScope ActiveScope { get; set; }

        /// <summary>
        /// Gets the <see cref="IDataStoreTransactionFactory"/> that opens the transactions.
        /// </summary>
        private IDataStoreTransactionFactory TransactionFactory { get; }

        /// <summary>
        /// Opens the transaction of the current request, or joins it when it is already open.
        /// </summary>
        /// <param name="mode">The <see cref="TransactionMode"/> the operation requires.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The <see cref="ITransactionScope"/> of the operation, or the errors that prevented opening the transaction.</returns>
        /// <exception cref="InvalidOperationException">A <see cref="TransactionMode.ReadWrite"/> scope is requested inside a read-only transaction.</exception>
        public async Task<ErrorOr<ITransactionScope>> BeginTransactionAsync(TransactionMode mode, CancellationToken cancellationToken)
        {
            if (this.ActiveScope != null)
            {
                return this.ActiveScope.Mode == TransactionMode.ReadOnly && mode == TransactionMode.ReadWrite
                    ? throw new InvalidOperationException("A write cannot take part in the read-only transaction of the current request.")
                    : new JoinedTransactionScope(this.ActiveScope);
            }

            var transaction = await this.TransactionFactory.BeginAsync(mode, cancellationToken);

            if (transaction.IsError)
            {
                return transaction.Errors;
            }

            this.ActiveScope = new OwningTransactionScope(this, transaction.Value, mode);

            return this.ActiveScope;
        }

        /// <summary>
        /// Forgets the transaction of the current request once its owning scope has been disposed.
        /// </summary>
        /// <param name="scope">The <see cref="OwningTransactionScope"/> that has been disposed.</param>
        private void Release(OwningTransactionScope scope)
        {
            if (ReferenceEquals(this.ActiveScope, scope))
            {
                this.ActiveScope = null;
            }
        }

        /// <summary>
        /// The <see cref="ITransactionScope"/> of the operation that opened the transaction.
        /// </summary>
        private sealed class OwningTransactionScope : ITransactionScope
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="OwningTransactionScope"/> class.
            /// </summary>
            /// <param name="handler">The <see cref="DatabaseHandler"/> that tracks the transaction.</param>
            /// <param name="transaction">The opened <see cref="IDataStoreTransaction"/>.</param>
            /// <param name="mode">The <see cref="TransactionMode"/> the transaction was opened with.</param>
            public OwningTransactionScope(DatabaseHandler handler, IDataStoreTransaction transaction, TransactionMode mode)
            {
                this.Handler = handler;
                this.Transaction = transaction;
                this.Mode = mode;
            }

            /// <summary>
            /// Gets a value indicating whether this scope opened the transaction, which it always did.
            /// </summary>
            public bool IsOwner => true;

            /// <summary>
            /// Gets the <see cref="TransactionMode"/> of the transaction.
            /// </summary>
            public TransactionMode Mode { get; }

            /// <summary>
            /// Gets the <see cref="IDataStoreTransaction"/> the repositories take part in.
            /// </summary>
            public IDataStoreTransaction Transaction { get; }

            /// <summary>
            /// Gets or sets a value indicating whether a joined scope rolled back, so the transaction can no longer be committed.
            /// </summary>
            public bool IsDoomed { get; set; }

            /// <summary>
            /// Gets the <see cref="DatabaseHandler"/> that tracks the transaction.
            /// </summary>
            private DatabaseHandler Handler { get; }

            /// <summary>
            /// Gets or sets a value indicating whether the transaction has been committed or rolled back.
            /// </summary>
            private bool IsCompleted { get; set; }

            /// <summary>
            /// Commits the transaction, unless a joined scope doomed it, in which case it is rolled back.
            /// </summary>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
            /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
            public async Task<ErrorOr<Success>> CommitAsync(CancellationToken cancellationToken)
            {
                if (this.IsDoomed)
                {
                    var rollback = await this.RollbackAsync(cancellationToken);

                    return rollback.IsError
                        ? rollback.Errors
                        : Error.Unexpected("transaction.doomed", "The transaction was rolled back because an operation that took part in it failed.");
                }

                var commit = await this.Transaction.CommitAsync(cancellationToken);
                this.IsCompleted = !commit.IsError;

                return commit;
            }

            /// <summary>
            /// Rolls the transaction back.
            /// </summary>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
            /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
            public async Task<ErrorOr<Success>> RollbackAsync(CancellationToken cancellationToken)
            {
                if (this.IsCompleted)
                {
                    return Result.Success;
                }

                this.IsCompleted = true;

                return await this.Transaction.RollbackAsync(cancellationToken);
            }

            /// <summary>
            /// Disposes the transaction, which rolls back a transaction that was not committed, and releases it from the handler.
            /// </summary>
            /// <returns>A <see cref="ValueTask"/> that completes once the transaction is disposed.</returns>
            public async ValueTask DisposeAsync()
            {
                try
                {
                    await this.Transaction.DisposeAsync();
                }
                finally
                {
                    this.Handler.Release(this);
                }
            }
        }

        /// <summary>
        /// The <see cref="ITransactionScope"/> of an operation that runs inside a transaction another operation opened.
        /// </summary>
        private sealed class JoinedTransactionScope : ITransactionScope
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="JoinedTransactionScope"/> class.
            /// </summary>
            /// <param name="owner">The <see cref="OwningTransactionScope"/> of the transaction that is joined.</param>
            public JoinedTransactionScope(OwningTransactionScope owner)
            {
                this.Owner = owner;
            }

            /// <summary>
            /// Gets a value indicating whether this scope opened the transaction, which it never did.
            /// </summary>
            public bool IsOwner => false;

            /// <summary>
            /// Gets the <see cref="TransactionMode"/> of the joined transaction.
            /// </summary>
            public TransactionMode Mode => this.Owner.Mode;

            /// <summary>
            /// Gets the <see cref="IDataStoreTransaction"/> the repositories take part in.
            /// </summary>
            public IDataStoreTransaction Transaction => this.Owner.Transaction;

            /// <summary>
            /// Gets the <see cref="OwningTransactionScope"/> of the transaction that is joined.
            /// </summary>
            private OwningTransactionScope Owner { get; }

            /// <summary>
            /// Leaves the commit to the owner of the transaction.
            /// </summary>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
            /// <returns>The successful outcome of the operation.</returns>
            public Task<ErrorOr<Success>> CommitAsync(CancellationToken cancellationToken) => Task.FromResult<ErrorOr<Success>>(Result.Success);

            /// <summary>
            /// Dooms the transaction, so that its owner rolls it back.
            /// </summary>
            /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
            /// <returns>The successful outcome of the operation.</returns>
            public Task<ErrorOr<Success>> RollbackAsync(CancellationToken cancellationToken)
            {
                this.Owner.IsDoomed = true;

                return Task.FromResult<ErrorOr<Success>>(Result.Success);
            }

            /// <summary>
            /// Leaves the disposal of the transaction to its owner.
            /// </summary>
            /// <returns>A completed <see cref="ValueTask"/>.</returns>
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
