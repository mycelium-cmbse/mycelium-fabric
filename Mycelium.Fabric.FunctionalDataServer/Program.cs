// ------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

    using Npgsql;

    /// <summary>
    /// Hosts the Functional Data microservice and its awaited database initialization.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        /// <summary>
        /// Builds the host, initializes the Functional Data database and runs the host until shutdown.
        /// </summary>
        /// <param name="args">The command-line configuration arguments.</param>
        /// <returns>A task that completes when the host stops.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The connection string is missing, blank or malformed, or host construction or database initialization fails.</exception>
        /// <exception cref="OperationCanceledException">Startup or execution is canceled.</exception>
        public static async Task Main(string[] args)
        {
            using var host = CreateHost(args);

            await host.RunAsync();
        }

        /// <summary>
        /// Builds, without starting it, the host with the data source, the awaited database initialization and the Functional Data access registrations,
        /// then resolves the data source from a scope of the built host so that a rejected connection string is logged and stops startup.
        /// </summary>
        /// <param name="args">The command-line configuration arguments.</param>
        /// <returns>The built host.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The connection string is missing, blank or malformed, or host construction fails.</exception>
        internal static IHost CreateHost(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton(serviceProvider => CreateDataSource(serviceProvider.GetRequiredService<IConfiguration>()));
            builder.Services.AddSingleton<IFunctionalDataInitializer, FunctionalDataInitializer>();
            builder.Services.AddHostedService<FunctionalDataInitializationService>();
            builder.Services.AddFunctionalDataAccess();

            var host = builder.Build();

            using (var scope = host.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program));

                try
                {
                    scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogError(exception, "The Functional Data server cannot start because its connection string was rejected.");
                    host.Dispose();
                    throw;
                }
            }

            return host;
        }

        /// <summary>
        /// Creates the data source of the Functional Data database from the connection string in the host's configuration.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <returns>The data source, which the host's container owns and disposes with the host.</returns>
        /// <exception cref="InvalidOperationException">
        /// The connection string is missing or blank, or it is malformed, in which case the Npgsql exception is the inner exception.
        /// </exception>
        private static NpgsqlDataSource CreateDataSource(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(FunctionalDataInitializer.ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"The '{FunctionalDataInitializer.ConnectionStringName}' connection string is required.");
            }

            try
            {
                return NpgsqlDataSource.Create(connectionString);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException($"The '{FunctionalDataInitializer.ConnectionStringName}' connection string is malformed.", exception);
            }
        }
    }
}
