// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementExtractionResource.cs" company="Starion Group S.A.">
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
    /// The MCP resource that serves the view of the extraction of requirements: the HTML page that a host showing MCP Apps
    /// displays with the result of propose_requirements, and in which the user chooses the requirements to import.
    /// </summary>
    [McpServerResourceType]
    public static class RequirementExtractionResource
    {
        /// <summary>
        /// The URI of the view, which propose_requirements declares in its <c>_meta.ui</c>.
        /// </summary>
        public const string Uri = "ui://fabric/requirement-extraction.html";

        /// <summary>
        /// The HTML page, read once from the McpApp folder embedded in this assembly, so that the page goes wherever the assembly goes.
        /// </summary>
        private static readonly Lazy<string> Page = new(() => McpAppPage.Read("requirement-extraction.html"));

        /// <summary>
        /// Gets the HTML page of the view of the extraction of requirements.
        /// </summary>
        /// <returns>The HTML page.</returns>
        [McpServerResource(UriTemplate = Uri, Name = "requirement_extraction", MimeType = McpApps.HtmlMimeType)]
        [Description("The view in which the user reviews the requirements found in a section of a document and imports the ones they keep.")]
        [return: Description("The HTML page of the view, with its style sheet and scripts inside.")]
        public static string GetPage()
        {
            return Page.Value;
        }
    }
}
