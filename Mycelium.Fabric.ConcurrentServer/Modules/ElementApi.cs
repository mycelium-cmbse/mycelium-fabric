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

    /// <summary>
    /// Hand-coded request handlers for the Element routes registered by the generated companion partial.
    /// </summary>
    /// <remarks>
    /// The navigation operations below belong to the <c>ElementNavigationService</c> — Systems Modeling API
    /// and Services 1.0 §7.2.2 (p. 41) — and are bound to their REST/HTTP endpoints by §8.1.3 (pp. 50–56).
    /// As §7.2.2 puts it, "Element is the root metaclass in the KerML abstract syntax [KerML]. Relationship
    /// is a subtype of Element. Both Element and Relationship realize the Data interface defined in the API
    /// Model (refer to 7.1.2 - Project Data Versioning)." Every operation is scoped to a project *and* a
    /// commit, so reads are always against an immutable snapshot of the model.
    /// </remarks>
    public partial class ElementApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/elements</c>, the REST/HTTP binding of
        /// <c>ElementNavigationService::getElements</c> — "Get all the elements in a given project at the
        /// given commit."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the elements belong to.</param>
        /// <param name="commitId">The identifier of the commit the elements are read at.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.2 (p. 41). §8.1.3 (pp. 50–56) also describes the
        /// pagination strategy that applies to collection endpoints such as this one. Not yet implemented —
        /// answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetElementsByProjectCommit(HttpContext context, Guid projectId, Guid commitId, ILogger<ElementApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getElementsByProjectCommit invoked for project {ProjectId} and commit {CommitId}", projectId, commitId);
            }

            return NotYetImplemented(context, "getElementsByProjectCommit");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/elements/{elementId}</c>, the REST/HTTP
        /// binding of <c>ElementNavigationService::getElementById</c> — "Get element with the given id
        /// (elementId) in the given project at the given commit."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the element belongs to.</param>
        /// <param name="commitId">The identifier of the commit the element is read at.</param>
        /// <param name="elementId">The identifier of the element that is requested.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.2 (p. 41). Not yet implemented — answers <c>500</c>
        /// with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetElementByProjectCommitId(HttpContext context, Guid projectId, Guid commitId, Guid elementId, ILogger<ElementApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getElementByProjectCommitId invoked for project {ProjectId}, commit {CommitId} and element {ElementId}", projectId, commitId, elementId);
            }

            return NotYetImplemented(context, "getElementByProjectCommitId");
        }

        /// <summary>
        /// Handles
        /// <c>GET /projects/{projectId}/commits/{commitId}/elements/{elementId}/projectUsage</c>, returning
        /// the project usage associated with the given element at the given commit.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the element belongs to.</param>
        /// <param name="commitId">The identifier of the commit the element is read at.</param>
        /// <param name="elementId">The identifier of the element whose project usage is requested.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// <b>This endpoint has no direct counterpart in the specification.</b> The mapping table in
        /// §8.1.3 (pp. 50–56) does not list it, and the <c>ProjectUsageService</c> — §7.2.6 (p. 47) — is
        /// instead realized through the Query service: <c>getProjectUsages</c> ("Get all the project usages
        /// in the given project at the given commit") is mapped onto a
        /// <c>POST /projects/{projectId}/queries</c> whose <c>Query.where</c> is a
        /// <c>PrimitiveConstraint</c> with <c>property = @type</c>, <c>value = 'ProjectUsage'</c> and
        /// <c>operator = '='</c>, executed via
        /// <c>GET /projects/{projectId}/queries/{queryId}/results?commitId={commitId}</c>. The route is
        /// therefore a convenience extension over the standard surface rather than a conformance
        /// requirement; §7.1.2 (pp. 32–36) defines <c>ProjectUsage</c> as "a subclass of Record that
        /// represents the use of a Project in the context of another Project". Confirm the intended
        /// semantics before implementing.
        /// <para>
        /// Not yet implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </para>
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetProjectUsageByProjectCommitElement(HttpContext context, Guid projectId, Guid commitId, Guid elementId, ILogger<ElementApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getProjectUsageByProjectCommitElement invoked for project {ProjectId}, commit {CommitId} and element {ElementId}", projectId, commitId, elementId);
            }

            return NotYetImplemented(context, "getProjectUsageByProjectCommitElement");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{commitId}/roots</c>, the REST/HTTP binding of
        /// <c>ElementNavigationService::getRootElements</c> — "Get all the root elements in the given
        /// project at the given commit."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the root elements belong to.</param>
        /// <param name="commitId">The identifier of the commit the root elements are read at.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.2 (p. 41). Not yet implemented — answers <c>500</c>
        /// with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetRootsByProjectCommit(HttpContext context, Guid projectId, Guid commitId, ILogger<ElementApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getRootsByProjectCommit invoked for project {ProjectId} and commit {CommitId}", projectId, commitId);
            }

            return NotYetImplemented(context, "getRootsByProjectCommit");
        }
    }
}
