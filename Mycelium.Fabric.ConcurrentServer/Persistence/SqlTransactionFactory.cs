// ------------------------------------------------------------------------------------------------
//  <copyright file="SqlTransactionFactory.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    /// <summary>
    /// The <see cref="IDataStoreTransactionFactory"/> that opens <see cref="SqlTransaction"/>s on the SysML v2 database.
    /// </summary>
    public sealed class SqlTransactionFactory : IDataStoreTransactionFactory
    {
        /// <summary>
        /// The statement that forbids every write in the transaction it runs in.
        /// </summary>
        private const string ReadOnlySql = "SET TRANSACTION READ ONLY";

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTransactionFactory"/> class.
        /// </summary>
        /// <param name="dataSource">The <see cref="DbDataSource"/> of the database that holds the SysML v2 schema.</param>
        /// <exception cref="ArgumentNullException"><paramref name="dataSource"/> is <c>null</c>.</exception>
        public SqlTransactionFactory(DbDataSource dataSource)
        {
            this.DataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        /// <summary>
        /// Gets the <see cref="DbDataSource"/> the connections are opened from.
        /// </summary>
        private DbDataSource DataSource { get; }

        /// <summary>
        /// Opens a connection and begins a transaction on it.
        /// </summary>
        /// <param name="mode">The <see cref="TransactionMode"/> that states whether the transaction may write.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The opened <see cref="SqlTransaction"/>, or the errors that prevented opening it.</returns>
        public async Task<ErrorOr<IDataStoreTransaction>> BeginAsync(TransactionMode mode, CancellationToken cancellationToken)
        {
            DbConnection connection = null;

            try
            {
                connection = await this.DataSource.OpenConnectionAsync(cancellationToken);

                var transaction = await connection.BeginTransactionAsync(cancellationToken);

                if (mode == TransactionMode.ReadOnly)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = ReadOnlySql;
                    command.Transaction = transaction;

                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                return new SqlTransaction(connection, transaction);
            }
            catch (DbException exception)
            {
                await DisposeAsync(connection);

                return exception.ToError();
            }
            catch
            {
                await DisposeAsync(connection);

                throw;
            }
        }

        /// <summary>
        /// Returns a connection that no transaction took ownership of to the pool.
        /// </summary>
        /// <param name="connection">The opened <see cref="DbConnection"/>, or <c>null</c> when opening it failed.</param>
        /// <returns>A <see cref="ValueTask"/> that completes once the connection is disposed.</returns>
        private static ValueTask DisposeAsync(DbConnection connection) => connection?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
