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
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// The <see cref="IDocumentParser"/> of CSV files (<c>.csv</c>, RFC 4180): one section without heading, and one line
    /// <c>| cell | cell |</c> per record that is not empty. The cells keep their column, so that the columns stay aligned
    /// from record to record.
    /// </summary>
    public sealed class CsvDocumentParser : IDocumentParser
    {
        /// <summary>
        /// The delimiters that can separate the cells, by order of preference: comma, semicolon (the CSV of the spreadsheets
        /// set to a language that writes decimals with a comma) and tab.
        /// </summary>
        private static readonly char[] Delimiters = [',', ';', '\t'];

        /// <summary>
        /// Gets the name of the format: <c>CSV</c>.
        /// </summary>
        public string FormatName => "CSV";

        /// <summary>
        /// Gets the extension of the files of this format: <c>.csv</c>.
        /// </summary>
        public IReadOnlyCollection<string> Extensions { get; } = [".csv"];

        /// <summary>
        /// Splits a CSV file into one section of its records. The delimiter is the one that appears most often in the first
        /// record, a comma when there is none. A cell between double quotes can hold delimiters, line breaks and doubled
        /// double quotes.
        /// </summary>
        /// <param name="content">The content of the file, in UTF-8 unless it starts with another byte order mark.</param>
        /// <returns>The sections of the file: one, or none when the file has no cell.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidDataException">Thrown when a cell between double quotes is not closed.</exception>
        public IReadOnlyList<DocumentSection> Parse(Stream content)
        {
            ArgumentNullException.ThrowIfNull(content);

            using var reader = new StreamReader(content, Encoding.UTF8, true, -1, true);
            var text = reader.ReadToEnd();
            var builder = new SectionListBuilder();

            foreach (var cells in ReadRecords(text, FindDelimiter(text)))
            {
                var lastValue = cells.FindLastIndex(cell => !string.IsNullOrWhiteSpace(cell));

                if (lastValue >= 0)
                {
                    builder.AddLine(MarkdownTable.FormatRow(cells[..(lastValue + 1)]));
                }
            }

            return builder.Build();
        }

        /// <summary>
        /// Finds the delimiter of a CSV text: the one of <see cref="Delimiters"/> that appears most often in the first record,
        /// outside double quotes.
        /// </summary>
        /// <param name="text">The CSV text.</param>
        /// <returns>The delimiter, a comma when the first record has none.</returns>
        private static char FindDelimiter(string text)
        {
            var counts = new int[Delimiters.Length];
            var quoted = false;

            foreach (var character in text)
            {
                if (character == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && character is '\r' or '\n')
                {
                    break;
                }
                else if (!quoted && Array.IndexOf(Delimiters, character) is var index and >= 0)
                {
                    counts[index]++;
                }
            }

            return Delimiters[Array.IndexOf(counts, counts.Max())];
        }

        /// <summary>
        /// Reads the records of a CSV text, each as the texts of its cells.
        /// </summary>
        /// <param name="text">The CSV text.</param>
        /// <param name="delimiter">The delimiter of the cells.</param>
        /// <returns>The records, in order.</returns>
        /// <exception cref="InvalidDataException">Thrown when a cell between double quotes is not closed.</exception>
        private static List<List<string>> ReadRecords(string text, char delimiter)
        {
            var records = new List<List<string>>();
            var cells = new List<string>();
            var cell = new StringBuilder();
            var line = 1;
            var index = 0;

            while (index < text.Length)
            {
                var character = text[index];
                var next = index + 1;

                if (character == '"' && cell.Length == 0)
                {
                    next = ReadQuotedCell(text, index, cell, ref line) + 1;
                }
                else if (character == delimiter)
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                }
                else if (character is '\r' or '\n')
                {
                    // A record ends at CRLF, LF or CR.
                    if (character == '\r' && next < text.Length && text[next] == '\n')
                    {
                        next++;
                    }

                    line++;
                    cells.Add(cell.ToString());
                    cell.Clear();
                    records.Add(cells);
                    cells = [];
                }
                else
                {
                    cell.Append(character);
                }

                index = next;
            }

            if (cell.Length > 0 || cells.Count > 0)
            {
                cells.Add(cell.ToString());
                records.Add(cells);
            }

            return records;
        }

        /// <summary>
        /// Reads a cell between double quotes, where a doubled double quote stands for one.
        /// </summary>
        /// <param name="text">The CSV text.</param>
        /// <param name="start">The index of the opening double quote.</param>
        /// <param name="cell">The <see cref="StringBuilder"/> to which the text of the cell is added.</param>
        /// <param name="line">The number of the current line, increased by the line breaks of the cell.</param>
        /// <returns>The index of the closing double quote.</returns>
        /// <exception cref="InvalidDataException">Thrown when the cell is not closed.</exception>
        private static int ReadQuotedCell(string text, int start, StringBuilder cell, ref int line)
        {
            var startLine = line;

            var index = start + 1;

            while (index < text.Length)
            {
                if (text[index] != '"')
                {
                    line += text[index] == '\n' ? 1 : 0;
                    cell.Append(text[index]);
                    index++;
                }
                else if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    cell.Append('"');
                    index += 2;
                }
                else
                {
                    return index;
                }
            }

            throw new InvalidDataException($"The quoted cell that starts on line {startLine} is not closed.");
        }
    }
}
