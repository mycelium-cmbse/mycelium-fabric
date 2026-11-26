// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Persistence
{
    using System;

    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.Common.DependencyInjection;
    using Mycelium.SDK.Serializer.Json;

    /// <summary>
    /// Registers Functional Data access: the JSON serializers, the generated data access objects, their provider, the generic read repository and the
    /// managed-value provider.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The service collection the registrations are added to.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers, as singletons, the stateless JSON <see cref="ISerializer"/> and <see cref="IDeSerializer"/> injected into the data access objects;
            /// registers, as scoped so that no scope shares database access with another, every <see cref="IBaseDao"/> implementation of this assembly under
            /// its <see cref="IDao{TDto}"/> interface, <see cref="DaoProvider"/> under <see cref="IDaoProvider"/>, <see cref="ThingRepository"/> under
            /// <see cref="IThingRepository"/>, and <see cref="ManagedValueProvider"/> under <see cref="IManagedValueProvider"/> and
            /// <see cref="IActingUserSetter"/>, both resolving the same instance of a scope, so that the acting user recorded in a scope is the one its data
            /// access objects read.
            /// </summary>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <exception cref="ArgumentNullException">The service collection is null.</exception>
            /// <exception cref="InvalidOperationException">Two data access objects implement the same <see cref="IDao{TDto}"/> interface.</exception>
            public IServiceCollection AddFunctionalDataAccess()
            {
                ArgumentNullException.ThrowIfNull(services);

                services.AddSingleton<ISerializer, Serializer>();
                services.AddSingleton<IDeSerializer, DeSerializer>();
                services.AddScoped<IDaoProvider, DaoProvider>();
                services.AddScoped<IThingRepository, ThingRepository>();
                services.AddScoped<ManagedValueProvider>();
                services.AddScoped<IManagedValueProvider>(serviceProvider => serviceProvider.GetRequiredService<ManagedValueProvider>());
                services.AddScoped<IActingUserSetter>(serviceProvider => serviceProvider.GetRequiredService<ManagedValueProvider>());

                return services.AddImplementationsOf<IBaseDao>(ServiceLifetime.Scoped);
            }
        }
    }
}
