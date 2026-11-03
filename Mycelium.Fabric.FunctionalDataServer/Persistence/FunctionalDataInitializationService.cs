// ------------------------------------------------------------------------------------------------
//  <copyright file="FunctionalDataInitializationService.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Gates successful host startup on completion of Functional Data initialization.
    /// </summary>
    public sealed class FunctionalDataInitializationService : IHostedService
    {
        /// <summary>
        /// The initializer awaited during startup.
        /// </summary>
        private readonly FunctionalDataInitializer initializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionalDataInitializationService"/> class.
        /// </summary>
        /// <param name="initializer">The Functional Data database initializer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="initializer"/> is null.</exception>
        public FunctionalDataInitializationService(FunctionalDataInitializer initializer)
        {
            ArgumentNullException.ThrowIfNull(initializer);

            this.initializer = initializer;
        }

        /// <summary>
        /// Awaits database initialization before reporting successful service startup.
        /// </summary>
        /// <param name="cancellationToken">The token canceling host startup.</param>
        /// <returns>A task that completes when initialization succeeds.</returns>
        /// <exception cref="InvalidOperationException">Database initialization fails.</exception>
        /// <exception cref="OperationCanceledException">Startup is canceled or initialization exceeds its deadline.</exception>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return this.initializer.InitializeAsync(cancellationToken);
        }

        /// <summary>
        /// Completes shutdown without retaining a database connection or background initialization task.
        /// </summary>
        /// <param name="cancellationToken">The token supplied for host shutdown.</param>
        /// <returns>An already completed task.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
