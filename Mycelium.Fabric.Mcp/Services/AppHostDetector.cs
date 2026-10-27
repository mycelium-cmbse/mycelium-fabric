// ------------------------------------------------------------------------------------------------
//  <copyright file="AppHostDetector.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System.Linq;

    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Protocol;

    /// <summary>
    /// An <see cref="IAppHostDetector"/> that reads the MCP Apps extension in the capabilities of the client.
    /// </summary>
    public class AppHostDetector : IAppHostDetector
    {
        /// <summary>
        /// Tells whether a client shows the HTML views of MCP Apps: it declares the MCP Apps extension, with the MIME type of
        /// its HTML views.
        /// </summary>
        /// <param name="clientCapabilities">The <see cref="ClientCapabilities"/> of the client, or <c>null</c> when unknown.</param>
        /// <returns><c>true</c> when the client shows the HTML views of MCP Apps.</returns>
        public bool ShowsApps(ClientCapabilities clientCapabilities)
        {
            if (clientCapabilities == null)
            {
                return false;
            }

            return McpApps.GetUiCapability(clientCapabilities)?.MimeTypes?.Contains(McpApps.HtmlMimeType) == true;
        }
    }
}
