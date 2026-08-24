// ------------------------------------------------------------------------------------------------
//  <copyright file="TagApi.cs" company="Starion Group S.A.">
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
    /// Provides the Tag routes that the OMG Systems Modeling API and Services
    /// specification requires the platform to expose.
    /// </summary>
    /// <remarks>
    /// Only the route registration is generated. Each route is mapped onto a hand-written static handler of
    /// the same name declared on the companion partial. A Carter module carries no dependencies of its own:
    /// the handler receives whatever services it needs from dependency injection through minimal-API
    /// parameter binding, and reads query parameters from the request.
    /// </remarks>
    [GeneratedCode("Mycelium.SDK", "latest")]
    public partial class TagApi : ICarterModule
    {
        /// <inheritdoc />
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/projects/{projectId:guid}/tags", GetTagsByProject)
                .WithName("getTagsByProject")
                .WithSummary("Get tags by project")
                .WithTags("Tag");

            app.MapPost("/projects/{projectId:guid}/tags", PostTagByProject)
                .WithName("postTagByProject")
                .WithSummary("Create tag by project")
                .WithTags("Tag");

            app.MapGet("/projects/{projectId:guid}/tags/{tagId:guid}", GetTagByProjectAndId)
                .WithName("getTagByProjectAndId")
                .WithSummary("Get tag by project and ID")
                .WithTags("Tag");

            app.MapDelete("/projects/{projectId:guid}/tags/{tagId:guid}", DeleteTagByProjectAndId)
                .WithName("deleteTagByProjectAndId")
                .WithSummary("Delete tag by project and ID")
                .WithTags("Tag");
        }
    }
}

// ------------------------------------------------------------------------------------------------
// --------THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!--------
// ------------------------------------------------------------------------------------------------
