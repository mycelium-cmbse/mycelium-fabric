// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelHealthResource.cs" company="Starion Group S.A.">
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
    /// The MCP resource that serves the view of the health of the model: the HTML page that a host showing MCP Apps displays
    /// with the result of check_requirements, in which the user sees the requirements and the validation issues together and
    /// acts on each of them.
    /// </summary>
    [McpServerResourceType]
    public static class ModelHealthResource
    {
        /// <summary>
        /// The URI of the view, which check_requirements declares in its <c>_meta.ui</c>.
        /// </summary>
        public const string Uri = "ui://fabric/model-health.html";

        /// <summary>
        /// The HTML page, read once from the McpApp folder embedded in this assembly, so that the page goes wherever the assembly goes.
        /// </summary>
        private static readonly Lazy<string> Page = new(() => McpAppPage.Read("model-health.html"));

        /// <summary>
        /// Gets the HTML page of the view of the health of the model.
        /// </summary>
        /// <returns>The HTML page.</returns>
        [McpServerResource(UriTemplate = Uri, Name = "model_health", MimeType = McpApps.HtmlMimeType)]
        [Description("The view in which the user sees the requirements and the validation issues of the model together, and acts on each of them.")]
        [return: Description("The HTML page of the view, with its style sheets and script inside.")]
        public static string GetPage()
        {
            return Page.Value;
        }
    }
}
