// ------------------------------------------------------------------------------------------------
//  <copyright file="TagApi.cs" company="Starion Group S.A.">
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
    /// Handlers of the Tag routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>ProjectDataVersioningService</c> tag operations (Systems Modeling API and Services 1.0 §7.2.3) to their §8.1.3 endpoints.
    /// </remarks>
    public partial class TagApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects/{projectId}/tags</c>: <c>ProjectDataVersioningService::getTags</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetTagsByProject(HttpContext context, Guid projectId, [FromServices] IProjectDataVersioningService versioningService, ILogger<TagApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getTagsByProject invoked for project {ProjectId}", projectId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "getTagsByProject");

                return;
            }

            try
            {
                await PagedAsync<Tag, SysML2.NET.PIM.DTO.Tag>(context, "getTagsByProject",
                    queryParameters => versioningService.GetTagsAsync(projectId, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, "getTagsByProject", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/tags</c>: <c>ProjectDataVersioningService::createTag</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="body">The <c>TagRequest</c> from the request body.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task PostTagByProject(HttpContext context, Guid projectId, RequestBody<TagRequest> body, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<TagApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("postTagByProject invoked for project {ProjectId}", projectId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "postTagByProject");

                return;
            }

            try
            {
                if (body.Content.IsError)
                {
                    await Failed(context, "postTagByProject", body.Content.Errors);

                    return;
                }

                var request = body.Content.Value;

                var outcome = await versioningService.CreateTagAsync(projectId, request.Name, request.TaggedCommit, context.RequestAborted);

                await WriteAsync<Tag, SysML2.NET.PIM.DTO.Tag>(context, "postTagByProject", outcome, StatusCodes.Status201Created);
            }
            catch (Exception exception)
            {
                await Failure(context, "postTagByProject", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/tags/{tagId}</c>: <c>ProjectDataVersioningService::getTagById</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="tagId">The tag identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetTagByProjectAndId(HttpContext context, Guid projectId, Guid tagId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<TagApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getTagByProjectAndId invoked for project {ProjectId} and tag {TagId}", projectId, tagId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "getTagByProjectAndId");

                return;
            }

            try
            {
                var outcome = await versioningService.GetTagByIdAsync(projectId, tagId, context.RequestAborted);

                await WriteAsync<Tag, SysML2.NET.PIM.DTO.Tag>(context, "getTagByProjectAndId", outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, "getTagByProjectAndId", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>DELETE /projects/{projectId}/tags/{tagId}</c>: <c>ProjectDataVersioningService::deleteTag</c> (Systems Modeling API and Services 1.0 §7.2.3).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="tagId">The tag identifier.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Answers <c>200</c> with the deleted tag, or <c>204</c> when the request carries <c>Prefer: return=minimal</c> (RFC 7240 §4.2).
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task DeleteTagByProjectAndId(HttpContext context, Guid projectId, Guid tagId, [FromServices] IProjectDataVersioningService versioningService,
            ILogger<TagApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("deleteTagByProjectAndId invoked for project {ProjectId} and tag {TagId}", projectId, tagId);
            }

            if (versioningService == null)
            {
                await NotYetImplemented(context, "deleteTagByProjectAndId");

                return;
            }

            try
            {
                var outcome = await versioningService.DeleteTagAsync(projectId, tagId, context.RequestAborted);

                await DeletedAsync<Tag, SysML2.NET.PIM.DTO.Tag>(context, "deleteTagByProjectAndId", outcome);
            }
            catch (Exception exception)
            {
                await Failure(context, "deleteTagByProjectAndId", exception, logger);
            }
        }
    }
}
