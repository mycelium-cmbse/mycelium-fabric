// ------------------------------------------------------------------------------------------------
//  <copyright file="FabricToolSession.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Hypha
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Extensions.AI;

    /// <summary>
    /// A session with the Fabric MCP server and the tools of the server that the LLM may call during one answer.
    /// </summary>
    public sealed class FabricToolSession : IAsyncDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FabricToolSession"/> class.
        /// </summary>
        /// <param name="client">The session with the server, closed with this instance; <c>null</c> when there is none to close.</param>
        /// <param name="tools">The tools that the LLM may call.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tools"/> is <c>null</c>.</exception>
        public FabricToolSession(IAsyncDisposable client, IList<AITool> tools)
        {
            ArgumentNullException.ThrowIfNull(tools);

            this.Client = client;
            this.Tools = tools;
        }

        /// <summary>
        /// Gets the tools that the LLM may call, each of which calls the server.
        /// </summary>
        public IList<AITool> Tools { get; }

        /// <summary>
        /// Gets the session with the server.
        /// </summary>
        private IAsyncDisposable Client { get; }

        /// <summary>
        /// Closes the session with the server.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that completes once the session is closed.</returns>
        public ValueTask DisposeAsync() => this.Client?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
