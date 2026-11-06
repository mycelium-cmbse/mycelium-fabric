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

    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Options;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Documents;
    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Prompts;
    using Mycelium.Fabric.Mcp.Resources;
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
            /// a <see cref="DocumentReader"/> that reads the document folder of the <see cref="DocumentReaderOptions"/>, the
            /// <see cref="ConstructionOptions"/> that tell whether apply_changes returns the new issues of a batch, all read from
            /// the configuration of the host, a <see cref="ModelValidator"/> with the <see cref="IValidationRule"/>s of this
            /// assembly, and for the review of a batch by the user in a host that shows MCP Apps, a <see cref="PendingBatchStore"/>,
            /// an <see cref="AppHostDetector"/> and the <see cref="BatchReviewResource"/> that serves the view of a batch. The input
            /// schema of apply_changes is the strict schema of <see cref="ModelChangeSchema"/>, and the
            /// <see cref="ToolArgumentChecker"/> checks the arguments of a tool against such a schema before the tool is called.
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
                services.AddOptions<ConstructionOptions>().BindConfiguration(ConstructionOptions.SectionName);

                services.AddSingleton<IModelExporter>(serviceProvider =>
                {
                    var exportOptions = serviceProvider.GetRequiredService<IOptions<ModelExportOptions>>().Value;
                    var exportDirectory = Path.GetFullPath(exportOptions.ExportDirectory ?? "exports", Path.GetDirectoryName(modelPath.LocalPath));

                    return new JsonModelExporter(serviceProvider.GetRequiredService<ISerializer>(), new Uri(exportDirectory));
                });

                services.AddSingleton<IPendingBatchStore, PendingBatchStore>();
                services.AddSingleton<IAppHostDetector, AppHostDetector>();

                services.AddImplementations<IValidationRule>(typeof(IValidationRule).Assembly, ServiceLifetime.Singleton);
                services.AddSingleton<IModelValidator, ModelValidator>();

                services.AddOptions<DocumentReaderOptions>().BindConfiguration(DocumentReaderOptions.SectionName);
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentParser, MarkdownDocumentParser>());
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IDocumentParser, CsvDocumentParser>());

                services.AddSingleton<IDocumentReader>(serviceProvider =>
                {
                    var documentOptions = serviceProvider.GetRequiredService<IOptions<DocumentReaderOptions>>().Value;
                    var documentDirectory = Path.GetFullPath(documentOptions.DocumentDirectory ?? "documents", Path.GetDirectoryName(modelPath.LocalPath));

                    return new DocumentReader(new Uri(documentDirectory), serviceProvider.GetServices<IDocumentParser>());
                });

                // WithTools<T>() takes no options for the input schema: the tools of ConstructionTools are created as it creates
                // them, apply_changes with the strict schema of ModelChangeSchema.
                services.AddSingleton(serviceProvider => CreateConstructionTool(nameof(ConstructionTools.ApplyChanges), ModelChangeSchema.CreateOptions, serviceProvider));
                services.AddSingleton(serviceProvider => CreateConstructionTool(nameof(ConstructionTools.DecideBatch), schemaCreateOptions: null, serviceProvider));

                return services
                    .AddMcpServer()
                    .WithTools<NavigationTools>()
                    .WithTools<BudgetTools>()
                    .WithTools<RequirementTools>()
                    .WithTools<ExportTools>()
                    .WithTools<TradeOffTools>(TradeOffTools.StudySerializerOptions)
                    .WithTools<ValidationTools>()
                    .WithTools<DocumentTools>()
                    .WithRequestFilters(filters => filters.AddCallToolFilter(ToolArgumentChecker.CheckArguments))
                    // A static class cannot be a type argument: the prompts are registered by their type.
                    .WithPrompts([typeof(GenerationPrompts), typeof(RequirementPrompts)])
                    .WithResources([typeof(BatchReviewResource), typeof(TradeOffResource), typeof(ModelHealthResource)])
                    // Last, so that the MCP Apps metadata of every tool registered above is written.
                    .WithMcpApps();
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

        /// <summary>
        /// Creates a tool of <see cref="ConstructionTools"/> as <c>WithTools&lt;T&gt;()</c> creates it, a new
        /// <see cref="ConstructionTools"/> answering each call, with the given options of its input schema.
        /// </summary>
        /// <param name="methodName">The name of the method of the tool, for example <c>ApplyChanges</c>.</param>
        /// <param name="schemaCreateOptions">The options of the input schema, or <c>null</c> for those of the SDK.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> of the server.</param>
        /// <returns>The <see cref="McpServerTool"/>.</returns>
        private static McpServerTool CreateConstructionTool(string methodName, AIJsonSchemaCreateOptions schemaCreateOptions, IServiceProvider serviceProvider)
        {
            return McpServerTool.Create(typeof(ConstructionTools).GetMethod(methodName), request => ActivatorUtilities.CreateInstance<ConstructionTools>(request.Services),
                new McpServerToolCreateOptions { Services = serviceProvider, SchemaCreateOptions = schemaCreateOptions });
        }
    }
}
