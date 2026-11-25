// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Extensions
{
    using System;
    using System.ClientModel;
    using System.ClientModel.Primitives;
    using System.IO;
    using System.Threading;

    using Carter;

    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Options;

    using Mycelium.Fabric.Agent.Hypha;
    using Mycelium.Fabric.Mcp.Extensions;

    using OpenAI;

    /// <summary>
    /// Registers the services of the Fabric Agent host in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// The key of the setting that gives the JSON file of the model that the MCP server serves.
        /// </summary>
        public const string ModelPathKey = "FabricMcp:ModelPath";

        /// <param name="services">The <see cref="IServiceCollection"/> to register the services in.</param>
        extension(IServiceCollection services)
        {
            /// <summary>
            /// Registers the Fabric MCP server over Streamable HTTP, with the model of the <see cref="ModelPathKey"/> setting, and
            /// Hypha: the <see cref="HyphaOptions"/> of the <c>Hypha</c> section, the <see cref="IChatClient"/> of the
            /// OpenAI-compatible API of the LLM unless the host has registered its own, the <see cref="FabricToolbox"/> with its
            /// <see cref="System.Net.Http.HttpClient"/>, the <see cref="HyphaAgent"/> and the Carter module of its routes.
            /// </summary>
            /// <param name="configuration">The <see cref="IConfiguration"/> of the host.</param>
            /// <param name="contentRootPath">The folder against which a relative path of the model is resolved.</param>
            /// <returns>The <see cref="IServiceCollection"/>, for chaining.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.</exception>
            /// <exception cref="InvalidOperationException">Thrown when the <see cref="ModelPathKey"/> setting is missing.</exception>
            public IServiceCollection AddFabricAgent(IConfiguration configuration, string contentRootPath)
            {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(configuration);

                var modelPath = configuration[ModelPathKey];

                if (string.IsNullOrWhiteSpace(modelPath))
                {
                    throw new InvalidOperationException($"The '{ModelPathKey}' setting is required.");
                }

                services.AddFabricMcpServer(new Uri(Path.GetFullPath(modelPath, contentRootPath ?? AppContext.BaseDirectory)))
                    // Stateful: each MCP session keeps the capabilities of its client (MCP Apps or not), which the tools read.
                    .WithHttpTransport(options => options.Stateless = false);

                services.AddOptions<HyphaOptions>().Bind(configuration.GetSection(HyphaOptions.SectionName));
                services.TryAddSingleton(CreateChatClient);
                services.AddHttpClient(FabricToolbox.HttpClientName);
                services.AddSingleton<IFabricToolbox, FabricToolbox>();
                services.AddSingleton<IHyphaAgent, HyphaAgent>();
                services.AddCarter();

                return services;
            }
        }

        /// <summary>
        /// Creates the <see cref="IChatClient"/> of the OpenAI-compatible API of the LLM, with no retry and no time limit: a
        /// local LLM may take minutes to read a long prompt, and the <see cref="HyphaAgent"/> applies its own limit of silence.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> that gives the <see cref="HyphaOptions"/>.</param>
        /// <returns>The <see cref="IChatClient"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the endpoint or the model of the LLM is not set.</exception>
        private static IChatClient CreateChatClient(IServiceProvider serviceProvider)
        {
            var options = serviceProvider.GetRequiredService<IOptions<HyphaOptions>>().Value;

            if (options.Endpoint is null || string.IsNullOrWhiteSpace(options.Model))
            {
                throw new InvalidOperationException($"The settings {HyphaOptions.SectionName}:{nameof(HyphaOptions.Endpoint)} and {HyphaOptions.SectionName}:{nameof(HyphaOptions.Model)} are required.");
            }

            var clientOptions = new OpenAIClientOptions { Endpoint = options.Endpoint, NetworkTimeout = Timeout.InfiniteTimeSpan, RetryPolicy = new ClientRetryPolicy(0) };

            // A local LLM asks for no key, but the OpenAI client needs one.
            var key = string.IsNullOrWhiteSpace(options.ApiKey) ? "none" : options.ApiKey;

            return new OpenAIClient(new ApiKeyCredential(key), clientOptions).GetChatClient(options.Model).AsIChatClient();
        }
    }
}
