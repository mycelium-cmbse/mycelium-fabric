// ------------------------------------------------------------------------------------------------
//  <copyright file="TextDocumentExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Documents;

    /// <summary>
    /// Extension methods for <see cref="TextDocument"/>: the parts of a document that the tools read.
    /// </summary>
    public static class TextDocumentExtensions
    {
        /// <summary>
        /// The time after which the identifier pattern given by the AI assistant stops matching a line.
        /// </summary>
        private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

        /// <param name="document">The <see cref="TextDocument"/>.</param>
        extension(TextDocument document)
        {
            /// <summary>
            /// Gets a section and its subsections: the sections that follow it with a deeper level, up to the next section of
            /// the same level or higher. The text before the first heading has no subsection.
            /// </summary>
            /// <param name="number">The number of the section, from 1, as the outline gives it.</param>
            /// <returns>The section, then its subsections, in the order of the document.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <c>null</c>.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="number"/> is not the number of a section.</exception>
            public IReadOnlyList<DocumentSection> GetSectionAndSubsections(int number)
            {
                ArgumentNullException.ThrowIfNull(document);
                ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(number, document.Sections.Count);

                var section = document.Sections[number - 1];

                return [section, .. document.Sections.Skip(number).TakeWhile(next => section.Level > 0 && next.Level > section.Level)];
            }

            /// <summary>
            /// Finds the requirement identifiers of each section with the pattern given by the AI assistant, as
            /// <see cref="TextDocument.FindIdentifiers"/> does, with a time limit for each line.
            /// </summary>
            /// <param name="idPattern">The regular expression that matches a requirement identifier.</param>
            /// <returns>The identifiers of each section, in the order of the sections.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <c>null</c>.</exception>
            /// <exception cref="McpException">
            /// Thrown when <paramref name="idPattern"/> is not a valid regular expression, or takes too long to match a line.
            /// </exception>
            public IReadOnlyList<IReadOnlyList<string>> FindIdentifiersMatching(string idPattern)
            {
                ArgumentNullException.ThrowIfNull(document);

                Regex identifierPattern;

                try
                {
                    identifierPattern = new Regex(idPattern, RegexOptions.CultureInvariant, PatternTimeout);
                }
                catch (ArgumentException exception)
                {
                    throw new McpException($"The idPattern '{idPattern}' is not a valid regular expression: {exception.Message}");
                }

                try
                {
                    return document.FindIdentifiers(identifierPattern);
                }
                catch (RegexMatchTimeoutException)
                {
                    throw new McpException($"The idPattern '{idPattern}' takes too long to match the document: write a simpler one, such as REQ-[A-Z]+-\\d{{3}}.");
                }
            }
        }
    }
}
