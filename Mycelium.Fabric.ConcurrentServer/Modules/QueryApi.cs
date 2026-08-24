// ------------------------------------------------------------------------------------------------
//  <copyright file="QueryApi.cs" company="Starion Group S.A.">
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
    /// Hand-coded request handlers for the Query routes registered by the generated companion partial.
    /// </summary>
    /// <remarks>
    /// Every operation below belongs to the <c>QueryService</c> — Systems Modeling API and Services 1.0
    /// §7.2.4 (p. 46) — and is bound to its REST/HTTP endpoint by §8.1.3 (pp. 50–56). §7.1.4 (pp. 38–39)
    /// defines the <c>Query</c> record itself along with the <c>Constraint</c> hierarchy
    /// (<c>PrimitiveConstraint</c>, <c>CompositeConstraint</c>) that <c>Query.where</c> is built from.
    /// <para>
    /// This service carries more weight than its size suggests: §8.1.3 realizes the whole of the
    /// <c>ExternalRelationshipService</c> (§7.2.5, pp. 46–47) and the read side of the
    /// <c>ProjectUsageService</c> (§7.2.6, p. 47) by creating a query whose <c>where</c> filters on
    /// <c>@type</c> and then executing it, rather than by dedicated endpoints.
    /// </para>
    /// </remarks>
    public partial class QueryApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects/{projectId}/queries</c>, the REST/HTTP binding of
        /// <c>QueryService::getQueries</c> — "Get all the queries in the given project."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project whose queries are requested.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.4 (p. 46). Not yet implemented — answers <c>500</c>
        /// with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetQueriesByProject(HttpContext context, Guid projectId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("getQueriesByProject invoked for project {ProjectId}", projectId);

            return NotYetImplemented(context, "getQueriesByProject");
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/queries</c>, the REST/HTTP binding of
        /// <c>QueryService::createQuery</c> — "Create a query in the given project with the given inputs."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the query is created in.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// The request body is a <c>QueryRequest</c>. Per §7.2.4 (p. 46) the operation accepts a
        /// <c>name</c>, the owning <c>project</c>, and the <c>select</c>, <c>scope</c>, <c>where</c> and
        /// <c>orderBy</c> members of the resulting <c>Query</c>. §8.1.3 (pp. 50–56) builds the
        /// ExternalRelationship and ProjectUsage lookups on top of this endpoint. Not yet implemented —
        /// answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task PostQueryByProject(HttpContext context, Guid projectId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("postQueryByProject invoked for project {ProjectId}", projectId);

            return NotYetImplemented(context, "postQueryByProject");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/queries/{queryId}</c>, the REST/HTTP binding of
        /// <c>QueryService::getQueryById</c> — "Get the query with the given id (queryId) in the given
        /// project."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the query belongs to.</param>
        /// <param name="queryId">The identifier of the query that is requested.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.4 (p. 46). Not yet implemented — answers <c>500</c>
        /// with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetQueryByProjectAndId(HttpContext context, Guid projectId, Guid queryId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("getQueryByProjectAndId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);

            return NotYetImplemented(context, "getQueryByProjectAndId");
        }

        /// <summary>
        /// Handles <c>PUT /projects/{projectId}/queries/{queryId}</c>, the REST/HTTP binding of
        /// <c>QueryService::updateQuery</c> — "Update the given query (updateQuery) in the given project."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the query belongs to.</param>
        /// <param name="queryId">The identifier of the query that is updated.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.4 (p. 46). Note that the generated route registration
        /// carries the summary "Update project by project and ID"; the operation this endpoint binds to is
        /// <c>updateQuery</c>, not <c>updateProject</c>. Not yet implemented — answers <c>500</c> with an
        /// RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task PutQueryByProjectAndId(HttpContext context, Guid projectId, Guid queryId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("putQueryByProjectAndId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);

            return NotYetImplemented(context, "putQueryByProjectAndId");
        }

        /// <summary>
        /// Handles <c>DELETE /projects/{projectId}/queries/{queryId}</c>, the REST/HTTP binding of
        /// <c>QueryService::deleteQuery</c> — "Delete the query with the given id (queryId) in the given
        /// project."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the query belongs to.</param>
        /// <param name="queryId">The identifier of the query that is deleted.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.4 (p. 46). Not yet implemented — answers <c>500</c>
        /// with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task DeleteQueryByProjectAndId(HttpContext context, Guid projectId, Guid queryId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("deleteQueryByProjectAndId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);

            return NotYetImplemented(context, "deleteQueryByProjectAndId");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/queries/{queryId}/results</c>, the REST/HTTP binding of
        /// <c>QueryService::executeQueryById</c> — "Execute the query with the given id in the owning
        /// project (Query.project) at the given commit. If the commit is not specified, then the head
        /// commit of the default branch of the project will be used."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the query belongs to.</param>
        /// <param name="queryId">The identifier of the query that is executed.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// The commit to evaluate against is carried by the optional <c>commitId</c> query parameter —
        /// §8.1.3 (pp. 50–56) uses exactly this endpoint, in the form <c>?commitId={commitId}</c>, to
        /// realize the ExternalRelationship and ProjectUsage read operations. Not yet implemented —
        /// answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetQueryResultsByProjectIdQueryId(HttpContext context, Guid projectId, Guid queryId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("getQueryResultsByProjectIdQueryId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);

            return NotYetImplemented(context, "getQueryResultsByProjectIdQueryId");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/query-results</c>, the REST/HTTP binding of
        /// <c>QueryService::executeQuery</c> — "Execute the given query in the owning project
        /// (Query.project) at the given commit. If the commit is not specified, then the head commit of the
        /// default branch of the project will be used."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the query is executed against.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// This executes a query supplied in the request rather than one previously persisted, so no
        /// <c>Query</c> record is created. §8.1.3 (pp. 50–56) maps <c>executeQuery</c> onto both this
        /// endpoint and its POST counterpart, and states that "either the GET or the POST endpoint may be
        /// used". Not yet implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetQueryResultsByProjectIdQuery(HttpContext context, Guid projectId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("getQueryResultsByProjectIdQuery invoked for project {ProjectId}", projectId);

            return NotYetImplemented(context, "getQueryResultsByProjectIdQuery");
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/query-results</c>, the POST binding of
        /// <c>QueryService::executeQuery</c>.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the query is executed against.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Semantically identical to the GET form above. §8.1.3 (pp. 50–56) gives the reason both exist:
        /// "The POST endpoint is provided for compatibility with clients that don't support GET requests
        /// with a body." Not yet implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetQueryResultsByProjectIdQueryPost(HttpContext context, Guid projectId, ILogger<QueryApi> logger)
        {
            logger.LogInformation("getQueryResultsByProjectIdQueryPost invoked for project {ProjectId}", projectId);

            return NotYetImplemented(context, "getQueryResultsByProjectIdQueryPost");
        }
    }
}
