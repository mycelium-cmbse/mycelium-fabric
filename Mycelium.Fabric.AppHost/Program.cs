// ------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.AppHost
{
    using System.Diagnostics.CodeAnalysis;

    using Aspire.Hosting;
    using Aspire.Hosting.ApplicationModel;

    /// <summary>
    /// Defines and runs the local PostgreSQL and concurrent-server environment.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        /// <summary>
        /// Builds the local Aspire resource model and runs the application until host shutdown.
        /// </summary>
        /// <param name="args">The command-line arguments forwarded to the Aspire application builder.</param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="args"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="DistributedApplicationException">
        /// The Aspire operation or resource model configuration is invalid.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// The host cannot be constructed from its configuration and registered services.
        /// </exception>
        /// <exception cref="System.AggregateException">
        /// The asynchronous application run fails and its exception is propagated by the synchronous host runner.
        /// </exception>
        public static void Main(string[] args)
        {
            var builder = DistributedApplication.CreateBuilder(args);
            var volumeName = builder.Configuration["FunctionalData:VolumeName"] ?? "mycelium-fabric-functionaldata-pg18";

            var postgres = builder.AddPostgres("postgres").WithImageTag("18.6").WithDataVolume(volumeName);

            var functionalData = postgres.AddDatabase("functionaldata", "mycelium_functional_data");

            builder.AddProject<Projects.Mycelium_Fabric_ConcurrentServer>("concurrent-server")
                .WithHttpEndpoint()
                .WithReference(functionalData)
                .WaitFor(functionalData, WaitBehavior.WaitOnResourceUnavailable);

            builder.Build().Run();
        }
    }
}
