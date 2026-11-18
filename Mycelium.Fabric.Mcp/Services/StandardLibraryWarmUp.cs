// ------------------------------------------------------------------------------------------------
//  <copyright file="StandardLibraryWarmUp.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Starts loading the <see cref="IStandardLibrary"/> in the background when the host starts, so that the server answers
    /// at once and the library is ready, or nearly, when a tool first needs it.
    /// </summary>
    public class StandardLibraryWarmUp : IHostedService
    {
        /// <summary>
        /// The <see cref="IStandardLibrary"/> to load.
        /// </summary>
        private readonly IStandardLibrary standardLibrary;

        /// <summary>
        /// Initializes a new instance of the <see cref="StandardLibraryWarmUp"/> class.
        /// </summary>
        /// <param name="standardLibrary">The <see cref="IStandardLibrary"/> to load.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="standardLibrary"/> is <c>null</c>.</exception>
        public StandardLibraryWarmUp(IStandardLibrary standardLibrary)
        {
            ArgumentNullException.ThrowIfNull(standardLibrary);

            this.standardLibrary = standardLibrary;
        }

        /// <summary>
        /// Starts loading the library, without waiting for the end of the loading.
        /// </summary>
        /// <param name="cancellationToken">Not used: the loading is not awaited.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.standardLibrary.StartLoading();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Does nothing: the library stays in memory until the server stops.
        /// </summary>
        /// <param name="cancellationToken">Not used.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
