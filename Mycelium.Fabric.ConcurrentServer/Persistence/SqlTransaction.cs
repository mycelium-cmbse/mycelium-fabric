// ------------------------------------------------------------------------------------------------
//  <copyright file="SqlTransaction.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    /// <summary>
    /// The <see cref="ISqlTransaction"/> that owns one connection and its open transaction.
    /// </summary>
    public sealed class SqlTransaction : ISqlTransaction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTransaction"/> class.
        /// </summary>
        /// <param name="connection">The open <see cref="DbConnection"/>, disposed with the transaction.</param>
        /// <param name="transaction">The <see cref="DbTransaction"/> begun on <paramref name="connection"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="transaction"/> is <c>null</c>.</exception>
        public SqlTransaction(DbConnection connection, DbTransaction transaction)
        {
            this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        /// <summary>
        /// Gets the open <see cref="DbConnection"/> the transaction runs on.
        /// </summary>
        private DbConnection Connection { get; }

        /// <summary>
        /// Gets or sets a value indicating whether a statement failed, after which the transaction cannot be committed.
        /// </summary>
        private bool IsFailed { get; set; }

        /// <summary>
        /// Gets the <see cref="DbTransaction"/> every command takes part in.
        /// </summary>
        private DbTransaction Transaction { get; }

        /// <summary>
        /// Applies every write that took part in this transaction.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors the store raised.</returns>
        /// <remarks>
        /// A transaction in which a statement failed is rolled back instead, since some databases answer its commit with a rollback, not an error.
        /// </remarks>
        public async Task<ErrorOr<Success>> CommitAsync(CancellationToken cancellationToken)
        {
            if (this.IsFailed)
            {
                var rolledBack = await this.RollbackAsync(cancellationToken);

                return rolledBack.IsError
                    ? rolledBack.Errors
                    : Error.Unexpected("store.aborted", "The transaction was rolled back because one of its statements failed.");
            }

            try
            {
                await this.Transaction.CommitAsync(cancellationToken);

                return Result.Success;
            }
            catch (DbException exception)
            {
                return exception.ToError();
            }
        }

        /// <summary>
        /// Disposes the transaction, which rolls it back when it was not committed, and then its connection.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that completes once both are disposed.</returns>
        public async ValueTask DisposeAsync()
        {
            await this.Transaction.DisposeAsync();
            await this.Connection.DisposeAsync();
        }

        /// <summary>
        /// Executes a statement that returns no rows.
        /// </summary>
        /// <param name="commandText">The SQL of the statement.</param>
        /// <param name="parameters">The parameters the statement is bound to.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The number of rows the statement affected, or the errors the store raised.</returns>
        /// <exception cref="ArgumentException"><paramref name="commandText"/> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
        public async Task<ErrorOr<int>> ExecuteNonQueryAsync(string commandText, IReadOnlyCollection<DbParameter> parameters, CancellationToken cancellationToken)
        {
            await using var command = this.CreateCommand(commandText, parameters);

            try
            {
                return await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException exception)
            {
                return this.Fail(exception);
            }
        }

        /// <summary>
        /// Executes a query and maps each of its rows.
        /// </summary>
        /// <typeparam name="TRow">The type each row is mapped to.</typeparam>
        /// <param name="commandText">The SQL of the query.</param>
        /// <param name="parameters">The parameters the query is bound to.</param>
        /// <param name="readRow">The function that maps the current row of the reader.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The mapped rows, in the order the query returned them, or the errors the store raised.</returns>
        /// <exception cref="ArgumentException"><paramref name="commandText"/> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="parameters"/> or <paramref name="readRow"/> is <c>null</c>.</exception>
        public async Task<ErrorOr<IReadOnlyList<TRow>>> ExecuteReaderAsync<TRow>(string commandText, IReadOnlyCollection<DbParameter> parameters,
            Func<DbDataReader, TRow> readRow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(readRow);

            await using var command = this.CreateCommand(commandText, parameters);

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                List<TRow> rows = [];

                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(readRow(reader));
                }

                return ErrorOrFactory.From<IReadOnlyList<TRow>>(rows);
            }
            catch (DbException exception)
            {
                return this.Fail(exception);
            }
        }

        /// <summary>
        /// Discards every write that took part in this transaction.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors the store raised.</returns>
        public async Task<ErrorOr<Success>> RollbackAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.Transaction.RollbackAsync(cancellationToken);

                return Result.Success;
            }
            catch (DbException exception)
            {
                return exception.ToError();
            }
        }

        /// <summary>
        /// Creates a command that takes part in this transaction.
        /// </summary>
        /// <param name="commandText">The SQL of the command.</param>
        /// <param name="parameters">The parameters the command is bound to.</param>
        /// <returns>The <see cref="DbCommand"/>, to be disposed by the caller.</returns>
        /// <exception cref="ArgumentException"><paramref name="commandText"/> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <c>null</c>.</exception>
        private DbCommand CreateCommand(string commandText, IReadOnlyCollection<DbParameter> parameters)
        {
            ArgumentException.ThrowIfNullOrEmpty(commandText);
            ArgumentNullException.ThrowIfNull(parameters);

            var command = this.Connection.CreateCommand();
            command.CommandText = commandText;
            command.Transaction = this.Transaction;
            command.Parameters.AddRange(parameters.ToArray());

            return command;
        }

        /// <summary>
        /// Records that a statement failed and translates the failure.
        /// </summary>
        /// <param name="exception">The <see cref="DbException"/> the statement raised.</param>
        /// <returns>The <see cref="Error"/> the failure translates into.</returns>
        private Error Fail(DbException exception)
        {
            this.IsFailed = true;

            return exception.ToError();
        }
    }
}
