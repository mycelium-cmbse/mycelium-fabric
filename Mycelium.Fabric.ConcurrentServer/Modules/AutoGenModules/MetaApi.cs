// ------------------------------------------------------------------------------------------------
//  <copyright file="MetaApi.cs" company="Starion Group S.A.">
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
    /// Provides the Meta routes that the OMG Systems Modeling API and Services
    /// specification requires the platform to expose.
    /// </summary>
    /// <remarks>
    /// Only the route registration is generated. Each route is mapped onto a hand-written static handler of
    /// the same name declared on the companion partial. A Carter module carries no dependencies of its own:
    /// the handler receives whatever services it needs from dependency injection through minimal-API
    /// parameter binding, and reads query parameters from the request.
    /// </remarks>
    [GeneratedCode("Mycelium.SDK", "latest")]
    public partial class MetaApi : ICarterModule
    {
        /// <summary>
        /// Invoked at startup to add routes to the HTTP pipeline
        /// </summary>
        /// <remarks>Implementations of <see cref="ICarterModule"/> should not inject constructor dependencies. All dependencies should be supplied in the route <see cref="RequestDelegate"/></remarks>
        /// <param name="app">An instance of <see cref="IEndpointRouteBuilder"/></param>
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/meta/datatypes", GetDatatypes)
                .WithName("getDatatypes")
                .WithSummary("Get datatypes")
                .WithTags("Meta");

            app.MapGet("/meta/datatypes/{datatypeId}", GetDatatypeById)
                .WithName("getDatatypeById")
                .WithSummary("Get datatype by ID")
                .WithTags("Meta");
        }
    }
}

// ------------------------------------------------------------------------------------------------
// --------THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!--------
// ------------------------------------------------------------------------------------------------
