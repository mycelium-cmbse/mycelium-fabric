// ------------------------------------------------------------------------------------------------
//  <copyright file="BranchApi.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.ConcurrentServer.Serialization;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Handlers of the Branch routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>ProjectDataVersioningService</c> branch operations (Systems Modeling API and Services 1.0 §7.2.3) to their §8.1.3 endpoints.
    /// </remarks>
    public partial class BranchApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects/{projectId}/branches</c>: <c>ProjectDataVersioningService::getBranches</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetBranchesByProject(HttpContext context, Guid projectId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<BranchApi> logger)
        {
            const string operationId = "getBranchesByProject";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ProjectInvokedLogTemplate, operationId, projectId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                await PagedAsync<Branch, SysML2.NET.PIM.DTO.Branch>(context, operationId,
                    queryParameters => versioningService.GetBranchesAsync(projectId, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/branches</c>: <c>ProjectDataVersioningService::createBranch</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="body">The <c>BranchRequest</c> from the request body.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task PostBranchByProject(HttpContext context, Guid projectId, RequestBody<BranchRequest> body,
            [FromServices] IProjectDataVersioningService versioningService, ILogger<BranchApi> logger)
        {
            const string operationId = "postBranchByProject";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ProjectInvokedLogTemplate, operationId, projectId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                if (body.Content.IsError)
                {
                    await Failed(context, operationId, body.Content.Errors);

                    return;
                }

                var request = body.Content.Value;

                var outcome = await versioningService.CreateBranchAsync(projectId, request.Name, request.Head.Value, context.RequestAborted);

                await WriteAsync<Branch, SysML2.NET.PIM.DTO.Branch>(context, operationId, outcome, StatusCodes.Status201Created);
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/branches/{branchId}</c>: <c>ProjectDataVersioningService::getBranchById</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="branchId">The branch identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetBranchesByProjectAndId(HttpContext context, Guid projectId, Guid branchId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<BranchApi> logger)
        {
            const string operationId = "getBranchesByProjectAndId";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(BranchInvokedLogTemplate, operationId, projectId, branchId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                var outcome = await versioningService.GetBranchByIdAsync(projectId, branchId, context.RequestAborted);

                await WriteAsync<Branch, SysML2.NET.PIM.DTO.Branch>(context, operationId, outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>DELETE /projects/{projectId}/branches/{branchId}</c>: <c>ProjectDataVersioningService::deleteBranch</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="branchId">The branch identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Answers <c>200</c> with the deleted branch, or <c>204</c> when the request carries <c>Prefer: return=minimal</c> (RFC 7240 §4.2).
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task DeleteBranchByProjectAndId(HttpContext context, Guid projectId, Guid branchId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<BranchApi> logger)
        {
            const string operationId = "deleteBranchByProjectAndId";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(BranchInvokedLogTemplate, operationId, projectId, branchId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                var outcome = await versioningService.DeleteBranchAsync(projectId, branchId, context.RequestAborted);

                await DeletedAsync<Branch, SysML2.NET.PIM.DTO.Branch>(context, operationId, outcome);
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }
    }
}
