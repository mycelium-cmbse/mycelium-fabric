// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Serialization
{
    using System;

    using Carter;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    using Mycelium.Fabric.Common.DependencyInjection;
    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Registers the services the REST/HTTP PSM endpoints require with the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The service collection the registrations are added to.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers the Carter modules, the SysML2.NET JSON serializers with the request body readers built on them,
            /// <see cref="JsonResponseNegotiator"/> as the only response negotiator, the per-request <see cref="IProjectScope"/> and
            /// <see cref="IDatabaseHandler"/>, and every PIM service under its own interface.
            /// </summary>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <remarks>
            /// The negotiators Carter registers itself are removed, since its default one writes any <c>*json</c> media type without the SysML2.NET serializers.
            /// The services and the handler are scoped, since they share the transaction and the project scope of the request.
            /// </remarks>
            public IServiceCollection AddRequiredServices()
            {
                ArgumentNullException.ThrowIfNull(services);

                services.AddSingleton<ISerializer, Serializer>();
                services.AddSingleton<IDeSerializer, DeSerializer>();
                services.AddSingleton<IRequestBodyDeserializer, JsonRequestBodyDeserializer>();
                services.AddSingleton<IRequestBodyReader, RequestBodyReader>();
                services.AddScoped<IProjectScope, ProjectScope>();
                services.AddScoped<IDatabaseHandler, DatabaseHandler>();
                services.AddImplementationsOf<IBaseService>();

                services.AddCarter();
                services.RemoveAll<IResponseNegotiator>();
                services.AddSingleton<IResponseNegotiator, JsonResponseNegotiator>();

                return services;
            }
        }
    }
}
