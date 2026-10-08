// ------------------------------------------------------------------------------------------------
//  <copyright file="ICommitGraphRepository.cs" company="Starion Group S.A.">
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
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Defines the persistence contract of the computations that walk the commit graph of a project.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.1.2 (pp. 32–36) defines a commit by the commits that
    /// precede it, so the state at a commit and the ancestry of a commit are both recursive over that
    /// graph. Neither is expressible as a read of one record, which is why they are declared here rather
    /// than on the generated per-record contracts.
    /// </remarks>
    public interface ICommitGraphRepository : IBaseRepository
    {
        /// <summary>
        /// Reads every <see cref="DataVersion"/> that is in effect at a commit.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the state is read at.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        /// <remarks>
        /// This is <c>Commit.versionedData</c> — the accumulated state of the project at the commit — as
        /// opposed to <c>Commit.change</c>, which is the delta the commit itself carries and which a
        /// filter on the <c>commit</c> of a <see cref="DataVersion"/> reads.
        /// </remarks>
        Task<ErrorOr<IReadOnlyList<DataVersion>>> ReadVersionedDataAsync(IDataStoreTransaction transaction, Guid commitId, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the commit and every commit that precedes it, most recent first.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the ancestry is read for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<IReadOnlyList<Commit>>> ReadAncestryAsync(IDataStoreTransaction transaction, Guid commitId, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the most recent commit that precedes all of the given commits.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitIds">The identifiers of the commits the merge base is read for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        /// <remarks>
        /// A merge reads the state at this commit to tell a change made on one side from a change made on
        /// both. Commits with no shared ancestor answer <c>NotFound</c>.
        /// </remarks>
        Task<ErrorOr<Commit>> ReadMergeBaseAsync(IDataStoreTransaction transaction, IReadOnlyList<Guid> commitIds, CancellationToken cancellationToken);
    }
}
