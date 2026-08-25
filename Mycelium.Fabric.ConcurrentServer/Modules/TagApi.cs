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

    /// <summary>
    /// Hand-coded request handlers for the Tag routes registered by the generated companion partial.
    /// </summary>
    /// <remarks>
    /// Every operation below belongs to the <c>ProjectDataVersioningService</c> — Systems Modeling API and
    /// Services 1.0 §7.2.3 (pp. 42–45) — and is bound to its REST/HTTP endpoint by §8.1.3 (pp. 50–56).
    /// Like a Branch, a Tag is a <c>CommitReference</c> (§7.1.2, pp. 32–36), but it names a fixed commit
    /// through <c>Tag.taggedCommit</c> rather than tracking a moving head.
    /// </remarks>
    public partial class TagApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects/{projectId}/tags</c>, the REST/HTTP binding of
        /// <c>ProjectDataVersioningService::getTags</c> — "Get all the tags in the given project."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project whose tags are requested.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.3 (pp. 42–45). Not yet implemented — answers
        /// <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetTagsByProject(HttpContext context, Guid projectId, ILogger<TagApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getTagsByProject invoked for project {ProjectId}", projectId);
            }
            
            return NotYetImplemented(context, "getTagsByProject");
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/tags</c>, the REST/HTTP binding of
        /// <c>ProjectDataVersioningService::createTag</c> — "Create a new tag with the given name (tagName)
        /// in the given project, and set the taggedCommit of the new tag as the given commit
        /// (taggedCommit)."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the tag is created in.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.3 (pp. 42–45). Not yet implemented — answers
        /// <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task PostTagByProject(HttpContext context, Guid projectId, ILogger<TagApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("postTagByProject invoked for project {ProjectId}", projectId);
            }

            return NotYetImplemented(context, "postTagByProject");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/tags/{tagId}</c>, the REST/HTTP binding of
        /// <c>ProjectDataVersioningService::getTagById</c> — "Get the tag with the given id (tagId) in the
        /// given project."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the tag belongs to.</param>
        /// <param name="tagId">The identifier of the tag that is requested.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// §8.1.3 (pp. 50–56) composes this endpoint into <c>getTaggedCommit</c>: the tag returned here
        /// carries <c>Tag.taggedCommit</c>, which is then resolved through
        /// <c>GET /projects/{projectId}/commits/{commitId}</c>. Not yet implemented — answers <c>500</c>
        /// with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetTagByProjectAndId(HttpContext context, Guid projectId, Guid tagId, ILogger<TagApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getTagByProjectAndId invoked for project {ProjectId} and tag {TagId}", projectId, tagId);
            }

            return NotYetImplemented(context, "getTagByProjectAndId");
        }

        /// <summary>
        /// Handles <c>DELETE /projects/{projectId}/tags/{tagId}</c>, the REST/HTTP binding of
        /// <c>ProjectDataVersioningService::deleteTag</c> — "Delete the tag with the given id (tagId) in
        /// the given project."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the tag belongs to.</param>
        /// <param name="tagId">The identifier of the tag that is deleted.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.3 (pp. 42–45). Not yet implemented — answers
        /// <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task DeleteTagByProjectAndId(HttpContext context, Guid projectId, Guid tagId, ILogger<TagApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("deleteTagByProjectAndId invoked for project {ProjectId} and tag {TagId}", projectId, tagId);
            }

            return NotYetImplemented(context, "deleteTagByProjectAndId");
        }
    }
}
