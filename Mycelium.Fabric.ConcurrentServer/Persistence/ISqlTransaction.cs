// ------------------------------------------------------------------------------------------------
//  <copyright file="ISqlTransaction.cs" company="Starion Group S.A.">
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
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    /// <summary>
    /// Defines an <see cref="IDataStoreTransaction"/> held by a SQL database, through which the data access objects run their SQL.
    /// </summary>
    public interface ISqlTransaction : IDataStoreTransaction
    {
        /// <summary>
        /// Executes a statement that returns no rows.
        /// </summary>
        /// <param name="commandText">The SQL of the statement.</param>
        /// <param name="parameters">The parameters the statement is bound to.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The number of rows the statement affected, or the errors the store raised.</returns>
        Task<ErrorOr<int>> ExecuteNonQueryAsync(string commandText, IReadOnlyCollection<DbParameter> parameters, CancellationToken cancellationToken);

        /// <summary>
        /// Executes a query and maps each of its rows.
        /// </summary>
        /// <typeparam name="TRow">The type each row is mapped to.</typeparam>
        /// <param name="commandText">The SQL of the query.</param>
        /// <param name="parameters">The parameters the query is bound to.</param>
        /// <param name="readRow">The function that maps the current row of the reader.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The mapped rows, in the order the query returned them, or the errors the store raised.</returns>
        Task<ErrorOr<IReadOnlyList<TRow>>> ExecuteReaderAsync<TRow>(string commandText, IReadOnlyCollection<DbParameter> parameters, Func<DbDataReader, TRow> readRow,
            CancellationToken cancellationToken);
    }
}
