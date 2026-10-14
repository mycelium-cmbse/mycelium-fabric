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
    using System.Linq;
    using System.Reflection;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Options;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Prompts;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.TradeOff;
    using Mycelium.Fabric.Mcp.Validation;

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
            /// Registers the MCP server, its tools and prompts, the JSON <see cref="Serializer"/> and <see cref="DeSerializer"/>
            /// unless the host has registered its own, the <see cref="ModelChangeApplier"/>, the <see cref="CpSatTradeOffSolver"/>,
            /// an <see cref="InMemoryModelProvider"/> that loads the model from the given JSON file, a
            /// <see cref="JsonModelExporter"/> that writes the model to the export folder of the <see cref="ModelExportOptions"/>,
            /// a <see cref="DocumentReader"/> that reads the document folder of the <see cref="DocumentReaderOptions"/>, both read
            /// from the configuration of the host, and a <see cref="ModelValidator"/> with the <see cref="IValidationRule"/>s of
            /// this assembly.
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
                services.AddSingleton<ITradeOffSolver, CpSatTradeOffSolver>();

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

                services.AddImplementations<IValidationRule>(typeof(IValidationRule).Assembly, ServiceLifetime.Singleton);
                services.AddSingleton<IModelValidator, ModelValidator>();

                services.AddOptions<DocumentReaderOptions>().BindConfiguration(DocumentReaderOptions.SectionName);
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentParser, MarkdownDocumentParser>());
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentParser, WordDocumentParser>());
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentParser, ExcelDocumentParser>());

                services.AddSingleton<IDocumentReader>(serviceProvider =>
                {
                    var documentOptions = serviceProvider.GetRequiredService<IOptions<DocumentReaderOptions>>().Value;
                    var documentDirectory = Path.GetFullPath(documentOptions.DocumentDirectory ?? "documents", Path.GetDirectoryName(modelPath.LocalPath));

                    return new DocumentReader(new Uri(documentDirectory), serviceProvider.GetServices<IDocumentParser>());
                });

                return services
                    .AddMcpServer()
                    .WithTools<NavigationTools>()
                    .WithTools<BudgetTools>()
                    .WithTools<ConstructionTools>()
                    .WithTools<RequirementTools>()
                    .WithTools<ExportTools>()
                    .WithTools<TradeOffTools>()
                    .WithTools<ValidationTools>()
                    .WithTools<DocumentTools>()
                    // A static class cannot be a type argument: the prompts are registered by their type.
                    .WithPrompts([typeof(GenerationPrompts), typeof(RequirementPrompts)]);
            }

            /// <summary>
            /// Registers, as a <typeparamref name="TService"/>, each class of an assembly that implements or inherits it, so
            /// that a new implementation, for example a new <see cref="IValidationRule"/>, is registered without another line.
            /// The abstract and generic classes are left out, and an implementation already registered is not added again.
            /// </summary>
            /// <typeparam name="TService">The type of the service, for example an interface.</typeparam>
            /// <param name="assembly">The <see cref="Assembly"/> to scan.</param>
            /// <param name="lifetime">The <see cref="ServiceLifetime"/> of the registrations.</param>
            /// <returns>The <see cref="IServiceCollection"/>, to chain the registrations.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="services"/> or <paramref name="assembly"/> is <c>null</c>.
            /// </exception>
            public IServiceCollection AddImplementations<TService>(Assembly assembly, ServiceLifetime lifetime)
            {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(assembly);

                var implementationTypes = assembly.GetTypes()
                    .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && typeof(TService).IsAssignableFrom(type))
                    .OrderBy(type => type.FullName, StringComparer.Ordinal);

                foreach (var implementationType in implementationTypes)
                {
                    services.TryAddEnumerable(ServiceDescriptor.Describe(typeof(TService), implementationType, lifetime));
                }

                return services;
            }
        }
    }
}
