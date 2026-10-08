// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;

    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    /// <summary>
    /// Extension methods that register the Mycelium Fabric MCP server in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The <see cref="IServiceCollection"/> to register the services in.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers the MCP server, its tools, the <see cref="ModelChangeApplier"/>, and an
            /// <see cref="InMemoryModelProvider"/> that loads the model from the given JSON file.
            /// </summary>
            /// <param name="modelPath">The <see cref="Uri"/> of the JSON file that contains the model.</param>
            /// <returns>
            /// The <see cref="IMcpServerBuilder"/> of the registered server, on which the host chooses the transport.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="services"/> or <paramref name="modelPath"/> is <c>null</c>.
            /// </exception>
            public IMcpServerBuilder AddFabricMcpServer(Uri modelPath)
            {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(modelPath);

                services.AddSingleton<IModelChangeApplier, ModelChangeApplier>();

                services.AddSingleton<IModelProvider>(serviceProvider =>
                {
                    var modelProvider = new InMemoryModelProvider(serviceProvider.GetRequiredService<IModelChangeApplier>());
                    modelProvider.LoadModel(modelPath);
                    return modelProvider;
                });

                return services
                    .AddMcpServer()
                    .WithTools<NavigationTools>()
                    .WithTools<BudgetTools>()
                    .WithTools<ConstructionTools>();
            }
        }
    }
}