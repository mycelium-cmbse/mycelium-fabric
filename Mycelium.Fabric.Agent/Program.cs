// ------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent
{
    using System;

    using Carter;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.Agent.Extensions;
    using Mycelium.Fabric.Agent.Hypha;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Entry class of the Fabric Agent host: the Fabric MCP server over HTTP and Hypha, the assistant that Bloom talks to.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The route of the Fabric MCP server.
        /// </summary>
        public const string McpRoute = "/mcp";

        /// <summary>
        /// Entry method of the host.
        /// </summary>
        /// <param name="args">The launch arguments, such as <c>--FabricMcp:ModelPath=model.json</c>.</param>
        /// <exception cref="InvalidOperationException">Thrown when the path of the model or the LLM is not configured.</exception>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddFabricAgent(builder.Configuration, builder.Environment.ContentRootPath);

            var app = builder.Build();

            // Loads the model and creates the agent at start: a wrong setting fails here, not at the first answer.
            app.Services.GetRequiredService<IModelProvider>();
            app.Services.GetRequiredService<IHyphaAgent>();

            app.MapMcp(McpRoute);
            app.MapCarter();

            app.Run();
        }
    }
}
