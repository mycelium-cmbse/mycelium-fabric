// ------------------------------------------------------------------------------------------------
//  <copyright file="MarkdownDocumentParser.cs" company="Starion Group S.A.">
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
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The <see cref="IDocumentParser"/> of Markdown and plain text files, split at their Markdown headings (<c>#</c> to
    /// <c>######</c>). A plain text file has no heading: it is one section.
    /// </summary>
    public sealed partial class MarkdownDocumentParser : IDocumentParser
    {
        /// <summary>
        /// Gets the name of the format: <c>Markdown or text</c>.
        /// </summary>
        public string FormatName => "Markdown or text";

        /// <summary>
        /// Gets the extensions of the files of this format: <c>.md</c>, <c>.markdown</c> and <c>.txt</c>.
        /// </summary>
        public IReadOnlyCollection<string> Extensions { get; } = [".md", ".markdown", ".txt"];

        /// <summary>
        /// Splits a Markdown or text document into sections at its headings. A line that starts with <c>#</c> inside a
        /// fenced code block is not a heading.
        /// </summary>
        /// <param name="content">The content of the file, in UTF-8 unless it starts with another byte order mark.</param>
        /// <returns>The sections of the document, numbered from 1 in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <c>null</c>.</exception>
        public IReadOnlyList<DocumentSection> Parse(Stream content)
        {
            ArgumentNullException.ThrowIfNull(content);

            using var reader = new StreamReader(content, Encoding.UTF8, true, -1, true);
            var builder = new SectionListBuilder();
            char? openFence = null;

            while (reader.ReadLine() is { } line)
            {
                var heading = openFence == null ? Heading().Match(line) : Match.Empty;

                if (heading.Success)
                {
                    builder.StartSection(heading.Groups["marks"].Length, heading.Groups["title"].Value);
                    continue;
                }

                var fence = Fence().Match(line);

                if (fence.Success)
                {
                    // A block opened by backticks is only closed by backticks, and a block opened by tildes by tildes.
                    var marker = fence.Value[^1];

                    if (openFence == null)
                    {
                        openFence = marker;
                    }
                    else if (openFence == marker)
                    {
                        openFence = null;
                    }
                }

                builder.AddLine(line);
            }

            return builder.Build();
        }

        /// <summary>
        /// Matches a Markdown heading: up to three spaces, one to six <c>#</c>, a space and the title, without the optional
        /// closing <c>#</c>.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of a Markdown heading.</returns>
        [GeneratedRegex(@"^ {0,3}(?<marks>#{1,6})[ \t]+(?<title>.*?)(?:[ \t]+#+)?[ \t]*$", RegexOptions.None, 1000)]
        private static partial Regex Heading();

        /// <summary>
        /// Matches the start of a line that opens or closes a fenced code block: up to three spaces and at least three
        /// backticks or tildes.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of a code fence.</returns>
        [GeneratedRegex("^ {0,3}(?:`{3}|~{3})", RegexOptions.None, 1000)]
        private static partial Regex Fence();
    }
}
