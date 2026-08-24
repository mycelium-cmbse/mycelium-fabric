// ------------------------------------------------------------------------------------------------
//  <copyright file="QueryApi.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

// ------------------------------------------------------------------------------------------------
// --------THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!--------
// ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Modules
{
    using System.CodeDom.Compiler;

    using Carter;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// Provides the Query routes that the OMG Systems Modeling API and Services
    /// specification requires the platform to expose.
    /// </summary>
    /// <remarks>
    /// Only the route registration is generated. Each route is mapped onto a hand-written static handler of
    /// the same name declared on the companion partial. A Carter module carries no dependencies of its own:
    /// the handler receives whatever services it needs from dependency injection through minimal-API
    /// parameter binding, and reads query parameters from the request.
    /// </remarks>
    [GeneratedCode("Mycelium.SDK", "latest")]
    public partial class QueryApi : ICarterModule
    {
        /// <inheritdoc />
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/projects/{projectId:guid}/queries", GetQueriesByProject)
                .WithName("getQueriesByProject")
                .WithSummary("Get queries by project")
                .WithTags("Query");

            app.MapPost("/projects/{projectId:guid}/queries", PostQueryByProject)
                .WithName("postQueryByProject")
                .WithSummary("Create query by project")
                .WithTags("Query");

            app.MapGet("/projects/{projectId:guid}/queries/{queryId:guid}", GetQueryByProjectAndId)
                .WithName("getQueryByProjectAndId")
                .WithSummary("Get query by project and ID")
                .WithTags("Query");

            app.MapPut("/projects/{projectId:guid}/queries/{queryId:guid}", PutQueryByProjectAndId)
                .WithName("putQueryByProjectAndId")
                .WithSummary("Update project by project and ID")
                .WithTags("Query");

            app.MapDelete("/projects/{projectId:guid}/queries/{queryId:guid}", DeleteQueryByProjectAndId)
                .WithName("deleteQueryByProjectAndId")
                .WithSummary("Delete query by project and ID")
                .WithTags("Query");

            app.MapGet("/projects/{projectId:guid}/queries/{queryId:guid}/results", GetQueryResultsByProjectIdQueryId)
                .WithName("getQueryResultsByProjectIdQueryId")
                .WithSummary("Get query results by project and query")
                .WithTags("Query");

            app.MapGet("/projects/{projectId:guid}/query-results", GetQueryResultsByProjectIdQuery)
                .WithName("getQueryResultsByProjectIdQuery")
                .WithSummary("Get query results by project and query definition")
                .WithTags("Query");

            app.MapPost("/projects/{projectId:guid}/query-results", GetQueryResultsByProjectIdQueryPost)
                .WithName("getQueryResultsByProjectIdQueryPost")
                .WithSummary("Get query results by project and query definition via POST")
                .WithTags("Query");
        }
    }
}

// ------------------------------------------------------------------------------------------------
// --------THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!--------
// ------------------------------------------------------------------------------------------------
