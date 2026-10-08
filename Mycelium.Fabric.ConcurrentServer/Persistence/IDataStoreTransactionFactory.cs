// ------------------------------------------------------------------------------------------------
//  <copyright file="IDataStoreTransactionFactory.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    /// <summary>
    /// Opens the unit of work that the reads and writes of one request take part in.
    /// </summary>
    public interface IDataStoreTransactionFactory
    {
        /// <summary>
        /// Opens a transaction against the data store.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The opened <see cref="IDataStoreTransaction"/>, or the errors that prevented it.
        /// </returns>
        Task<ErrorOr<IDataStoreTransaction>> BeginAsync(CancellationToken cancellationToken);
    }
}
