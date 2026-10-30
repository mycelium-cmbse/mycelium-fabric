// ------------------------------------------------------------------------------------------------
//  <copyright file="IAppHostDetector.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using ModelContextProtocol.Protocol;

    /// <summary>
    /// Tells whether the host connected to the server shows MCP Apps, that is the HTML views that the tools link to.
    /// </summary>
    public interface IAppHostDetector
    {
        /// <summary>
        /// Tells whether a client shows the HTML views of MCP Apps, from the capabilities it declared when it connected.
        /// </summary>
        /// <param name="clientCapabilities">The <see cref="ClientCapabilities"/> of the client, or <c>null</c> when unknown.</param>
        /// <returns><c>true</c> when the client shows the HTML views of MCP Apps.</returns>
        bool ShowsApps(ClientCapabilities clientCapabilities);
    }
}
