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

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Npgsql;

    /// <summary>
    /// Registers Functional Data access: the connection source and the generated data access objects.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// The name of the connection string to the Functional Data database.
        /// </summary>
        private const string ConnectionStringName = "functionaldata";

        /// <summary>
        /// Registers, as singletons, the <see cref="NpgsqlDataSource"/> of the Functional Data database and every <see cref="IDao{TDto}"/>
        /// implementation of this assembly under its <see cref="IDao{TDto}"/> interface.
        /// </summary>
        /// <param name="services">The service collection to extend.</param>
        /// <returns>The same <paramref name="services"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public static IServiceCollection AddFunctionalDataAccess(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton(CreateDataSource);

            var daoRegistrations = typeof(IDao<>).Assembly.GetTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .SelectMany(type => type.GetInterfaces().Where(IsDaoInterface).Select(serviceType => (ServiceType: serviceType, ImplementationType: type)));

            foreach (var (serviceType, implementationType) in daoRegistrations)
            {
                services.AddSingleton(serviceType, implementationType);
            }

            return services;
        }

        /// <summary>
        /// Creates the <see cref="NpgsqlDataSource"/> of the Functional Data database from the configured connection string, without connecting.
        /// </summary>
        /// <param name="provider">The service provider supplying the configuration.</param>
        /// <returns>The data source that opens connections to the Functional Data database.</returns>
        /// <exception cref="InvalidOperationException">The connection string is missing or blank.</exception>
        private static NpgsqlDataSource CreateDataSource(IServiceProvider provider)
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"The '{ConnectionStringName}' connection string is not configured.");
            }

            return NpgsqlDataSource.Create(connectionString);
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
