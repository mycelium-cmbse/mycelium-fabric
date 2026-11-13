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
    using System.Linq;

    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.SDK.Serializer.Json;

    /// <summary>
    /// Registers Functional Data access: the JSON serializers and the generated data access objects.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The service collection the registrations are added to.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers, as singletons, the JSON <see cref="ISerializer"/> and <see cref="IDeSerializer"/> injected into the data access objects, and every
            /// <see cref="IDao{TDto}"/> implementation of this assembly under its <see cref="IDao{TDto}"/> interface.
            /// </summary>
            /// <returns>The same service collection, so that registrations chain.</returns>
            /// <exception cref="ArgumentNullException">The service collection is null.</exception>
            public IServiceCollection AddFunctionalDataAccess()
            {
                ArgumentNullException.ThrowIfNull(services);

                services.AddSingleton<ISerializer, Serializer>();
                services.AddSingleton<IDeSerializer, DeSerializer>();

                var daoRegistrations = typeof(IDao<>).Assembly.GetTypes()
                    .Where(type => type is { IsClass: true, IsAbstract: false })
                    .SelectMany(type => type.GetInterfaces().Where(IsDaoInterface).Select(serviceType => (ServiceType: serviceType, ImplementationType: type)));

                foreach (var (serviceType, implementationType) in daoRegistrations)
                {
                    services.AddSingleton(serviceType, implementationType);
                }

                return services;
            }
        }

        /// <summary>
        /// Determines whether a type is a closed <see cref="IDao{TDto}"/> interface.
        /// </summary>
        /// <param name="type">The interface type to test.</param>
        /// <returns><see langword="true"/> when <paramref name="type"/> closes <see cref="IDao{TDto}"/>; otherwise <see langword="false"/>.</returns>
        private static bool IsDaoInterface(Type type)
        {
            return type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(IDao<>);
        }
    }
}
