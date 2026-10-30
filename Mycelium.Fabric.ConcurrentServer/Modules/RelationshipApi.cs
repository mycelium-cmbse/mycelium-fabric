// ------------------------------------------------------------------------------------------------
//  <copyright file="RelationshipApi.cs" company="Starion Group S.A.">
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
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Handlers of the Relationship routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>ElementNavigationService::getRelationshipsByRelatedElement</c> (Systems Modeling API and Services 1.0 §7.2.2) to its §8.1.3 endpoint.
    /// </remarks>
    public partial class RelationshipApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/elements/{relatedElementId}/relationships</c>: <c>ElementNavigationService::getRelationshipsByRelatedElement</c> (Systems Modeling API and Services 1.0 §7.2.2).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="commitId">The commit identifier.</param>
        /// <param name="relatedElementId">The identifier of the element the relationships relate to.</param>
        /// <param name="navigationService">The <see cref="IElementNavigationService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// The <c>direction</c> query parameter takes <c>in</c>, <c>out</c> or <c>both</c>.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetRelationshipsByProjectCommitRelatedElement(HttpContext context, Guid projectId, Guid commitId, Guid relatedElementId,
            [FromServices] IElementNavigationService navigationService, ILogger<RelationshipApi> logger)
        {
            const string operationId = "getRelationshipsByProjectCommitRelatedElement";

            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("{OperationId} invoked for project {ProjectId}, commit {CommitId} and related element {RelatedElementId}", operationId,
                    projectId, commitId, relatedElementId);
            }

            if (navigationService == null)
            {
                await NotYetImplemented(context, operationId);

                return;
            }

            try
            {
                var direction = context.Request.ReadDirection();

                if (direction.IsError)
                {
                    await Failed(context, operationId, direction.Errors);

                    return;
                }

                await PagedAsync(context, operationId,
                    queryParameters => navigationService.GetRelationshipsByRelatedElementAsync(projectId, commitId, relatedElementId, direction.Value, queryParameters,
                        context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, operationId, exception, logger);
            }
        }
    }
}
