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

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

    /// <summary>
    /// Hosts the Functional Data microservice and its awaited database initialization.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        /// <summary>
        /// Initializes the Functional Data database and runs the host until shutdown.
        /// </summary>
        /// <param name="args">The command-line configuration arguments.</param>
        /// <returns>A task that completes when the host stops.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Host construction or database initialization fails.</exception>
        /// <exception cref="OperationCanceledException">Startup or execution is canceled.</exception>
        public static async Task Main(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton<IFunctionalDataInitializer, FunctionalDataInitializer>();
            builder.Services.AddHostedService<FunctionalDataInitializationService>();
            builder.Services.AddFunctionalDataAccess();

            using var host = builder.Build();

            await host.RunAsync();
        }
    }
}
