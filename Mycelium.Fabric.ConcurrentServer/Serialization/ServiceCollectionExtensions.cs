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
    /// Registers the services of the concurrent server with the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The service collection the registrations are added to.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers the SQL <see cref="IDataStoreTransactionFactory"/>, and per request the <see cref="IDatabaseHandler"/>, the
            /// <see cref="IElementDao{TElement}"/> of every metaclass and the generated <see cref="IElementDaoProvider"/> that resolves them.
            /// </summary>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <exception cref="ArgumentNullException">The service collection is <c>null</c>.</exception>
            /// <remarks>
            /// The <c>DbDataSource</c> of the SysML v2 database and the SysML2.NET JSON serializers are registered elsewhere.
            /// </remarks>
            public IServiceCollection AddCoreDataAccess()
            {
                ArgumentNullException.ThrowIfNull(services);

                services.AddSingleton<IDataStoreTransactionFactory, SqlTransactionFactory>();
                services.AddScoped<IDatabaseHandler, DatabaseHandler>();
                services.AddImplementationsOf<IElementDao>();
                services.AddScoped<IElementDaoProvider, ElementDaoProvider>();

                return services;
            }

            /// <summary>
            /// Registers the <see cref="IChangeExpander"/> and <see cref="IChangeValidator"/>, the <see cref="ICoreDataServiceProvider"/> they
            /// dispatch through, the <see cref="ICoreDataService{TElement}"/> of every metaclass, and every <see cref="IElementEffect"/> and <see cref="IElementRule"/>.
            /// </summary>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <exception cref="ArgumentNullException">The service collection is <c>null</c>.</exception>
            /// <remarks>
            /// Every <see cref="IElementEffect"/> and <see cref="IElementRule"/> of this assembly is found by scan and registered as a singleton, like
            /// the services that hold it.
            /// </remarks>
            public IServiceCollection AddCoreDataServices()
            {
                ArgumentNullException.ThrowIfNull(services);

                services.AddSingleton(typeof(ICoreDataService<>), typeof(CoreDataService<>));
                services.AddEnumerableImplementationsOf<IElementEffect>(ServiceLifetime.Singleton);
                services.AddEnumerableImplementationsOf<IElementRule>(ServiceLifetime.Singleton);
                services.AddSingleton<ICoreDataServiceProvider, CoreDataServiceProvider>();
                services.AddSingleton<IChangeExpander, ChangeExpander>();
                services.AddSingleton<IChangeValidator, ChangeValidator>();

                return services;
            }

            /// <summary>
            /// Registers the Carter modules, the SysML2.NET JSON serializers with the request body readers built on them,
            /// <see cref="JsonResponseNegotiator"/> as the only response negotiator, and the per-request <see cref="IProjectScope"/>.
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
                services.AddScoped<IProjectScope, ProjectScope>();

                services.AddCarter();
                services.RemoveAll<IResponseNegotiator>();
                services.AddSingleton<IResponseNegotiator, JsonResponseNegotiator>();

                return services;
            }
        }
    }
}
