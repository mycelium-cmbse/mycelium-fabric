// ------------------------------------------------------------------------------------------------
//  <copyright file="McpAppPage.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Resources
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Reads the HTML page of an MCP App from the files of the McpApp folder embedded in this assembly, with the style sheets and
    /// scripts it links to written inside the page: a host shows the page alone in a sandboxed frame, where a relative link leads
    /// nowhere.
    /// </summary>
    public static partial class McpAppPage
    {
        /// <summary>
        /// The prefix of the names of the files of the McpApp folder in the resources embedded in this assembly.
        /// </summary>
        private const string ResourcePrefix = "Mycelium.Fabric.Mcp.McpApp.";

        /// <summary>
        /// Reads an HTML page of the McpApp folder and writes inside it the style sheets and scripts of the same folder it links to.
        /// </summary>
        /// <param name="fileName">The name of the HTML file in the McpApp folder, such as <c>review-batch.html</c>.</param>
        /// <returns>The HTML page, in one piece.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the assembly does not contain the page or a file it links to.</exception>
        public static string Read(string fileName)
        {
            var page = ReadFile(fileName);
            page = StyleSheetLink().Replace(page, match => $"<style>\n{ReadFile(match.Groups["file"].Value)}</style>");

            return ScriptLink().Replace(page, match => $"<script>\n{ReadFile(match.Groups["file"].Value)}</script>");
        }

        /// <summary>
        /// Reads a file of the McpApp folder from the resources of this assembly.
        /// </summary>
        /// <param name="fileName">The name of the file in the McpApp folder.</param>
        /// <returns>The content of the file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the assembly does not contain the file.</exception>
        private static string ReadFile(string fileName)
        {
            var resourceName = ResourcePrefix + fileName;

            using var stream = typeof(McpAppPage).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"The resource '{resourceName}' is missing from the assembly.");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }

        /// <summary>
        /// Matches the link to a style sheet of the McpApp folder: <c>&lt;link rel="stylesheet" href="style.css"&gt;</c>.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of a link to a style sheet.</returns>
        [GeneratedRegex("""<link\s+rel="stylesheet"\s+href="(?<file>[^"/:]+)"\s*/?>""", RegexOptions.None, 1000)]
        private static partial Regex StyleSheetLink();

        /// <summary>
        /// Matches the link to a script of the McpApp folder: <c>&lt;script src="script.js"&gt;&lt;/script&gt;</c>.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of a link to a script.</returns>
        [GeneratedRegex("""<script\s+src="(?<file>[^"/:]+)"\s*></script>""", RegexOptions.None, 1000)]
        private static partial Regex ScriptLink();
    }
}
