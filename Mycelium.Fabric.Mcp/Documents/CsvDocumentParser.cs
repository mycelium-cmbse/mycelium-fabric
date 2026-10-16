// ------------------------------------------------------------------------------------------------
//  <copyright file="CsvDocumentParser.cs" company="Starion Group S.A.">
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
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    using CsvHelper;
    using CsvHelper.Configuration;

    /// <summary>
    /// The <see cref="IDocumentParser"/> of CSV files (<c>.csv</c>, RFC 4180), read with CsvHelper: one section without
    /// heading, and one line <c>| cell | cell |</c> per record that is not empty. The cells keep their column, so that the
    /// columns stay aligned from record to record.
    /// </summary>
    public sealed class CsvDocumentParser : IDocumentParser
    {
        /// <summary>
        /// The <see cref="CsvConfiguration"/> of the files: the delimiter is detected among the comma, the semicolon (the CSV
        /// of the spreadsheets set to a language that writes decimals with a comma) and the tab, and a double quote inside a
        /// cell without double quotes is kept as text.
        /// </summary>
        private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
        {
            DetectDelimiter = true,
            DetectDelimiterValues = [",", ";", "\t"],
            BadDataFound = RejectUnclosedCell
        };

        /// <summary>
        /// Gets the name of the format: <c>CSV</c>.
        /// </summary>
        public string FormatName => "CSV";

        /// <summary>
        /// Gets the extension of the files of this format: <c>.csv</c>.
        /// </summary>
        public IReadOnlyCollection<string> Extensions { get; } = [".csv"];

        /// <summary>
        /// Splits a CSV file into one section of its records. The delimiter is the one of the comma, the semicolon and the tab
        /// that appears on every line, the most often; a comma when there is none. A cell between double quotes can hold
        /// delimiters, line breaks and doubled double quotes.
        /// </summary>
        /// <param name="content">The content of the file, in UTF-8 unless it starts with another byte order mark.</param>
        /// <returns>The sections of the file: one, or none when the file has no cell.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidDataException">Thrown when a cell between double quotes is not closed.</exception>
        public IReadOnlyList<DocumentSection> Parse(Stream content)
        {
            ArgumentNullException.ThrowIfNull(content);

            using var reader = new StreamReader(content, Encoding.UTF8, true, -1, true);
            using var parser = new CsvParser(reader, Configuration);
            var builder = new SectionListBuilder();

            while (parser.Read())
            {
                var cells = parser.Record;
                var lastValue = Array.FindLastIndex(cells, cell => !string.IsNullOrWhiteSpace(cell));

                if (lastValue >= 0)
                {
                    builder.AddLine(MarkdownTable.FormatRow(cells[..(lastValue + 1)]));
                }
            }

            return builder.Build();
        }

        /// <summary>
        /// Refuses a cell between double quotes that is not closed, which CsvHelper would read up to the end of the file. The
        /// other bad cells (a double quote inside a cell without double quotes, text after the closing double quote) are kept.
        /// </summary>
        /// <param name="args">The <see cref="BadDataFoundArgs"/> of the bad cell, with its raw text.</param>
        /// <exception cref="InvalidDataException">Thrown when the cell is not closed.</exception>
        private static void RejectUnclosedCell(BadDataFoundArgs args)
        {
            var cell = args.Field;

            if (cell.StartsWith('"') && cell.Count(character => character == '"') % 2 == 1)
            {
                var startLine = args.Context.Parser.RawRow - cell.Count(character => character == '\n');
                throw new InvalidDataException($"The quoted cell that starts on line {startLine} is not closed.");
            }
        }
    }
}
