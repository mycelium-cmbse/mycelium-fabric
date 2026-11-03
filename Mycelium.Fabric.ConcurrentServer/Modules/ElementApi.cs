// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementApi.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Handlers of the Element routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>ElementNavigationService</c> operations (Systems Modeling API and Services 1.0 §7.2.2) to their §8.1.3 endpoints.
    /// </remarks>
    public partial class ElementApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/elements</c>: <c>ElementNavigationService::getElements</c> (Systems Modeling API and Services 1.0 §7.2.2).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="navigationService">The <see cref="IElementNavigationService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetElementsByProjectCommit(HttpContext context, Guid projectId, Guid commitId, [FromServices] IElementNavigationService navigationService,
            ILogger<ElementApi> logger)
        {
            const string operationId = "getElementsByProjectCommit";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(CommitInvokedLogTemplate, operationId, projectId, commitId);
            }

            if (navigationService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                await PagedAsync(context, operationId,
                    queryParameters => navigationService.GetElementsAsync(projectId, commitId, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/elements/{elementId}</c>: <c>ElementNavigationService::getElementById</c> (Systems Modeling API and Services 1.0 §7.2.2).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="elementId">The element identifier.</param>
        /// <param name="navigationService">The <see cref="IElementNavigationService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetElementByProjectCommitId(HttpContext context, Guid projectId, Guid commitId, Guid elementId,
            [FromServices] IElementNavigationService navigationService, ILogger<ElementApi> logger)
        {
            const string operationId = "getElementByProjectCommitId";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ElementInvokedLogTemplate, operationId, projectId, commitId, elementId);
            }

            if (navigationService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                var outcome = await navigationService.GetElementByIdAsync(projectId, commitId, elementId, context.RequestAborted);

                await WriteAsync(context, operationId, outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/elements/{elementId}/projectUsage</c>, which has no §8.1.3 counterpart.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="elementId">The element identifier.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Not yet implemented: §8.1.3 realizes <c>ProjectUsageService</c> (§7.2.6) through queries, so the semantics of this route are still to be decided.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static Task GetProjectUsageByProjectCommitElement(HttpContext context, Guid projectId, Guid commitId, Guid elementId, ILogger<ElementApi> logger)
        {
            const string operationId = "getProjectUsageByProjectCommitElement";

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(ElementInvokedLogTemplate, operationId, projectId, commitId,
                    elementId);
            }

            return NotYetImplemented(context, operationId);
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/roots</c>: <c>ElementNavigationService::getRootElements</c> (Systems Modeling API and Services 1.0 §7.2.2).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="navigationService">The <see cref="IElementNavigationService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetRootsByProjectCommit(HttpContext context, Guid projectId, Guid commitId, [FromServices] IElementNavigationService navigationService,
            ILogger<ElementApi> logger)
        {
            const string operationId = "getRootsByProjectCommit";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(CommitInvokedLogTemplate, operationId, projectId, commitId);
            }

            if (navigationService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                await PagedAsync(context, operationId,
                    queryParameters => navigationService.GetRootElementsAsync(projectId, commitId, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }
    }
}
