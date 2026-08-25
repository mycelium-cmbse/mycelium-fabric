// ------------------------------------------------------------------------------------------------
//  <copyright file="TestEndpointRouteBuilder.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A minimal <see cref="IEndpointRouteBuilder"/> that collects the endpoints a Carter module registers,
    /// so that <c>AddRoutes</c> can be exercised without standing up a web host.
    /// </summary>
    internal sealed class TestEndpointRouteBuilder : IEndpointRouteBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestEndpointRouteBuilder"/> class.
        /// </summary>
        internal TestEndpointRouteBuilder()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddRouting();

            this.ServiceProvider = services.BuildServiceProvider();
        }

        /// <inheritdoc />
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

        /// <inheritdoc />
        public IServiceProvider ServiceProvider { get; }

        /// <inheritdoc />
        public IApplicationBuilder CreateApplicationBuilder()
        {
            return new ApplicationBuilder(this.ServiceProvider);
        }

        /// <summary>
        /// Gets every <see cref="RouteEndpoint"/> registered against this builder.
        /// </summary>
        /// <returns>The registered route endpoints.</returns>
        internal IReadOnlyList<RouteEndpoint> RouteEndpoints()
        {
            return this.DataSources
                .SelectMany(dataSource => dataSource.Endpoints)
                .OfType<RouteEndpoint>()
                .ToList();
        }

        /// <summary>
        /// Gets the route template of every registered endpoint, paired with the endpoint name.
        /// </summary>
        /// <returns>A dictionary keyed by endpoint name, carrying the raw route template.</returns>
        internal IReadOnlyDictionary<string, string> TemplatesByEndpointName()
        {
            return this.RouteEndpoints()
                .Where(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>() is not null)
                .ToDictionary(
                    endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>().EndpointName,
                    endpoint => endpoint.RoutePattern.RawText);
        }

        /// <summary>
        /// Gets the status codes declared by the <c>ProducesResponseType</c> attributes of the endpoint
        /// registered under the supplied name.
        /// </summary>
        /// <param name="endpointName">The endpoint name to look up.</param>
        /// <returns>The declared status codes, in ascending order.</returns>
        internal IReadOnlyList<int> DeclaredStatusCodes(string endpointName)
        {
            var endpoint = this.RouteEndpoints()
                .Single(routeEndpoint =>
                    routeEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>() is { } name
                    && name.EndpointName == endpointName);

            return endpoint.Metadata
                .OfType<ProducesResponseTypeAttribute>()
                .Select(attribute => attribute.StatusCode)
                .Order()
                .ToList();
        }
    }
}
