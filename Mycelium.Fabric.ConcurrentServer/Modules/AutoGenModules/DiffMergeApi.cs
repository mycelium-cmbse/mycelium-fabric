// ------------------------------------------------------------------------------------------------
//  <copyright file="DiffMergeApi.cs" company="Starion Group S.A.">
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
    /// Provides the Diff &amp; Merge routes that the OMG Systems Modeling API and Services
    /// specification requires the platform to expose.
    /// </summary>
    /// <remarks>
    /// Only the route registration is generated. Each route is mapped onto a hand-written static handler of
    /// the same name declared on the companion partial. A Carter module carries no dependencies of its own:
    /// the handler receives whatever services it needs from dependency injection through minimal-API
    /// parameter binding, and reads query parameters from the request.
    /// </remarks>
    [GeneratedCode("Mycelium.SDK", "latest")]
    public partial class DiffMergeApi : ICarterModule
    {
        /// <inheritdoc />
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/projects/{projectId:guid}/branches/{targetBranchId:guid}/merge", Merge)
                .WithName("merge")
                .WithSummary("Merge source commit(s) into a target branch")
                .WithTags("Diff & Merge");

            app.MapGet("/projects/{projectId:guid}/commits/{compareCommitId:guid}/diff", Diff)
                .WithName("diff")
                .WithSummary("Diff a base commit and compare commit")
                .WithTags("Diff & Merge");
        }
    }
}

// ------------------------------------------------------------------------------------------------
// --------THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!--------
// ------------------------------------------------------------------------------------------------
