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

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Serialization;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.Common;
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Handlers of the Query routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>QueryService</c> operations (Systems Modeling API and Services 1.0 §7.2.4) to their §8.1.3 endpoints, which also realize the ExternalRelationship and ProjectUsage reads.
    /// </remarks>
    public partial class QueryApi : ApiBase
    {
        /// <summary>
        /// The query parameter naming the commit a query is evaluated at.
        /// </summary>
        private const string CommitIdParameter = "commitId";

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/queries</c>: <c>QueryService::getQueries</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetQueriesByProject(HttpContext context, Guid projectId, [FromServices] IQueryService queryService, ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getQueriesByProject invoked for project {ProjectId}", projectId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "getQueriesByProject");

                return;
            }

            try
            {
                await PagedAsync<Query, SysML2.NET.PIM.DTO.Query>(context, "getQueriesByProject",
                    queryParameters => queryService.GetQueriesAsync(projectId, queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, "getQueriesByProject", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/queries</c>: <c>QueryService::createQuery</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="body">The <c>QueryRequest</c> from the request body.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// <c>scope</c> and <c>orderBy</c> are passed empty, since <c>QueryRequest</c> carries neither.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task PostQueryByProject(HttpContext context, Guid projectId, RequestBody<QueryRequest> body, [FromServices] IQueryService queryService,
            ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("postQueryByProject invoked for project {ProjectId}", projectId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "postQueryByProject");

                return;
            }

            try
            {
                if (body.Content.IsError)
                {
                    await Failed(context, "postQueryByProject", body.Content.Errors);

                    return;
                }

                var query = body.Content.Value.ToPim();

                var outcome = await queryService.CreateQueryAsync(query.Name, projectId, query.Select, [], query.Where, [], context.RequestAborted);

                await WriteAsync<Query, SysML2.NET.PIM.DTO.Query>(context, "postQueryByProject", outcome, StatusCodes.Status201Created);
            }
            catch (Exception exception)
            {
                await Failure(context, "postQueryByProject", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/queries/{queryId}</c>: <c>QueryService::getQueryById</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="queryId">The query identifier.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetQueryByProjectAndId(HttpContext context, Guid projectId, Guid queryId, [FromServices] IQueryService queryService, ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getQueryByProjectAndId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "getQueryByProjectAndId");

                return;
            }

            try
            {
                var outcome = await queryService.GetQueryByIdAsync(projectId, queryId, context.RequestAborted);

                await WriteAsync<Query, SysML2.NET.PIM.DTO.Query>(context, "getQueryByProjectAndId", outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, "getQueryByProjectAndId", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>PUT /projects/{projectId}/queries/{queryId}</c>: <c>QueryService::updateQuery</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="queryId">The query identifier.</param>
        /// <param name="body">The <c>QueryRequest</c> from the request body.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// The route's <c>queryId</c> identifies the record the body replaces.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task PutQueryByProjectAndId(HttpContext context, Guid projectId, Guid queryId, RequestBody<QueryRequest> body,
            [FromServices] IQueryService queryService, ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("putQueryByProjectAndId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "putQueryByProjectAndId");

                return;
            }

            try
            {
                if (body.Content.IsError)
                {
                    await Failed(context, "putQueryByProjectAndId", body.Content.Errors);

                    return;
                }

                var query = body.Content.Value.ToPim();
                query.Id = queryId;
                query.Project = projectId;

                var outcome = await queryService.UpdateQueryAsync(projectId, query, context.RequestAborted);

                await WriteAsync<Query, SysML2.NET.PIM.DTO.Query>(context, "putQueryByProjectAndId", outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, "putQueryByProjectAndId", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>DELETE /projects/{projectId}/queries/{queryId}</c>: <c>QueryService::deleteQuery</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="queryId">The query identifier.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Answers <c>200</c> with the deleted query, or <c>204</c> when the request carries <c>Prefer: return=minimal</c> (RFC 7240 §4.2).
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task DeleteQueryByProjectAndId(HttpContext context, Guid projectId, Guid queryId, [FromServices] IQueryService queryService, ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("deleteQueryByProjectAndId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "deleteQueryByProjectAndId");

                return;
            }

            try
            {
                var outcome = await queryService.DeleteQueryAsync(projectId, queryId, context.RequestAborted);

                await DeletedAsync<Query, SysML2.NET.PIM.DTO.Query>(context, "deleteQueryByProjectAndId", outcome);
            }
            catch (Exception exception)
            {
                await Failure(context, "deleteQueryByProjectAndId", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/queries/{queryId}/results</c>: <c>QueryService::executeQueryById</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="queryId">The query identifier.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// The optional <c>commitId</c> query parameter selects the commit; the service defaults to the head of the default branch. The query must belong to the project,
        /// since <c>executeQueryById</c> takes no project.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetQueryResultsByProjectIdQueryId(HttpContext context, Guid projectId, Guid queryId, [FromServices] IQueryService queryService,
            ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getQueryResultsByProjectIdQueryId invoked for project {ProjectId} and query {QueryId}", projectId, queryId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "getQueryResultsByProjectIdQueryId");

                return;
            }

            try
            {
                var commitId = context.Request.ReadOptionalId(CommitIdParameter);

                if (commitId.IsError)
                {
                    await Failed(context, "getQueryResultsByProjectIdQueryId", commitId.Errors);

                    return;
                }

                var query = await queryService.GetQueryByIdAsync(projectId, queryId, context.RequestAborted);

                if (query.IsError)
                {
                    await Failed(context, "getQueryResultsByProjectIdQueryId", query.Errors);

                    return;
                }

                await PagedAsync(context, "getQueryResultsByProjectIdQueryId",
                    queryParameters => queryService.ExecuteQueryByIdAsync(queryId, commitId.Value, queryParameters, context.RequestAborted), ToResponse);
            }
            catch (Exception exception)
            {
                await Failure(context, "getQueryResultsByProjectIdQueryId", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/query-results</c>: <c>QueryService::executeQuery</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="body">The <c>QueryRequest</c> from the request body.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Executes the query in the body without persisting it.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetQueryResultsByProjectIdQuery(HttpContext context, Guid projectId, RequestBody<QueryRequest> body, [FromServices] IQueryService queryService,
            ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getQueryResultsByProjectIdQuery invoked for project {ProjectId}", projectId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "getQueryResultsByProjectIdQuery");

                return;
            }

            try
            {
                await ExecuteSuppliedQueryAsync(context, "getQueryResultsByProjectIdQuery", body, projectId, queryService);
            }
            catch (Exception exception)
            {
                await Failure(context, "getQueryResultsByProjectIdQuery", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>POST /projects/{projectId}/query-results</c>: <c>QueryService::executeQuery</c> (Systems Modeling API and Services 1.0 §7.2.4).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="body">The <c>QueryRequest</c> from the request body.</param>
        /// <param name="queryService">The <see cref="IQueryService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// The POST form of the query execution, for clients that cannot send a GET body (§8.1.3).
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetQueryResultsByProjectIdQueryPost(HttpContext context, Guid projectId, RequestBody<QueryRequest> body, [FromServices] IQueryService queryService,
            ILogger<QueryApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getQueryResultsByProjectIdQueryPost invoked for project {ProjectId}", projectId);
            }

            if (queryService == null)
            {
                await NotYetImplemented(context, "getQueryResultsByProjectIdQueryPost");

                return;
            }

            try
            {
                await ExecuteSuppliedQueryAsync(context, "getQueryResultsByProjectIdQueryPost", body, projectId, queryService);
            }
            catch (Exception exception)
            {
                await Failure(context, "getQueryResultsByProjectIdQueryPost", exception, logger);
            }
        }

        /// <summary>
        /// Executes the query of the request body and writes the selected page of Data.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="operationId">The operation id of the route.</param>
        /// <param name="body">The <c>QueryRequest</c> from the request body.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="service">The query service.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        private static async Task ExecuteSuppliedQueryAsync(HttpContext context, string operationId, RequestBody<QueryRequest> body, Guid projectId, IQueryService service)
        {
            var commitId = context.Request.ReadOptionalId(CommitIdParameter);

            if (commitId.IsError)
            {
                await Failed(context, operationId, commitId.Errors);

                return;
            }

            if (body.Content.IsError)
            {
                await Failed(context, operationId, body.Content.Errors);

                return;
            }

            var query = body.Content.Value.ToPim();
            query.Project = projectId;

            await PagedAsync(context, operationId, queryParameters => service.ExecuteQueryAsync(query, commitId.Value, queryParameters, context.RequestAborted), ToResponse);
        }

        /// <summary>
        /// Maps a query result onto its PSM response, leaving SysML Core elements as is.
        /// </summary>
        /// <param name="data">The Data the query returned.</param>
        /// <returns>The model written for <paramref name="data"/>.</returns>
        private static object ToResponse(IData data)
        {
            return data switch
            {
                SysML2.NET.PIM.DTO.ExternalRelationship externalRelationship => PimMapper.Map<ExternalRelationship, SysML2.NET.PIM.DTO.ExternalRelationship>(externalRelationship),
                SysML2.NET.PIM.DTO.ExternalData externalData => PimMapper.Map<ExternalData, SysML2.NET.PIM.DTO.ExternalData>(externalData),
                SysML2.NET.PIM.DTO.ProjectUsage projectUsage => PimMapper.Map<ProjectUsage, SysML2.NET.PIM.DTO.ProjectUsage>(projectUsage),
                _ => data
            };
        }
    }
}
