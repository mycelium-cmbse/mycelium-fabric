// ------------------------------------------------------------------------------------------------
//  <copyright file="CommitApi.cs" company="Starion Group S.A.">
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
    /// Provides the Commit routes that the OMG Systems Modeling API and Services
    /// specification requires the platform to expose.
    /// </summary>
    /// <remarks>
    /// Only the route registration is generated. Each route is mapped onto a hand-written static handler of
    /// the same name declared on the companion partial. A Carter module carries no dependencies of its own:
    /// the handler receives whatever services it needs from dependency injection through minimal-API
    /// parameter binding, and reads query parameters from the request.
    /// </remarks>
    [GeneratedCode("Mycelium.SDK", "latest")]
    public partial class CommitApi : ICarterModule
    {
        /// <inheritdoc />
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/projects/{projectId:guid}/commits", GetCommitsByProject)
                .WithName("getCommitsByProject")
                .WithSummary("Get commits by project")
                .WithTags("Commit");

            app.MapPost("/projects/{projectId:guid}/commits", PostCommitByProject)
                .WithName("postCommitByProject")
                .WithSummary("Create commit by project")
                .WithTags("Commit");

            app.MapGet("/projects/{projectId:guid}/commits/{commitId:guid}", GetCommitByProjectAndId)
                .WithName("getCommitByProjectAndId")
                .WithSummary("Get commit by project and ID")
                .WithTags("Commit");

            app.MapGet("/projects/{projectId:guid}/commits/{commitId:guid}/changes", GetChangesByProjectCommit)
                .WithName("getChangesByProjectCommit")
                .WithSummary("Get changes by project and commit")
                .WithTags("Commit");

            app.MapGet("/projects/{projectId:guid}/commits/{commitId:guid}/changes/{changeId:guid}", GetChangeByProjectCommitId)
                .WithName("getChangeByProjectCommitId")
                .WithSummary("Get change by project, commit and ID")
                .WithTags("Commit");
        }
    }
}

// ------------------------------------------------------------------------------------------------
// --------THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!--------
// ------------------------------------------------------------------------------------------------
