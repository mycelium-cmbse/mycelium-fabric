// ------------------------------------------------------------------------------------------------
//  <copyright file="MarkdownTable.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Writes the rows of the tables of Word and Excel documents as Markdown table rows, so that every format gives the same
    /// lines and that a requirement identifier in the first cell starts its line.
    /// </summary>
    public static class MarkdownTable
    {
        /// <summary>
        /// The line breaks that can appear in the text of a cell.
        /// </summary>
        private static readonly string[] LineBreaks = ["\r\n", "\n", "\r"];

        /// <summary>
        /// Writes a table row as one line: <c>| cell | cell |</c>. The line breaks of a cell become spaces and its
        /// <c>|</c> are escaped, so that the row stays on one line with the same columns.
        /// </summary>
        /// <param name="cells">The texts of the cells of the row, in order.</param>
        /// <returns>The Markdown line of the row.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is <c>null</c>.</exception>
        public static string FormatRow(IEnumerable<string> cells)
        {
            ArgumentNullException.ThrowIfNull(cells);

            var texts = cells.Select(cell => string.Join(' ', (cell ?? string.Empty).Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Replace("|", "\\|"));

            return $"| {string.Join(" | ", texts)} |";
        }
    }
}
