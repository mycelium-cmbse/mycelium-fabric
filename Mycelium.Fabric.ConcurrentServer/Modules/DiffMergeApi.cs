// ------------------------------------------------------------------------------------------------
//  <copyright file="DiffMergeApi.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Serialization;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Handlers of the diff and merge routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>ProjectDataVersioningService</c> <c>diffCommits</c> and <c>mergeIntoBranch</c> (Systems Modeling API and Services 1.0 §7.2.3) to their §8.1.3 endpoints.
    /// </remarks>
    public partial class DiffMergeApi : ApiBase
    {
        /// <summary>
        /// The comma-separated query parameter naming the commits to merge.
        /// </summary>
        private const string SourceCommitIdParameter = "sourceCommitId";

        /// <summary>
        /// The query parameter carrying the description of the merged commit.
        /// </summary>
        private const string DescriptionParameter = "description";

        /// <summary>
        /// The query parameter naming the base commit of a difference.
        /// </summary>
        private const string BaseCommitIdParameter = "baseCommitId";

        /// <summary>
        /// The query parameter filtering the reported changes by <c>ChangeType</c>.
        /// </summary>
        private const string ChangeTypesParameter = "changeTypes";

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/branches/{targetBranchId}/merge</c>: <c>ProjectDataVersioningService::mergeIntoBranch</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="targetBranchId">The identifier of the branch merged into.</param>
        /// <param name="resolution">The optional conflict resolution Data from the request body.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Commits to merge come from the required <c>sourceCommitId</c> query parameter; the target branch and those commits must belong to the project, since
        /// <c>mergeIntoBranch</c> takes no project. A merge answers <c>201</c> with its commit or <c>409</c> with the conflicts.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(SysML2.NET.PSM.DTO.Error), StatusCodes.Status500InternalServerError)]
        private static async Task Merge(HttpContext context, Guid projectId, Guid targetBranchId, DataBody resolution,
            [FromServices] IProjectDataVersioningService versioningService, ILogger<DiffMergeApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("merge invoked for project {ProjectId} and target branch {TargetBranchId}", projectId, targetBranchId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "merge");

                return;
            }

            try
            {
                var commitsToMerge = context.Request.ReadIds(SourceCommitIdParameter);

                if (commitsToMerge.IsError)
                {
                    await Failed(context, "merge", commitsToMerge.Errors);

                    return;
                }

                if (commitsToMerge.Value.Count == 0)
                {
                    await Invalid(context, "merge", $"{SourceCommitIdParameter}.missing", $"The '{SourceCommitIdParameter}' query parameter is required.");

                    return;
                }

                if (resolution.Content.IsError)
                {
                    await Failed(context, "merge", resolution.Content.Errors);

                    return;
                }

                var scope = await InProjectAsync(versioningService, projectId, targetBranchId, commitsToMerge.Value, context.RequestAborted);

                if (scope.IsError)
                {
                    await Failed(context, "merge", scope.Errors);

                    return;
                }

                var outcome = await versioningService.MergeIntoBranchAsync(targetBranchId, commitsToMerge.Value, resolution.Content.Value,
                    context.Request.Query[DescriptionParameter], context.RequestAborted);

                if (outcome.IsError)
                {
                    await Failed(context, "merge", outcome.Errors);

                    return;
                }

                await MergeResultMapper.ToPsm(outcome.Value).Match(mergeCommit => Negotiated(context, mergeCommit, StatusCodes.Status201Created),
                    conflict => Negotiated(context, conflict, StatusCodes.Status409Conflict));
            }
            catch (Exception exception)
            {
                await Failure(context, "merge", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{compareCommitId}/diff</c>: <c>ProjectDataVersioningService::diffCommits</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="compareCommitId">The identifier of the commit compared against the base commit.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// The base commit comes from the required <c>baseCommitId</c> query parameter; both commits must belong to the project, since <c>diffCommits</c> takes
        /// no project. The <c>changeTypes</c> query parameter is an extension of the OpenAPI document carrying the §7.2.3 <c>changeTypes</c> argument.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(SysML2.NET.PSM.DTO.Error), StatusCodes.Status500InternalServerError)]
        private static async Task Diff(HttpContext context, Guid projectId, Guid compareCommitId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<DiffMergeApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("diff invoked for project {ProjectId} and compare commit {CompareCommitId}", projectId, compareCommitId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "diff");

                return;
            }

            try
            {
                var baseCommitId = context.Request.ReadRequiredId(BaseCommitIdParameter);

                if (baseCommitId.IsError)
                {
                    await Failed(context, "diff", baseCommitId.Errors);

                    return;
                }

                var changeTypes = context.Request.ReadChangeTypes(ChangeTypesParameter);

                if (changeTypes.IsError)
                {
                    await Failed(context, "diff", changeTypes.Errors);

                    return;
                }

                var scope = await InProjectAsync(versioningService, projectId, null, [baseCommitId.Value, compareCommitId], context.RequestAborted);

                if (scope.IsError)
                {
                    await Failed(context, "diff", scope.Errors);

                    return;
                }

                await PagedAsync<DataDifference, SysML2.NET.PIM.DTO.DataDifference>(context, "diff",
                    queryParameters => versioningService.DiffCommitsAsync(baseCommitId.Value, compareCommitId, changeTypes.Value, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, "diff", exception, logger);
            }
        }

        /// <summary>
        /// Checks that a branch and commits belong to a project, through the project-scoped reads of the versioning service.
        /// </summary>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="branchId">The branch identifier, or <c>null</c> when no branch is involved.</param>
        /// <param name="commitIds">The commit identifiers.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> of the request.</param>
        /// <returns>Success, or the errors of the first read that failed.</returns>
        private static async Task<ErrorOr<Success>> InProjectAsync(IProjectDataVersioningService versioningService, Guid projectId, Guid? branchId,
            IEnumerable<Guid> commitIds, CancellationToken cancellationToken)
        {
            if (branchId.HasValue)
            {
                var branch = await versioningService.GetBranchByIdAsync(projectId, branchId.Value, cancellationToken);

                if (branch.IsError)
                {
                    return branch.Errors;
                }
            }

            foreach (var commitId in commitIds)
            {
                var commit = await versioningService.GetCommitByIdAsync(projectId, commitId, cancellationToken);

                if (commit.IsError)
                {
                    return commit.Errors;
                }
            }

            return Result.Success;
        }
    }
}
