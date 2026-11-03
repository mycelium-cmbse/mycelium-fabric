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
            /// Registers the Carter modules, the SysML2.NET JSON serializers with the request body readers built on them, and
            /// <see cref="JsonResponseNegotiator"/> as the only response negotiator.
            /// </summary>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <remarks>
            /// The negotiators Carter registers itself are removed, since its default one writes any <c>*json</c> media type without the SysML2.NET serializers.
            /// </remarks>
            public IServiceCollection AddRequiredServices()
            {
                ArgumentNullException.ThrowIfNull(services);

                services.AddSingleton<ISerializer, Serializer>();
                services.AddSingleton<IDeSerializer, DeSerializer>();
                services.AddSingleton<IRequestBodyDeserializer, JsonRequestBodyDeserializer>();
                services.AddSingleton<IRequestBodyReader, RequestBodyReader>();

                services.AddCarter();
                services.RemoveAll<IResponseNegotiator>();
                services.AddSingleton<IResponseNegotiator, JsonResponseNegotiator>();

                return services;
            }
        }
    }
}
