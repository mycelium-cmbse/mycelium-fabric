// ------------------------------------------------------------------------------------------------
//  <copyright file="IDataStoreTransaction.cs" company="Starion Group S.A.">
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
    /// Defines the unit of work that the reads and writes of one request take part in.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.2.3 (pp. 41–46) specifies <c>createCommit</c> and
    /// <c>mergeIntoBranch</c> as single operations over several records, so the service layer needs one
    /// scope that either applies all of them or none. A transaction that is disposed without being
    /// committed is rolled back.
    /// </remarks>
    public interface IDataStoreTransaction : IAsyncDisposable
    {
        /// <summary>
        /// Applies every write that took part in this transaction.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        Task<ErrorOr<Success>> CommitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Discards every write that took part in this transaction.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        Task<ErrorOr<Success>> RollbackAsync(CancellationToken cancellationToken);
    }
}
