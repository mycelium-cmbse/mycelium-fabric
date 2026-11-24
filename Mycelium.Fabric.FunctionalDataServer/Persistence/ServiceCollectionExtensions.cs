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
    /// Registers Functional Data access: the JSON serializers, the generated data access objects, their provider and the generic read repository.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The service collection the registrations are added to.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers, as singletons, the JSON <see cref="ISerializer"/> and <see cref="IDeSerializer"/> injected into the data access objects, and every
            /// <see cref="IBaseDao"/> implementation of this assembly under its <see cref="IDao{TDto}"/> interface, <see cref="DaoProvider"/> under
            /// <see cref="IDaoProvider"/> and <see cref="ThingRepository"/> under <see cref="IThingRepository"/>.
            /// </summary>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <exception cref="ArgumentNullException">The service collection is null.</exception>
            /// <exception cref="InvalidOperationException">Two data access objects implement the same <see cref="IDao{TDto}"/> interface.</exception>
            public IServiceCollection AddFunctionalDataAccess()
            {
                ArgumentNullException.ThrowIfNull(services);

                services.AddSingleton<ISerializer, Serializer>();
                services.AddSingleton<IDeSerializer, DeSerializer>();
                services.AddSingleton<IDaoProvider, DaoProvider>();
                services.AddSingleton<IThingRepository, ThingRepository>();

                return services.AddImplementationsOf<IBaseDao>(ServiceLifetime.Singleton);
            }
        }
    }
}
