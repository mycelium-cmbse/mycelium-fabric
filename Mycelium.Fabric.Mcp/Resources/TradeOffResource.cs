// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffResource.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Resources
{
    using System;
    using System.ComponentModel;

    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Server;

    /// <summary>
    /// The MCP resource that serves the view of a trade-off: the HTML page that a host showing MCP Apps displays with the
    /// result of solve_trade_off, and in which the user changes the settings of the trade-off without going back through the
    /// AI assistant.
    /// </summary>
    [McpServerResourceType]
    public class TradeOffResource
    {
        /// <summary>
        /// The URI of the view, which solve_trade_off declares in its <c>_meta.ui</c>.
        /// </summary>
        public const string Uri = "ui://fabric/trade-off.html";

        /// <summary>
        /// The HTML page, read once from the McpApp folder embedded in this assembly, so that the page goes wherever the assembly goes.
        /// </summary>
        private static readonly Lazy<string> Page = new(() => McpAppPage.Read("trade-off.html"));

        /// <summary>
        /// Gets the HTML page of the view of a trade-off.
        /// </summary>
        /// <returns>The HTML page.</returns>
        [McpServerResource(UriTemplate = Uri, Name = "trade_off", MimeType = McpApps.HtmlMimeType)]
        [Description("The view in which the user explores the result of solve_trade_off with other settings.")]
        public static string GetPage()
        {
            return Page.Value;
        }
    }
}
