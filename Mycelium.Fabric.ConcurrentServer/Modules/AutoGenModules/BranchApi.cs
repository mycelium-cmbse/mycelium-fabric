// ------------------------------------------------------------------------------------------------
//  <copyright file="BranchApi.cs" company="Starion Group S.A.">
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
    /// Provides the Branch routes that the OMG Systems Modeling API and Services
    /// specification requires the platform to expose.
    /// </summary>
    /// <remarks>
    /// Only the route registration is generated. Each route is mapped onto a hand-written static handler of
    /// the same name declared on the companion partial. A Carter module carries no dependencies of its own:
    /// the handler receives whatever services it needs from dependency injection through minimal-API
    /// parameter binding, and reads query parameters from the request.
    /// </remarks>
    [GeneratedCode("Mycelium.SDK", "latest")]
    public partial class BranchApi : ICarterModule
    {
        /// <summary>
        /// Invoked at startup to add routes to the HTTP pipeline
        /// </summary>
        /// <remarks>Implementations of <see cref="ICarterModule"/> should not inject constructor dependencies. All dependencies should be supplied in the route <see cref="RequestDelegate"/></remarks>
        /// <param name="app">An instance of <see cref="IEndpointRouteBuilder"/></param>
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/projects/{projectId:guid}/branches", GetBranchesByProject)
                .WithName("getBranchesByProject")
                .WithSummary("Get branches by project")
                .WithTags("Branch");

            app.MapPost("/projects/{projectId:guid}/branches", PostBranchByProject)
                .WithName("postBranchByProject")
                .WithSummary("Create branch by project")
                .WithTags("Branch");

            app.MapGet("/projects/{projectId:guid}/branches/{branchId:guid}", GetBranchesByProjectAndId)
                .WithName("getBranchesByProjectAndId")
                .WithSummary("Get branch by project and ID")
                .WithTags("Branch");

            app.MapDelete("/projects/{projectId:guid}/branches/{branchId:guid}", DeleteBranchByProjectAndId)
                .WithName("deleteBranchByProjectAndId")
                .WithSummary("Delete branch by project and ID")
                .WithTags("Branch");
        }
    }
}

// ------------------------------------------------------------------------------------------------
// --------THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!--------
// ------------------------------------------------------------------------------------------------
