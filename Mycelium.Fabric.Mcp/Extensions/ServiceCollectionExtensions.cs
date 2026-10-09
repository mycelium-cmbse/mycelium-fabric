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
    using System.IO;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Options;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Extension methods that register the Mycelium Fabric MCP server in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <param name="services">The <see cref="IServiceCollection"/> to register the services in.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers the MCP server, its tools, the JSON <see cref="Serializer"/> and <see cref="DeSerializer"/> unless the
            /// host has registered its own, the <see cref="ModelChangeApplier"/>, an <see cref="InMemoryModelProvider"/> that
            /// loads the model from the given JSON file, and a <see cref="JsonModelExporter"/> that writes the model to the
            /// export folder of the <see cref="ModelExportOptions"/>, read from the configuration of the host.
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

                services.TryAddSingleton<ISerializer, Serializer>();
                services.TryAddSingleton<IDeSerializer, DeSerializer>();
                services.AddSingleton<IModelChangeApplier, ModelChangeApplier>();

                services.AddSingleton<IModelProvider>(serviceProvider =>
                {
                    var modelProvider = new InMemoryModelProvider(serviceProvider.GetRequiredService<IModelChangeApplier>(), serviceProvider.GetRequiredService<IDeSerializer>());
                    modelProvider.LoadModel(modelPath);
                    return modelProvider;
                });

                services.AddOptions<ModelExportOptions>().BindConfiguration(ModelExportOptions.SectionName);

                services.AddSingleton<IModelExporter>(serviceProvider =>
                {
                    var exportOptions = serviceProvider.GetRequiredService<IOptions<ModelExportOptions>>().Value;
                    var exportDirectory = Path.GetFullPath(exportOptions.ExportDirectory ?? "exports", Path.GetDirectoryName(modelPath.LocalPath));

                    return new JsonModelExporter(serviceProvider.GetRequiredService<ISerializer>(), new Uri(exportDirectory));
                });

                return services
                    .AddMcpServer()
                    .WithTools<NavigationTools>()
                    .WithTools<BudgetTools>()
                    .WithTools<ConstructionTools>()
                    .WithTools<RequirementTools>()
                    .WithTools<ExportTools>();
            }
        }
    }
}
