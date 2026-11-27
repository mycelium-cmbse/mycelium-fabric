// ------------------------------------------------------------------------------------------------
//  <copyright file="AgentApplicationFactory.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Tests.TestHelpers
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Testing;

    using ModelContextProtocol.Client;

    using Moq;

    using Mycelium.Fabric.Agent.Extensions;
    using Mycelium.Fabric.Agent.Hypha;
    using Mycelium.Fabric.Agent.Modules;

    /// <summary>
    /// Starts the Fabric Agent host in memory on the model of the tests, with a mocked LLM: the MCP server, the agent and its
    /// client of the MCP server are real, the client reaching the server through the in-memory test server.
    /// </summary>
    public class AgentApplicationFactory : WebApplicationFactory<HyphaOptions>
    {
        public AgentApplicationFactory() : this(TimeSpan.FromMinutes(1), 32768)
        {
        }

        public AgentApplicationFactory(TimeSpan silenceTimeout, int contextSize)
        {
            this.SilenceTimeout = silenceTimeout;
            this.ContextSize = contextSize;
        }

        public Mock<IChatClient> ChatClient { get; } = new();

        public FakeLogger<HyphaApi> ApiLogger { get; } = new();

        private TimeSpan SilenceTimeout { get; }

        private int ContextSize { get; }

        public async Task<McpClient> CreateMcpClientAsync()
        {
            var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(this.Server.BaseAddress, Program.McpRoute) }, this.CreateClient(), this.Services.GetRequiredService<ILoggerFactory>(), true);

            return await McpClient.CreateAsync(transport);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

            builder.UseSetting(ServiceCollectionExtensions.ModelPathKey, Path.Combine(dataDirectory, "Satellite.json"))
                .UseSetting("FabricMcp:LibraryDirectory", Path.Combine(dataDirectory, "StandardLibrary"))
                .UseSetting("Hypha:McpEndpoint", $"http://localhost{Program.McpRoute}")
                .UseSetting("Hypha:SilenceTimeout", this.SilenceTimeout.ToString())
                .UseSetting("Hypha:ContextSize", this.ContextSize.ToString(CultureInfo.InvariantCulture))
                .UseSetting("Hypha:ReservedTokens", "100")
                .ConfigureTestServices(services =>
                {
                    services.AddSingleton(this.ChatClient.Object);
                    services.AddSingleton<ILogger<HyphaApi>>(this.ApiLogger);
                    services.AddHttpClient(FabricToolbox.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => this.Server.CreateHandler());
                });
        }
    }
}
