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

    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

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

        /// <summary>
        /// Reads one page of the <see cref="DataVersion"/> records a commit changes, narrowed to the given kinds of change.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit whose change is read.</param>
        /// <param name="changeTypes">The kinds of change to keep, every kind when empty.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        /// <remarks>
        /// §7.2.3 <c>getCommitChange</c> keeps only the changes of the given types without defining them; this server reads a version
        /// without payload as a deletion, and otherwise as an update when its identity is in effect at a previous commit, a creation when not.
        /// </remarks>
        Task<ErrorOr<PagedResult<DataVersion>>> ReadChangeAsync(IDataStoreTransaction transaction, Guid commitId, IReadOnlyList<ChangeType> changeTypes,
            QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Reads one page of the differences between the data in effect at two commits, ordered by data identity.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="baseCommitId">The identifier of the commit compared against.</param>
        /// <param name="compareCommitId">The identifier of the commit that is compared.</param>
        /// <param name="changeTypes">The kinds of difference to keep, every kind when empty.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        /// <remarks>
        /// This is <c>compareCommit.versionedData - baseCommit.versionedData</c> of §7.2.3 <c>diffCommits</c>: an identity absent from the
        /// base is created, one absent from the compared commit is deleted, and one whose versions differ is updated.
        /// </remarks>
        Task<ErrorOr<PagedResult<DataDifference>>> ReadDifferencesAsync(IDataStoreTransaction transaction, Guid baseCommitId, Guid compareCommitId,
            IReadOnlyList<ChangeType> changeTypes, QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Moves the head of a branch to a new commit, provided the head is still the one the commit was built on.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="branchId">The identifier of the branch.</param>
        /// <param name="expectedHeadId">The identifier of the head the new commit was built on.</param>
        /// <param name="newHeadId">The identifier of the new head.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The branch with its new head, or <see cref="ErrorType.Conflict"/> when another commit moved the head first.</returns>
        /// <remarks>
        /// A compare-and-swap, so that of two commits built concurrently on the same head only one becomes the head and the other is
        /// rolled back rather than left orphaned.
        /// </remarks>
        Task<ErrorOr<Branch>> MoveHeadAsync(IDataStoreTransaction transaction, Guid branchId, Guid expectedHeadId, Guid newHeadId, CancellationToken cancellationToken);
    }
}
