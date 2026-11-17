// ------------------------------------------------------------------------------------------------
//  <copyright file="ITransactionScope.cs" company="Starion Group S.A.">
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
    /// Gives one operation access to the transaction of the current request, whether it opened that transaction or joined it.
    /// </summary>
    /// <remarks>
    /// Only the scope that opened the transaction commits or rolls it back; a joined scope that rolls back dooms the
    /// transaction, so that its owner cannot commit it. Disposing an owning scope that was not committed rolls it back.
    /// </remarks>
    public interface ITransactionScope : IAsyncDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this scope opened the transaction, and so decides its outcome.
        /// </summary>
        bool IsOwner { get; }

        /// <summary>
        /// Gets the <see cref="TransactionMode"/> of the transaction.
        /// </summary>
        TransactionMode Mode { get; }

        /// <summary>
        /// Gets the <see cref="IDataStoreTransaction"/> the repositories take part in.
        /// </summary>
        IDataStoreTransaction Transaction { get; }

        /// <summary>
        /// Commits the transaction when this scope owns it; a joined scope leaves the decision to the owner.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<Success>> CommitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Rolls the transaction back when this scope owns it, or dooms it when this scope joined it.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<Success>> RollbackAsync(CancellationToken cancellationToken);
    }
}
