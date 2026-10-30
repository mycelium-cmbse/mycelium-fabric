// ------------------------------------------------------------------------------------------------
//  <copyright file="BatchReviewResource.cs" company="Starion Group S.A.">
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
    /// The MCP resource that serves the view of a batch of changes: the HTML page that a host showing MCP Apps displays with the
    /// result of apply_changes, and in which the user applies or rejects the batch.
    /// </summary>
    [McpServerResourceType]
    public static class BatchReviewResource
    {
        /// <summary>
        /// The URI of the view, which apply_changes and decide_batch declare in their <c>_meta.ui</c>.
        /// </summary>
        public const string Uri = "ui://fabric/review-batch.html";

        /// <summary>
        /// The HTML page, read once from the McpApp folder embedded in this assembly, so that the page goes wherever the assembly goes.
        /// </summary>
        private static readonly Lazy<string> Page = new(() => McpAppPage.Read("review-batch.html"));

        /// <summary>
        /// Gets the HTML page of the view of a batch.
        /// </summary>
        /// <returns>The HTML page.</returns>
        [McpServerResource(UriTemplate = Uri, Name = "review_batch", MimeType = McpApps.HtmlMimeType)]
        [Description("The view in which the user applies or rejects a batch of changes sent to apply_changes.")]
        [return: Description("The HTML page of the view, with its style sheet and script inside.")]
        public static string GetPage()
        {
            return Page.Value;
        }
    }
}
