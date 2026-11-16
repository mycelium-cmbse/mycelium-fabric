// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectDataVersioningService.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Common;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    /// <summary>
    /// The ProjectDataVersioningService of Systems Modeling API and Services 1.0 §7.2.3, confined to the project of the request.
    /// </summary>
    public class ProjectDataVersioningService : ServiceBase, IProjectDataVersioningService
    {
        /// <summary>
        /// The code of the error that a change updates data the deletion of an element in the same change deletes.
        /// </summary>
        private const string DependentUpdatedCode = "change.dependent.updated";

        /// <summary>
        /// The key of the metadata of a <see cref="DependentUpdatedCode"/> error that lists the data identities involved.
        /// </summary>
        private const string IdentitiesMetadataKey = "identities";

        /// <summary>
        /// The smallest interval the data store distinguishes between two commit timestamps.
        /// </summary>
        private static readonly TimeSpan TimestampResolution = TimeSpan.FromTicks(10);

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectDataVersioningService"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="branchRepository">The <see cref="IBranchRepository"/> of the branches.</param>
        /// <param name="tagRepository">The <see cref="ITagRepository"/> of the tags.</param>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> of the commits.</param>
        /// <param name="dataVersionRepository">The <see cref="IDataVersionRepository"/> of the data versions.</param>
        /// <param name="dataIdentityRepository">The <see cref="IDataIdentityRepository"/> of the data identities.</param>
        /// <param name="commitGraphRepository">The <see cref="ICommitGraphRepository"/> that walks the commit graph.</param>
        /// <param name="dataRepository">The <see cref="IDataRepository"/> of the versioned payload.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public ProjectDataVersioningService(IDatabaseHandler databaseHandler, IProjectScope projectScope, IProjectRepository projectRepository, IBranchRepository branchRepository,
            ITagRepository tagRepository, ICommitRepository commitRepository, IDataVersionRepository dataVersionRepository, IDataIdentityRepository dataIdentityRepository,
            ICommitGraphRepository commitGraphRepository, IDataRepository dataRepository, ILogger<ProjectDataVersioningService> logger)
            : base(databaseHandler, projectScope, projectRepository, logger)
        {
            this.BranchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
            this.TagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
            this.CommitRepository = commitRepository ?? throw new ArgumentNullException(nameof(commitRepository));
            this.DataVersionRepository = dataVersionRepository ?? throw new ArgumentNullException(nameof(dataVersionRepository));
            this.DataIdentityRepository = dataIdentityRepository ?? throw new ArgumentNullException(nameof(dataIdentityRepository));
            this.CommitGraphRepository = commitGraphRepository ?? throw new ArgumentNullException(nameof(commitGraphRepository));
            this.DataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
        }

        /// <summary>
        /// Gets the <see cref="IBranchRepository"/> of the branches.
        /// </summary>
        private IBranchRepository BranchRepository { get; }

        /// <summary>
        /// Gets the <see cref="ICommitGraphRepository"/> that walks the commit graph.
        /// </summary>
        private ICommitGraphRepository CommitGraphRepository { get; }

        /// <summary>
        /// Gets the <see cref="ICommitRepository"/> of the commits.
        /// </summary>
        private ICommitRepository CommitRepository { get; }

        /// <summary>
        /// Gets the <see cref="IDataIdentityRepository"/> of the data identities.
        /// </summary>
        private IDataIdentityRepository DataIdentityRepository { get; }

        /// <summary>
        /// Gets the <see cref="IDataRepository"/> of the versioned payload.
        /// </summary>
        private IDataRepository DataRepository { get; }

        /// <summary>
        /// Gets the <see cref="IDataVersionRepository"/> of the data versions.
        /// </summary>
        private IDataVersionRepository DataVersionRepository { get; }

        /// <summary>
        /// Gets the <see cref="ITagRepository"/> of the tags.
        /// </summary>
        private ITagRepository TagRepository { get; }

        /// <summary>
        /// Creates a branch of the project of the request whose head is the given commit.
        /// </summary>
        /// <param name="branch">The branch to create, of which only the name, aliases, description, owning project and head are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The created branch, or the errors that prevented creating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="branch"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The branch names a project other than the one of the request.</exception>
        public async Task<ErrorOr<Branch>> CreateBranchAsync(Branch branch, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(branch);

            if (string.IsNullOrWhiteSpace(branch.Name))
            {
                return Error.Validation("branch.name.missing", "A branch is created with a name.");
            }

            if (branch.Head == Guid.Empty)
            {
                return Error.Validation("branch.head.missing", "A branch is created with a head commit.");
            }

            var projectId = branch.OwningProject;

            return await this.WriteInProjectAsync(projectId, async transaction =>
            {
                var head = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, branch.Head, cancellationToken);

                if (head.IsError)
                {
                    return head.Errors;
                }

                var nameIsFree = await VerifyNameIsFreeAsync(this.BranchRepository, transaction, OwningProjectProperty, projectId, branch.Name, Guid.Empty, cancellationToken);

                if (nameIsFree.IsError)
                {
                    return nameIsFree.Errors;
                }

                var created = new Branch
                {
                    Id = Guid.NewGuid(),
                    Name = branch.Name,
                    Alias = AliasIncludingName(branch.Alias, branch.Name),
                    Description = branch.Description,
                    OwningProject = projectId,
                    Head = head.Value.Id,
                    Created = DateTime.UtcNow
                };

                return await this.BranchRepository.CreateAsync(transaction, created, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a commit of the given change on a branch of the project of the request, and moves the head of that branch to it.
        /// </summary>
        /// <param name="change">The <see cref="DataVersion"/> records that create, update or delete data.</param>
        /// <param name="branchId">The identifier of the branch, or <c>null</c> for the default branch of the project.</param>
        /// <param name="commit">The commit to create, of which only the name, aliases, description, owning project and previous commits are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The created commit, or the errors that prevented creating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="change"/> or <paramref name="commit"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The commit names a project other than the one of the request.</exception>
        /// <remarks>
        /// Annex A (PIM-PCB-010): the previous commits are the head of the branch together with the given ones, and the head moves to the
        /// created commit. The recorded change can exceed the requested one: §7.2.3 adds the deletion of the data a deleted element takes with
        /// it, and §7.1.2 the versions that settle what the previous commits disagree on.
        /// </remarks>
        public async Task<ErrorOr<Commit>> CreateCommitAsync(IReadOnlyList<DataVersion> change, Guid? branchId, Commit commit, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(change);
            ArgumentNullException.ThrowIfNull(commit);

            if (change.Count == 0)
            {
                return Error.Validation("change.missing", "A commit is created with at least one change.");
            }

            var projectId = commit.OwningProject;

            return await this.WriteInProjectAsync(projectId, async transaction =>
            {
                var branch = await this.ReadBranchOrDefaultAsync(transaction, projectId, branchId, cancellationToken);

                if (branch.IsError)
                {
                    return branch.Errors;
                }

                List<Guid> otherParents = [.. (commit.PreviousCommits ?? []).Where(parentId => parentId != Guid.Empty && parentId != branch.Value.Head).Distinct()];

                var fullChange = await this.IncludeParentChangesAsync(transaction, projectId, branch.Value.Head, otherParents, change, cancellationToken);

                return fullChange.IsError
                    ? fullChange.Errors
                    : await this.CommitChangeAsync(transaction, projectId, branch.Value, fullChange.Value, otherParents, commit, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a tag of the project of the request on the given commit.
        /// </summary>
        /// <param name="tag">The tag to create, of which only the name, aliases, description, owning project and tagged commit are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The created tag, or the errors that prevented creating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tag"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The tag names a project other than the one of the request.</exception>
        public async Task<ErrorOr<Tag>> CreateTagAsync(Tag tag, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tag);

            if (string.IsNullOrWhiteSpace(tag.Name))
            {
                return Error.Validation("tag.name.missing", "A tag is created with a name.");
            }

            if (tag.TaggedCommit == Guid.Empty)
            {
                return Error.Validation("tag.taggedCommit.missing", "A tag is created on a commit.");
            }

            var projectId = tag.OwningProject;

            return await this.WriteInProjectAsync(projectId, async transaction =>
            {
                var taggedCommit = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, tag.TaggedCommit, cancellationToken);

                if (taggedCommit.IsError)
                {
                    return taggedCommit.Errors;
                }

                var nameIsFree = await VerifyNameIsFreeAsync(this.TagRepository, transaction, OwningProjectProperty, projectId, tag.Name, Guid.Empty, cancellationToken);

                if (nameIsFree.IsError)
                {
                    return nameIsFree.Errors;
                }

                var created = new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = tag.Name,
                    Alias = AliasIncludingName(tag.Alias, tag.Name),
                    Description = tag.Description,
                    OwningProject = projectId,
                    TaggedCommit = taggedCommit.Value.Id,
                    Created = DateTime.UtcNow
                };

                return await this.TagRepository.CreateAsync(transaction, created, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes a branch of the project of the request, leaving its commits in place.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The deleted branch, or the errors that prevented deleting it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        /// <remarks>
        /// The default branch is refused, a deliberate choice where Annex A is silent: §7.1.2 gives a project exactly one default branch.
        /// </remarks>
        public Task<ErrorOr<Branch>> DeleteBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
        {
            return this.WriteInProjectAsync(projectId, async transaction =>
            {
                var branch = await this.ReadBranchAsync(transaction, projectId, branchId, cancellationToken);

                if (branch.IsError)
                {
                    return branch.Errors;
                }

                var project = await this.ProjectRepository.ReadByIdAsync(transaction, projectId, cancellationToken);

                if (project.IsError)
                {
                    return project.Errors;
                }

                if (project.Value.DefaultBranch == branchId)
                {
                    return Error.Conflict("branch.default", $"Branch {branchId} is the default branch of project {projectId}; set another default branch before deleting it.");
                }

                var deleted = await this.BranchRepository.DeleteAsync(transaction, branchId, cancellationToken);

                return deleted.IsError ? deleted.Errors : branch;
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes a tag of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="tagId">The identifier of the tag.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The deleted tag, or the errors that prevented deleting it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Tag>> DeleteTagAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken)
        {
            return this.WriteInProjectAsync(projectId, async transaction =>
            {
                var tag = await this.ReadTagAsync(transaction, projectId, tagId, cancellationToken);

                if (tag.IsError)
                {
                    return tag.Errors;
                }

                var deleted = await this.TagRepository.DeleteAsync(transaction, tagId, cancellationToken);

                return deleted.IsError ? deleted.Errors : tag;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets one page of the differences between the state of the project at two of its commits.
        /// </summary>
        /// <param name="baseCommitId">The identifier of the commit compared against.</param>
        /// <param name="compareCommitId">The identifier of the commit that is compared.</param>
        /// <param name="changeTypes">The kinds of difference to keep, all of them when empty.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of differences, ordered by data identity, or the errors that prevented computing them.</returns>
        /// <exception cref="InvalidOperationException">No project is in scope.</exception>
        public Task<ErrorOr<PagedResult<DataDifference>>> DiffCommitsAsync(Guid baseCommitId, Guid compareCommitId, IReadOnlyList<ChangeType> changeTypes,
            QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            var projectId = this.ProjectScope.ProjectId;
            List<ChangeType> selectedTypes = [.. (changeTypes ?? []).Distinct()];

            return this.ReadInProjectAsync(projectId, async transaction =>
            {
                var baseCommit = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, baseCommitId, cancellationToken);

                if (baseCommit.IsError)
                {
                    return baseCommit.Errors;
                }

                var compareCommit = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, compareCommitId, cancellationToken);

                return compareCommit.IsError
                    ? compareCommit.Errors
                    : await this.CommitGraphRepository.ReadDifferencesAsync(transaction, baseCommitId, compareCommitId, selectedTypes, queryParameters, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets a branch of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The branch, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Branch>> GetBranchByIdAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => this.ReadBranchAsync(transaction, projectId, branchId, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets one page of the branches of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of branches, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<Branch>>> GetBranchesAsync(Guid projectId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => ReadOwnedAsync(this.BranchRepository, transaction, projectId, queryParameters, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Gets a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The commit, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Commit>> GetCommitByIdAsync(Guid projectId, Guid commitId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => this.ReadCommitAsync(transaction, projectId, commitId, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets one page of the change a commit of the project of the request carries.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="changeTypes">The kinds of change to keep, all of them when empty.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of <see cref="DataVersion"/> records, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<DataVersion>>> GetCommitChangeAsync(Guid projectId, Guid commitId, IReadOnlyList<ChangeType> changeTypes, QueryParameters queryParameters,
            CancellationToken cancellationToken)
        {
            List<ChangeType> selectedTypes = [.. (changeTypes ?? []).Distinct()];

            return this.ReadInProjectAsync(projectId, async transaction =>
            {
                var commit = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, commitId, cancellationToken);

                return commit.IsError
                    ? commit.Errors
                    : await this.CommitGraphRepository.ReadChangeAsync(transaction, commitId, selectedTypes, queryParameters, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets one <see cref="DataVersion"/> of the change a commit of the project of the request carries.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="changeId">The identifier of the <see cref="DataVersion"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The <see cref="DataVersion"/>, or <see cref="ErrorType.NotFound"/> when the commit carries no such change.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<DataVersion>> GetCommitChangeByIdAsync(Guid projectId, Guid commitId, Guid changeId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, async transaction =>
            {
                var commit = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, commitId, cancellationToken);

                if (commit.IsError)
                {
                    return commit.Errors;
                }

                var version = await this.DataVersionRepository.ReadByIdAsync(transaction, changeId, cancellationToken);

                if (version.IsError)
                {
                    return version.Errors;
                }

                return version.Value.Commit == commitId
                    ? version
                    : Error.NotFound("dataVersion.notFound", $"The DataVersion {changeId} is not a change of commit {commitId}.");
            }, cancellationToken);
        }

        /// <summary>
        /// Gets one page of the commits of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of commits, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<Commit>>> GetCommitsAsync(Guid projectId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => ReadOwnedAsync(this.CommitRepository, transaction, projectId, queryParameters, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Gets the default branch of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The default branch, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Branch>> GetDefaultBranchAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => this.ReadBranchOrDefaultAsync(transaction, projectId, null, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets the head commit of a branch of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch, or <c>null</c> for the default branch of the project.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The head commit, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Commit>> GetHeadCommitAsync(Guid projectId, Guid? branchId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, async transaction =>
            {
                var branch = await this.ReadBranchOrDefaultAsync(transaction, projectId, branchId, cancellationToken);

                return branch.IsError ? branch.Errors : await this.ReadCommitAsync(transaction, projectId, branch.Value.Head, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets a tag of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="tagId">The identifier of the tag.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The tag, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Tag>> GetTagByIdAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => this.ReadTagAsync(transaction, projectId, tagId, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets the commit a tag of the project of the request is on.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="tagId">The identifier of the tag.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The tagged commit, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Commit>> GetTaggedCommitAsync(Guid projectId, Guid tagId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, async transaction =>
            {
                var tag = await this.ReadTagAsync(transaction, projectId, tagId, cancellationToken);

                return tag.IsError ? tag.Errors : await this.ReadCommitAsync(transaction, projectId, tag.Value.TaggedCommit, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets one page of the tags of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of tags, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<Tag>>> GetTagsAsync(Guid projectId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => ReadOwnedAsync(this.TagRepository, transaction, projectId, queryParameters, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Merges commits of the project of the request into one of its branches.
        /// </summary>
        /// <param name="baseBranchId">The identifier of the branch merged into.</param>
        /// <param name="commitsToMergeIds">The identifiers of the commits to merge.</param>
        /// <param name="resolution">The data that resolves the conflicts of the merge.</param>
        /// <param name="description">The description of the merge commit.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The <see cref="MergeResult"/> with either the merge commit or the unresolved conflicts, or the errors that prevented the merge.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="commitsToMergeIds"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">No project is in scope.</exception>
        /// <remarks>
        /// A data identity conflicts when the branch and a merged commit, or two merged commits, changed it differently since their
        /// merge base, or when one side changed data the other side's deletion of an element takes with it. Commits the head already
        /// descends from are not merged again, and a merge that brings no change answers the head itself rather than an empty commit.
        /// </remarks>
        public async Task<ErrorOr<MergeResult>> MergeIntoBranchAsync(Guid baseBranchId, IReadOnlyList<Guid> commitsToMergeIds, IReadOnlyList<IData> resolution, string description,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(commitsToMergeIds);

            var projectId = this.ProjectScope.ProjectId;
            List<IData> resolvingData = [.. resolution ?? []];

            if (commitsToMergeIds.Count == 0)
            {
                return Error.Validation("commitsToMerge.missing", "A merge names at least one commit to merge.");
            }

            if (resolvingData.Any(data => data == null || data.Id == Guid.Empty) || resolvingData.DistinctBy(data => data.Id).Count() != resolvingData.Count)
            {
                return Error.Validation("resolution.invalid", "Every resolving data carries its own, distinct identifier.");
            }

            return await this.WriteInProjectAsync<MergeResult>(projectId, async transaction =>
            {
                var branch = await this.ReadReferencedBranchAsync(transaction, projectId, baseBranchId, cancellationToken);

                if (branch.IsError)
                {
                    return branch.Errors;
                }

                var pending = await this.ReadCommitsToMergeAsync(transaction, projectId, branch.Value.Head, commitsToMergeIds, cancellationToken);

                if (pending.IsError)
                {
                    return pending.Errors;
                }

                if (pending.Value.Count == 0)
                {
                    return new MergeResult { MergeCommit = branch.Value.Head };
                }

                var plan = await this.PlanMergeAsync(transaction, branch.Value.Head, pending.Value, cancellationToken);

                if (plan.IsError)
                {
                    return plan.Errors;
                }

                var resolvedIds = resolvingData.Select(data => data.Id).ToHashSet();
                List<Guid> unresolved = [.. plan.Value.Conflicts.Where(identityId => !resolvedIds.Contains(identityId))];

                if (unresolved.Count > 0)
                {
                    return this.Conflicted(pending.Value.Count, baseBranchId, projectId, unresolved);
                }

                List<DataVersion> change =
                [
                    .. plan.Value.Changes.Where(version => !resolvedIds.Contains(version.Identity.Id)),
                    .. resolvingData.Select(data => new DataVersion { Identity = new DataIdentity { Id = data.Id }, Payload = data })
                ];

                if (change.Count == 0)
                {
                    return new MergeResult { MergeCommit = branch.Value.Head };
                }

                var mergeCommit = await this.CommitChangeAsync(transaction, projectId, branch.Value, change, pending.Value, new Commit { Description = description },
                    cancellationToken);

                if (mergeCommit.IsError && mergeCommit.FirstError.Code == DependentUpdatedCode)
                {
                    return this.Conflicted(pending.Value.Count, baseBranchId, projectId, (List<Guid>)mergeCommit.FirstError.Metadata[IdentitiesMetadataKey]);
                }

                return mergeCommit.IsError ? mergeCommit.Errors : new MergeResult { MergeCommit = mergeCommit.Value.Id };
            }, cancellationToken);
        }

        /// <summary>
        /// Makes a branch of the project of the request its default branch.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The updated project, or the errors that prevented updating it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        /// <remarks>
        /// Annex A (PIM-PCB-009) leaves the project unchanged for a branch that is not one of the project's and reports that the project has no
        /// such branch, whoever owns it; the project is not returned alongside the error, since the REST/HTTP PSM answers a failure with an error body only.
        /// </remarks>
        public Task<ErrorOr<Project>> SetDefaultBranchAsync(Guid projectId, Guid branchId, CancellationToken cancellationToken)
        {
            return this.WriteInProjectAsync(projectId, async transaction =>
            {
                var branch = await this.ReadBranchAsync(transaction, projectId, branchId, cancellationToken);

                if (branch.IsError)
                {
                    return branch.Errors;
                }

                var project = await this.ProjectRepository.ReadByIdAsync(transaction, projectId, cancellationToken);

                if (project.IsError)
                {
                    return project.Errors;
                }

                project.Value.DefaultBranch = branchId;

                return await this.ProjectRepository.UpdateAsync(transaction, project.Value, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gives the data identity a version of the given identifier belongs to, reusing the one at the previous commits.
        /// </summary>
        /// <param name="identityId">The identifier of the data identity.</param>
        /// <param name="previousState">The state at the previous commits, by data identity.</param>
        /// <returns>The existing <see cref="DataIdentity"/>, or a new one when the data is created.</returns>
        private static DataIdentity ExistingOrNewIdentity(Guid identityId, Dictionary<Guid, DataVersion> previousState)
        {
            return previousState.TryGetValue(identityId, out var previous) && previous.Identity != null ? previous.Identity : new DataIdentity { Id = identityId };
        }

        /// <summary>
        /// Gives the creation timestamp of a commit, strictly newer than every one of its previous commits at the resolution of the store.
        /// </summary>
        /// <param name="previousCommits">The previous commits.</param>
        /// <returns>The creation timestamp.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.1.2 requires version histories to increase monotonically in time.
        /// </remarks>
        private static DateTime NextCreated(IEnumerable<Commit> previousCommits)
        {
            var now = ToStoreResolution(DateTime.UtcNow);
            var latest = ToStoreResolution(previousCommits.Select(commit => commit.Created).DefaultIfEmpty(DateTime.MinValue).Max());

            return now > latest ? now : latest + TimestampResolution;
        }

        /// <summary>
        /// Reads one page of the records of a project.
        /// </summary>
        /// <typeparam name="TRecord">The type of record that is read.</typeparam>
        /// <param name="repository">The <see cref="IRecordRepository{TRecord}"/> of the records.</param>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of records, or the errors that prevented reading it.</returns>
        private static Task<ErrorOr<PagedResult<TRecord>>> ReadOwnedAsync<TRecord>(IRecordRepository<TRecord> repository, IDataStoreTransaction transaction, Guid projectId,
            QueryParameters queryParameters, CancellationToken cancellationToken) where TRecord : Record
        {
            return repository.ReadByFilterAsync(transaction, SearchFilter.EqualTo(OwningProjectProperty, projectId), queryParameters, cancellationToken);
        }

        /// <summary>
        /// Resolves one <see cref="DataVersion"/> of a requested change into the version that is written.
        /// </summary>
        /// <param name="version">The requested <see cref="DataVersion"/>.</param>
        /// <param name="previousState">The state at the previous commits, by data identity.</param>
        /// <returns>The version that is written, or the validation error of the request.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.3: a creation may leave its identity empty, an update names an existing identity,
        /// and a deletion names the existing identity it deletes and has no payload. §7.1.2 requires every data to provide its identifier
        /// and makes it the identifier of its identity; every version gets a new identifier.
        /// </remarks>
        private static ErrorOr<DataVersion> ResolveVersion(DataVersion version, Dictionary<Guid, DataVersion> previousState)
        {
            if (version == null)
            {
                return Error.Validation("change.missing", "A change does not hold an empty DataVersion.");
            }

            var identityId = version.Identity?.Id ?? Guid.Empty;

            if (version.Payload == null)
            {
                if (identityId == Guid.Empty)
                {
                    return Error.Validation("change.identity.missing", "A deletion names the identity of the data it deletes.");
                }

                if (!previousState.ContainsKey(identityId))
                {
                    return Error.Validation("change.identity.unknown", $"The data {identityId} does not exist at the previous commits, so it cannot be deleted.");
                }
            }
            else
            {
                if (version.Payload.Id == Guid.Empty)
                {
                    return Error.Validation("change.payload.id.missing", "The data a change versions carries its own identifier.");
                }

                if (identityId != Guid.Empty && identityId != version.Payload.Id)
                {
                    return Error.Validation("change.identity.mismatch", $"The identity {identityId} is not the identifier {version.Payload.Id} of the data it versions.");
                }

                identityId = version.Payload.Id;
            }

            return new DataVersion
            {
                Id = Guid.NewGuid(),
                Identity = ExistingOrNewIdentity(identityId, previousState),
                Payload = version.Payload
            };
        }

        /// <summary>
        /// Truncates a timestamp to the resolution the store keeps.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns>The timestamp as the store keeps it.</returns>
        private static DateTime ToStoreResolution(DateTime timestamp)
        {
            return new DateTime(timestamp.Ticks - timestamp.Ticks % TimestampResolution.Ticks, timestamp.Kind);
        }

        /// <summary>
        /// Writes a commit of a change on a branch and moves the head of the branch to it.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the writes take part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branch">The <see cref="Branch"/> the commit is made on.</param>
        /// <param name="change">The change, including the versions that settle what the previous commits disagree on.</param>
        /// <param name="otherParentIds">The identifiers of the previous commits besides the head of the branch.</param>
        /// <param name="template">The commit whose name, aliases and description are kept.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The created commit, or the errors that prevented creating it.</returns>
        /// <remarks>
        /// Annex A (PIM-PCB-010) has the service generate the identifier of the commit, so none supplied by the caller is kept.
        /// </remarks>
        private async Task<ErrorOr<Commit>> CommitChangeAsync(IDataStoreTransaction transaction, Guid projectId, Branch branch, IReadOnlyList<DataVersion> change,
            IReadOnlyList<Guid> otherParentIds, Commit template, CancellationToken cancellationToken)
        {
            IEnumerable<Guid> parentIds = [branch.Head, .. otherParentIds];
            List<Guid> previousIds = [.. parentIds.Where(commitId => commitId != Guid.Empty).Distinct()];

            var previousCommits = await this.ReadCommitsAsync(transaction, projectId, previousIds, cancellationToken);

            if (previousCommits.IsError)
            {
                return previousCommits.Errors;
            }

            var previousState = await this.ReadStateAsync(transaction, previousIds, cancellationToken);

            if (previousState.IsError)
            {
                return previousState.Errors;
            }

            var versions = await this.ResolveChangeAsync(transaction, previousIds, change, previousState.Value, cancellationToken);

            if (versions.IsError)
            {
                return versions.Errors;
            }

            var heldIdentities = await this.VerifyNewIdentitiesAsync(transaction, projectId, versions.Value, previousState.Value, cancellationToken);

            if (heldIdentities.IsError)
            {
                return heldIdentities.Errors;
            }

            var commit = new Commit
            {
                Id = Guid.NewGuid(),
                Name = template.Name,
                Alias = AliasIncludingName(template.Alias, template.Name),
                Description = template.Description,
                OwningProject = projectId,
                Created = NextCreated(previousCommits.Value),
                PreviousCommits = previousIds,
                Change = [.. versions.Value.Select(version => version.Id)]
            };

            var created = await this.CommitRepository.CreateAsync(transaction, commit, cancellationToken);

            if (created.IsError)
            {
                return created.Errors;
            }

            var written = await this.WriteVersionsAsync(transaction, commit.Id, versions.Value, previousState.Value, heldIdentities.Value, cancellationToken);

            if (written.IsError)
            {
                return written.Errors;
            }

            var moved = await this.CommitGraphRepository.MoveHeadAsync(transaction, branch.Id, branch.Head, commit.Id, cancellationToken);

            return moved.IsError ? moved.Errors : created;
        }

        /// <summary>
        /// Gives the result of a merge that left conflicts, after logging them.
        /// </summary>
        /// <param name="commitCount">The number of commits merged.</param>
        /// <param name="branchId">The identifier of the branch merged into.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="conflicts">The identifiers of the conflicting data identities.</param>
        /// <returns>The <see cref="MergeResult"/> that reports the conflicts.</returns>
        private MergeResult Conflicted(int commitCount, Guid branchId, Guid projectId, List<Guid> conflicts)
        {
            if (this.Logger.IsEnabled(LogLevel.Warning))
            {
                this.Logger.LogWarning("Merging {CommitCount} commits into branch {BranchId} of project {ProjectId} left {ConflictCount} conflicts unresolved",
                    commitCount, branchId, projectId, conflicts.Count);
            }

            return new MergeResult { Conflict = conflicts };
        }

        /// <summary>
        /// Leaves out of a set of merged commits those another commit of the set descends from.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="commitIds">The identifiers of the merged commits.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The identifiers of the commits no other commit of the set descends from, or the errors that prevented reading their ancestry.</returns>
        /// <remarks>
        /// A commit and one of its own ancestors would otherwise read as two sides that changed the same data differently.
        /// </remarks>
        private async Task<ErrorOr<List<Guid>>> ExcludeAncestorsOfOthersAsync(IDataStoreTransaction transaction, List<Guid> commitIds, CancellationToken cancellationToken)
        {
            if (commitIds.Count < 2)
            {
                return commitIds;
            }

            var ancestorsOfOthers = new HashSet<Guid>();

            foreach (var commitId in commitIds)
            {
                var ancestry = await this.CommitGraphRepository.ReadAncestryAsync(transaction, commitId, cancellationToken);

                if (ancestry.IsError)
                {
                    return ancestry.Errors;
                }

                ancestorsOfOthers.UnionWith(ancestry.Value.Select(commit => commit.Id).Where(ancestorId => ancestorId != commitId));
            }

            return commitIds.Where(commitId => !ancestorsOfOthers.Contains(commitId)).ToList();
        }

        /// <summary>
        /// Completes a requested change with what the previous commits of a commit disagree on, once its conflicts are verified to be resolved.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="headId">The identifier of the head commit of the branch.</param>
        /// <param name="otherParentIds">The identifiers of the other previous commits.</param>
        /// <param name="change">The requested change.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The completed change, or <see cref="ErrorType.Validation"/> when a conflict is left unresolved.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.1.2 requires a commit to resolve all conflicts in its parent commits; the merged
        /// versions the requested change does not name are added, as <see cref="MergeIntoBranchAsync"/> adds them.
        /// </remarks>
        private async Task<ErrorOr<List<DataVersion>>> IncludeParentChangesAsync(IDataStoreTransaction transaction, Guid projectId, Guid headId, List<Guid> otherParentIds,
            IReadOnlyList<DataVersion> change, CancellationToken cancellationToken)
        {
            List<DataVersion> fullChange = [.. change];

            if (otherParentIds.Count == 0)
            {
                return fullChange;
            }

            var otherParents = await this.ReadCommitsAsync(transaction, projectId, otherParentIds, cancellationToken);

            if (otherParents.IsError)
            {
                return otherParents.Errors;
            }

            var mergedIds = await this.ExcludeAncestorsOfOthersAsync(transaction, otherParentIds, cancellationToken);

            if (mergedIds.IsError)
            {
                return mergedIds.Errors;
            }

            var plan = await this.PlanMergeAsync(transaction, headId, mergedIds.Value, cancellationToken);

            if (plan.IsError)
            {
                return plan.Errors;
            }

            var changedIds = change.Where(version => version != null).Select(version => version.Identity?.Id ?? version.Payload?.Id ?? Guid.Empty).ToHashSet();
            List<Guid> unresolved = [.. plan.Value.Conflicts.Where(identityId => !changedIds.Contains(identityId))];

            if (unresolved.Count > 0)
            {
                return Error.Validation("commit.conflict.unresolved",
                    $"The change leaves the conflicts between the previous commits on {string.Join(", ", unresolved)} unresolved.");
            }

            fullChange.AddRange(plan.Value.Changes.Where(version => !changedIds.Contains(version.Identity.Id)));

            return fullChange;
        }

        /// <summary>
        /// Computes the changes a merge brings in and the data identities it cannot merge.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="headId">The identifier of the head commit of the branch merged into.</param>
        /// <param name="mergedCommitIds">The identifiers of the merged commits.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The <see cref="MergePlan"/>, or the errors that prevented computing it.</returns>
        private async Task<ErrorOr<MergePlan>> PlanMergeAsync(IDataStoreTransaction transaction, Guid headId, IReadOnlyList<Guid> mergedCommitIds,
            CancellationToken cancellationToken)
        {
            var mergeBase = await this.CommitGraphRepository.ReadMergeBaseAsync(transaction, [headId, .. mergedCommitIds], cancellationToken);

            if (mergeBase.IsError && mergeBase.FirstError.Type != ErrorType.NotFound)
            {
                return mergeBase.Errors;
            }

            var baseState = await this.ReadStateAsync(transaction, mergeBase.IsError ? [] : [mergeBase.Value.Id], cancellationToken);

            if (baseState.IsError)
            {
                return baseState.Errors;
            }

            var ours = await this.ReadStateAsync(transaction, [headId], cancellationToken);

            if (ours.IsError)
            {
                return ours.Errors;
            }

            List<Dictionary<Guid, DataVersion>> theirs = [];

            foreach (var mergedCommitId in mergedCommitIds)
            {
                var state = await this.ReadStateAsync(transaction, [mergedCommitId], cancellationToken);

                if (state.IsError)
                {
                    return state.Errors;
                }

                theirs.Add(state.Value);
            }

            var identityIds = baseState.Value.Keys.Union(ours.Value.Keys).Union(theirs.SelectMany(state => state.Keys)).Order();
            var plan = new MergePlan();

            foreach (var identityId in identityIds)
            {
                plan.Include(identityId, baseState.Value.GetValueOrDefault(identityId), ours.Value.GetValueOrDefault(identityId),
                    [.. theirs.Select(state => state.GetValueOrDefault(identityId))]);
            }

            return plan;
        }

        /// <summary>
        /// Reads a branch of a project.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The branch, or <see cref="ErrorType.NotFound"/> when the project has no such branch.</returns>
        private async Task<ErrorOr<Branch>> ReadBranchAsync(IDataStoreTransaction transaction, Guid projectId, Guid branchId, CancellationToken cancellationToken)
        {
            var branch = await this.BranchRepository.ReadByIdAsync(transaction, branchId, cancellationToken);

            return WithinProject(branch, record => record.OwningProject, branchId, projectId);
        }

        /// <summary>
        /// Reads a branch of a project, or its default branch.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch, or <c>null</c> for the default branch.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The branch, or the errors that prevented reading it.</returns>
        private async Task<ErrorOr<Branch>> ReadBranchOrDefaultAsync(IDataStoreTransaction transaction, Guid projectId, Guid? branchId, CancellationToken cancellationToken)
        {
            if (branchId.HasValue)
            {
                return await this.ReadReferencedBranchAsync(transaction, projectId, branchId.Value, cancellationToken);
            }

            var project = await this.ProjectRepository.ReadByIdAsync(transaction, projectId, cancellationToken);

            return project.IsError ? project.Errors : await this.ReadBranchAsync(transaction, projectId, project.Value.DefaultBranch, cancellationToken);
        }

        /// <summary>
        /// Reads a commit of a project.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The commit, or <see cref="ErrorType.NotFound"/> when the project has no such commit.</returns>
        private async Task<ErrorOr<Commit>> ReadCommitAsync(IDataStoreTransaction transaction, Guid projectId, Guid commitId, CancellationToken cancellationToken)
        {
            var commit = await this.CommitRepository.ReadByIdAsync(transaction, commitId, cancellationToken);

            return WithinProject(commit, record => record.OwningProject, commitId, projectId);
        }

        /// <summary>
        /// Reads commits that an operation takes as input references.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitIds">The identifiers of the commits.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The commits, or the errors of the first one that could not be read.</returns>
        private async Task<ErrorOr<List<Commit>>> ReadCommitsAsync(IDataStoreTransaction transaction, Guid projectId, IEnumerable<Guid> commitIds,
            CancellationToken cancellationToken)
        {
            List<Commit> commits = [];

            foreach (var commitId in commitIds)
            {
                var commit = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, commitId, cancellationToken);

                if (commit.IsError)
                {
                    return commit.Errors;
                }

                commits.Add(commit.Value);
            }

            return commits;
        }

        /// <summary>
        /// Reads the commits to merge, leaving out those the head already descends from.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="headId">The identifier of the head commit of the branch merged into.</param>
        /// <param name="commitIds">The identifiers of the commits to merge.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The identifiers of the commits still to merge, or the errors of the first one that could not be read.</returns>
        private async Task<ErrorOr<List<Guid>>> ReadCommitsToMergeAsync(IDataStoreTransaction transaction, Guid projectId, Guid headId, IEnumerable<Guid> commitIds,
            CancellationToken cancellationToken)
        {
            List<Guid> distinctIds = [.. commitIds.Distinct()];

            var commits = await this.ReadCommitsAsync(transaction, projectId, distinctIds, cancellationToken);

            if (commits.IsError)
            {
                return commits.Errors;
            }

            var ancestry = await this.CommitGraphRepository.ReadAncestryAsync(transaction, headId, cancellationToken);

            if (ancestry.IsError)
            {
                return ancestry.Errors;
            }

            var ancestorIds = ancestry.Value.Select(commit => commit.Id).ToHashSet();

            return await this.ExcludeAncestorsOfOthersAsync(transaction, [.. distinctIds.Where(commitId => !ancestorIds.Contains(commitId))], cancellationToken);
        }

        /// <summary>
        /// Reads which of the elements a change deletes take the given data with them.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="previousIds">The identifiers of the previous commits the dependent data is read at.</param>
        /// <param name="deletedElementIds">The identifiers of the deleted elements.</param>
        /// <param name="dependentIds">The identifiers of the dependent data.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The identifiers of the deleted elements the dependent data depends on, or the errors that prevented reading them.</returns>
        private async Task<ErrorOr<List<Guid>>> ReadDeletedElementsDependedOnAsync(IDataStoreTransaction transaction, IReadOnlyList<Guid> previousIds,
            IReadOnlyList<Guid> deletedElementIds, IReadOnlySet<Guid> dependentIds, CancellationToken cancellationToken)
        {
            List<Guid> dependedOn = [];

            foreach (var elementId in deletedElementIds)
            {
                foreach (var commitId in previousIds)
                {
                    var dependent = await this.DataRepository.ReadDependentDataClosureAsync(transaction, commitId, [elementId], cancellationToken);

                    if (dependent.IsError)
                    {
                        return dependent.Errors;
                    }

                    if (dependent.Value.Any(dependentIds.Contains))
                    {
                        dependedOn.Add(elementId);

                        break;
                    }
                }
            }

            return dependedOn;
        }

        /// <summary>
        /// Reads a branch that an operation takes as an input reference.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The branch, <see cref="ErrorType.NotFound"/> when it does not exist, or <see cref="ErrorType.Validation"/> when another project owns it.</returns>
        private async Task<ErrorOr<Branch>> ReadReferencedBranchAsync(IDataStoreTransaction transaction, Guid projectId, Guid branchId, CancellationToken cancellationToken)
        {
            var branch = await this.BranchRepository.ReadByIdAsync(transaction, branchId, cancellationToken);

            return ReferencedWithinProject(branch, record => record.OwningProject, branchId, projectId);
        }

        /// <summary>
        /// Reads the state of the project at a set of commits, as the data versions in effect keyed by data identity.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="commitIds">The identifiers of the commits, the first of which wins when two disagree.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The state, or the errors that prevented reading it.</returns>
        private async Task<ErrorOr<Dictionary<Guid, DataVersion>>> ReadStateAsync(IDataStoreTransaction transaction, IEnumerable<Guid> commitIds,
            CancellationToken cancellationToken)
        {
            var state = new Dictionary<Guid, DataVersion>();

            foreach (var commitId in commitIds)
            {
                var versionedData = await this.CommitGraphRepository.ReadVersionedDataAsync(transaction, commitId, cancellationToken);

                if (versionedData.IsError)
                {
                    return versionedData.Errors;
                }

                foreach (var version in versionedData.Value.Where(version => version.Identity != null))
                {
                    state.TryAdd(version.Identity.Id, version);
                }
            }

            return state;
        }

        /// <summary>
        /// Reads a tag of a project.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="tagId">The identifier of the tag.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The tag, or <see cref="ErrorType.NotFound"/> when the project has no such tag.</returns>
        private async Task<ErrorOr<Tag>> ReadTagAsync(IDataStoreTransaction transaction, Guid projectId, Guid tagId, CancellationToken cancellationToken)
        {
            var tag = await this.TagRepository.ReadByIdAsync(transaction, tagId, cancellationToken);

            return WithinProject(tag, record => record.OwningProject, tagId, projectId);
        }

        /// <summary>
        /// Resolves a change into the versions that are written, adding the deletion of the data a deleted element takes with it.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="previousIds">The identifiers of the previous commits the dependent data is read at.</param>
        /// <param name="change">The change.</param>
        /// <param name="previousState">The state at the previous commits, by data identity.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The versions that are written, or the errors that prevented resolving them.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.3: deleting an element deletes its incoming relationships, read at every previous
        /// commit so that a merge also drops those only one parent holds; §7.1.2 keeps the identity of a version unique within a change.
        /// The references other data hold in their stored properties, which §7.2.3 also removes, are left to the core-data services.
        /// </remarks>
        private async Task<ErrorOr<List<DataVersion>>> ResolveChangeAsync(IDataStoreTransaction transaction, IReadOnlyList<Guid> previousIds,
            IReadOnlyList<DataVersion> change, Dictionary<Guid, DataVersion> previousState, CancellationToken cancellationToken)
        {
            List<DataVersion> versions = [];
            var identityIds = new HashSet<Guid>();

            foreach (var requested in change)
            {
                var version = ResolveVersion(requested, previousState);

                if (version.IsError)
                {
                    return version.Errors;
                }

                if (!identityIds.Add(version.Value.Identity.Id))
                {
                    return Error.Validation("change.identity.duplicate", $"The change versions the data {version.Value.Identity.Id} more than once.");
                }

                versions.Add(version.Value);
            }

            List<Guid> deletedElementIds =
            [
                .. versions.Where(version => version.Payload == null && previousState[version.Identity.Id].Payload is IElement).Select(version => version.Identity.Id)
            ];

            if (deletedElementIds.Count == 0)
            {
                return versions;
            }

            var updatedDependents = new HashSet<Guid>();

            foreach (var commitId in previousIds)
            {
                var dependent = await this.DataRepository.ReadDependentDataClosureAsync(transaction, commitId, deletedElementIds, cancellationToken);

                if (dependent.IsError)
                {
                    return dependent.Errors;
                }

                foreach (var dataId in dependent.Value.Where(previousState.ContainsKey))
                {
                    if (identityIds.Add(dataId))
                    {
                        versions.Add(new DataVersion { Id = Guid.NewGuid(), Identity = ExistingOrNewIdentity(dataId, previousState) });
                    }
                    else if (versions.Find(version => version.Identity.Id == dataId).Payload != null)
                    {
                        updatedDependents.Add(dataId);
                    }
                }
            }

            if (updatedDependents.Count == 0)
            {
                return versions;
            }

            var dependedOn = await this.ReadDeletedElementsDependedOnAsync(transaction, previousIds, deletedElementIds, updatedDependents, cancellationToken);

            if (dependedOn.IsError)
            {
                return dependedOn.Errors;
            }

            List<Guid> involved = [.. updatedDependents, .. dependedOn.Value];

            return Error.Validation(DependentUpdatedCode,
                $"The change updates {string.Join(", ", updatedDependents)}, which the deletion of {string.Join(", ", dependedOn.Value)} deletes.",
                new Dictionary<string, object> { [IdentitiesMetadataKey] = involved });
        }

        /// <summary>
        /// Verifies that the data a change creates comes with identities no other project owns.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="versions">The versions that are written.</param>
        /// <param name="previousState">The state at the previous commits, by data identity.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The identities of created data the project already holds, or <see cref="ErrorType.Validation"/> when another project owns one.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.3 only requires the identity of created data to be new to the previous commits, so data
        /// deleted earlier, or created on another branch of the project, keeps its identity.
        /// </remarks>
        private async Task<ErrorOr<HashSet<Guid>>> VerifyNewIdentitiesAsync(IDataStoreTransaction transaction, Guid projectId, IEnumerable<DataVersion> versions,
            Dictionary<Guid, DataVersion> previousState, CancellationToken cancellationToken)
        {
            var heldIdentityIds = new HashSet<Guid>();

            foreach (var identityId in versions.Where(version => version.Payload != null && !previousState.ContainsKey(version.Identity.Id)).Select(version => version.Identity.Id))
            {
                var owner = await this.DataRepository.ReadIdentityOwnerAsync(transaction, identityId, cancellationToken);

                if (owner.IsError && owner.FirstError.Type == ErrorType.NotFound)
                {
                    continue;
                }

                if (owner.IsError)
                {
                    return owner.Errors;
                }

                if (owner.Value != projectId)
                {
                    return Error.Validation("change.identity.taken", $"The identity {identityId} belongs to another project, so it cannot identify created data.");
                }

                heldIdentityIds.Add(identityId);
            }

            return heldIdentityIds;
        }

        /// <summary>
        /// Writes the data versions of a commit, creating the identities the project does not hold yet.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the writes take part in.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="versions">The versions that are written.</param>
        /// <param name="previousState">The state at the previous commits, by data identity.</param>
        /// <param name="heldIdentityIds">The identities of created data the project already holds.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or the errors that prevented writing.</returns>
        private async Task<ErrorOr<Success>> WriteVersionsAsync(IDataStoreTransaction transaction, Guid commitId, IEnumerable<DataVersion> versions,
            Dictionary<Guid, DataVersion> previousState, HashSet<Guid> heldIdentityIds, CancellationToken cancellationToken)
        {
            foreach (var version in versions)
            {
                version.Commit = commitId;

                if (version.Payload != null && !previousState.ContainsKey(version.Identity.Id) && !heldIdentityIds.Contains(version.Identity.Id))
                {
                    version.Identity.Version = [version.Id];

                    var createdIdentity = await this.DataIdentityRepository.CreateAsync(transaction, version.Identity, cancellationToken);

                    if (createdIdentity.IsError)
                    {
                        return createdIdentity.Errors;
                    }
                }

                var created = await this.DataVersionRepository.CreateAsync(transaction, version, cancellationToken);

                if (created.IsError)
                {
                    return created.Errors;
                }
            }

            return Result.Success;
        }

        /// <summary>
        /// The changes a merge brings in and the data identities it cannot merge.
        /// </summary>
        private sealed class MergePlan
        {
            /// <summary>
            /// Gets the versions the merge commit carries, without identifier, commit or resolution.
            /// </summary>
            public List<DataVersion> Changes { get; } = [];

            /// <summary>
            /// Gets the identifiers of the data identities that were changed differently on two sides of the merge.
            /// </summary>
            public List<Guid> Conflicts { get; } = [];

            /// <summary>
            /// Plans the merge of one data identity from its versions at the merge base, at the head and at each merged commit.
            /// </summary>
            /// <param name="identityId">The identifier of the data identity.</param>
            /// <param name="baseVersion">The version at the merge base, or <c>null</c> when absent.</param>
            /// <param name="ours">The version at the head, or <c>null</c> when absent.</param>
            /// <param name="theirs">The version at each merged commit, <c>null</c> where absent.</param>
            /// <remarks>
            /// The merge commit lists every identity on which a parent differs from the merged state, so that it carries all it brings in
            /// and settles what the parents disagree on (§7.1.2), and a merge that brings nothing has no change.
            /// </remarks>
            public void Include(Guid identityId, DataVersion baseVersion, DataVersion ours, IReadOnlyList<DataVersion> theirs)
            {
                var baseVersionId = baseVersion?.Id;
                List<DataVersion> incoming = [.. theirs.Where(version => version?.Id != baseVersionId).DistinctBy(version => version?.Id)];

                if (incoming.Count > 1 || (incoming.Count == 1 && incoming[0]?.Id != ours?.Id && ours?.Id != baseVersionId))
                {
                    this.Conflicts.Add(identityId);

                    return;
                }

                var merged = incoming.Count == 1 ? incoming[0] : ours;

                if (!theirs.Prepend(ours).Any(parent => parent?.Id != merged?.Id))
                {
                    return;
                }

                this.Changes.Add(new DataVersion
                {
                    Identity = merged?.Identity ?? theirs.Prepend(ours).First(parent => parent != null).Identity,
                    Payload = merged?.Payload
                });
            }
        }
    }
}
