// ------------------------------------------------------------------------------------------------
//  <copyright file="CommitApi.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Modules
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Serialization;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Handlers of the Commit routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>ProjectDataVersioningService</c> commit operations (Systems Modeling API and Services 1.0 §7.2.3) to their §8.1.3 endpoints.
    /// </remarks>
    public partial class CommitApi : ApiBase
    {
        /// <summary>
        /// The query parameter selecting the branch a commit is created on.
        /// </summary>
        private const string BranchIdParameter = "branchId";

        /// <summary>
        /// The query parameter filtering the reported changes by <c>ChangeType</c>.
        /// </summary>
        private const string ChangeTypesParameter = "changeTypes";

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits</c>: <c>ProjectDataVersioningService::getCommits</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetCommitsByProject(HttpContext context, Guid projectId, [FromServices] IProjectDataVersioningService versioningService, ILogger<CommitApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getCommitsByProject invoked for project {ProjectId}", projectId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "getCommitsByProject");

                return;
            }

            try
            {
                await PagedAsync<Commit, SysML2.NET.PIM.DTO.Commit>(context, "getCommitsByProject",
                    queryParameters => versioningService.GetCommitsAsync(projectId, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, "getCommitsByProject", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/commits</c>: <c>ProjectDataVersioningService::createCommit</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="body">The <c>CommitRequest</c> from the request body.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// The branch comes from the optional <c>branchId</c> query parameter; <c>previousCommits</c> has no REST source and is passed empty.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task PostCommitByProject(HttpContext context, Guid projectId, RequestBody<CommitRequest> body,
            [FromServices] IProjectDataVersioningService versioningService, ILogger<CommitApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("postCommitByProject invoked for project {ProjectId}", projectId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "postCommitByProject");

                return;
            }

            try
            {
                var branchId = context.Request.ReadOptionalId(BranchIdParameter);

                if (branchId.IsError)
                {
                    await Failed(context, "postCommitByProject", branchId.Errors);

                    return;
                }

                if (body.Content.IsError)
                {
                    await Failed(context, "postCommitByProject", body.Content.Errors);

                    return;
                }

                var (_, change) = body.Content.Value.ToPimWithChange();

                var outcome = await versioningService.CreateCommitAsync(change, branchId.Value, [], projectId, context.RequestAborted);

                await WriteAsync<Commit, SysML2.NET.PIM.DTO.Commit>(context, "postCommitByProject", outcome, StatusCodes.Status201Created);
            }
            catch (Exception exception)
            {
                await Failure(context, "postCommitByProject", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}</c>: <c>ProjectDataVersioningService::getCommitById</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetCommitByProjectAndId(HttpContext context, Guid projectId, Guid commitId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<CommitApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getCommitByProjectAndId invoked for project {ProjectId} and commit {CommitId}", projectId, commitId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "getCommitByProjectAndId");

                return;
            }

            try
            {
                var outcome = await versioningService.GetCommitByIdAsync(projectId, commitId, context.RequestAborted);

                await WriteAsync<Commit, SysML2.NET.PIM.DTO.Commit>(context, "getCommitByProjectAndId", outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, "getCommitByProjectAndId", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/changes</c>: <c>ProjectDataVersioningService::getCommitChange</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// The optional <c>changeTypes</c> query parameter, an extension of the OpenAPI document, carries the §7.2.3 <c>changeTypes</c> argument.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetChangesByProjectCommit(HttpContext context, Guid projectId, Guid commitId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<CommitApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getChangesByProjectCommit invoked for project {ProjectId} and commit {CommitId}", projectId, commitId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "getChangesByProjectCommit");

                return;
            }

            try
            {
                var changeTypes = context.Request.ReadChangeTypes(ChangeTypesParameter);

                if (changeTypes.IsError)
                {
                    await Failed(context, "getChangesByProjectCommit", changeTypes.Errors);

                    return;
                }

                await PagedAsync<DataVersion, SysML2.NET.PIM.DTO.DataVersion>(context, "getChangesByProjectCommit",
                    queryParameters => versioningService.GetCommitChangeAsync(projectId, commitId, changeTypes.Value, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, "getChangesByProjectCommit", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/changes/{changeId}</c>: <c>ProjectDataVersioningService::getCommitChangeById</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="changeId">The identifier of the <c>DataVersion</c> that changed.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetChangeByProjectCommitId(HttpContext context, Guid projectId, Guid commitId, Guid changeId,
            [FromServices] IProjectDataVersioningService versioningService, ILogger<CommitApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getChangeByProjectCommitId invoked for project {ProjectId}, commit {CommitId} and change {ChangeId}", projectId, commitId, changeId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "getChangeByProjectCommitId");

                return;
            }

            try
            {
                var outcome = await versioningService.GetCommitChangeByIdAsync(projectId, commitId, changeId, context.RequestAborted);

                await WriteAsync<DataVersion, SysML2.NET.PIM.DTO.DataVersion>(context, "getChangeByProjectCommitId", outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, "getChangeByProjectCommitId", exception, logger);
            }
        }
    }
}
