// ------------------------------------------------------------------------------------------------
//  <copyright file="FabricToolbox.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Hypha
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using ModelContextProtocol.Client;
    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Protocol;

    /// <summary>
    /// Gives the LLM the tools of the Fabric MCP server, through a client of the server over HTTP: the same way in as the
    /// views of Bloom. The tools that only the views may call, such as the decision on a batch, are kept from the LLM.
    /// </summary>
    public class FabricToolbox : IFabricToolbox
    {
        /// <summary>
        /// The name of the <see cref="HttpClient"/> that calls the Fabric MCP server.
        /// </summary>
        public const string HttpClientName = "FabricMcp";

        /// <summary>
        /// The options of the client, which says who it is to the server.
        /// </summary>
        private static readonly McpClientOptions ClientOptions = new() { ClientInfo = new Implementation { Name = "Hypha", Version = "0.0.1" } };

        /// <summary>
        /// Initializes a new instance of the <see cref="FabricToolbox"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> that gives the <see cref="HttpClient"/> named <see cref="HttpClientName"/>.</param>
        /// <param name="options">The <see cref="HyphaOptions"/>, whose <see cref="HyphaOptions.McpEndpoint"/> is the address of the server.</param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> of the client.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
        public FabricToolbox(IHttpClientFactory httpClientFactory, IOptions<HyphaOptions> options, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            this.HttpClientFactory = httpClientFactory;
            this.Options = options.Value;
            this.LoggerFactory = loggerFactory;
        }

        /// <summary>
        /// Gets the <see cref="IHttpClientFactory"/> that gives the <see cref="HttpClient"/> of the client.
        /// </summary>
        private IHttpClientFactory HttpClientFactory { get; }

        /// <summary>
        /// Gets the <see cref="HyphaOptions"/>.
        /// </summary>
        private HyphaOptions Options { get; }

        /// <summary>
        /// Gets the <see cref="ILoggerFactory"/> of the client.
        /// </summary>
        private ILoggerFactory LoggerFactory { get; }

        /// <summary>
        /// Opens a session with the Fabric MCP server and lists the tools that the LLM may call: a new session for each answer,
        /// so an answer never meets a session that the server has closed; the state of the model is on the server anyway.
        /// </summary>
        /// <param name="cancellationToken">Cancels the opening of the session.</param>
        /// <returns>The <see cref="FabricToolSession"/>, which the caller disposes once the answer is over.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="HyphaOptions.McpEndpoint"/> is not set.</exception>
        public async Task<FabricToolSession> OpenAsync(CancellationToken cancellationToken)
        {
            var endpoint = this.Options.McpEndpoint ?? throw new InvalidOperationException($"The setting {HyphaOptions.SectionName}:{nameof(HyphaOptions.McpEndpoint)} is required.");
            var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpoint, Name = "Fabric MCP" }, this.HttpClientFactory.CreateClient(HttpClientName), this.LoggerFactory, true);
            var client = await McpClient.CreateAsync(transport, ClientOptions, this.LoggerFactory, cancellationToken);

            try
            {
                var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

                return new FabricToolSession(client, [.. tools.Where(IsForModel).Cast<AITool>()]);
            }
            catch
            {
                await client.DisposeAsync();
                throw;
            }
        }

        /// <summary>
        /// Tells whether the LLM may call a tool: a tool of an MCP App is visible to the LLM unless its visibility names only the views.
        /// </summary>
        /// <param name="tool">The tool, as the server describes it.</param>
        /// <returns><c>true</c> when the LLM may call the tool.</returns>
        private static bool IsForModel(McpClientTool tool)
        {
            var visibility = tool.ProtocolTool.Meta?["ui"]?.Deserialize<McpUiToolMeta>(McpApps.SerializerOptions)?.Visibility;

            return visibility is null || visibility.Count == 0 || visibility.Contains(McpUiToolVisibility.Model);
        }
    }
}
