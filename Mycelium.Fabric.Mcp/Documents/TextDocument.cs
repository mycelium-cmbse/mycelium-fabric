// ------------------------------------------------------------------------------------------------
//  <copyright file="TextDocument.cs" company="Starion Group S.A.">
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
    using System.Text.RegularExpressions;

    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// A text document read by an <see cref="IDocumentReader"/>, split into sections at its headings.
    /// </summary>
    /// <param name="FileName">The name of the file of the document.</param>
    /// <param name="Sections">The <see cref="DocumentSection"/>s of the document, in order.</param>
    public sealed record TextDocument(string FileName, IReadOnlyList<DocumentSection> Sections)
    {
        /// <summary>
        /// The characters that can come before a requirement identifier on the line that defines it: white space, the markers
        /// of tables, lists, quotes and emphasis, and a numbering such as <c>3.2.1</c> or <c>1)</c>.
        /// </summary>
        private const string DefinitionPrefixCharacters = " \t|*_-+>#[(`~.)0123456789";

        /// <summary>
        /// Finds the requirement identifiers of each section: the texts that match the pattern in its title or in its lines.
        /// An identifier belongs to the first section that defines it, that is where it starts a line, a table row or the
        /// title, so that a reference made before or after the definition is not counted. An identifier that no line
        /// defines, which the document only mentions, belongs to the first section that mentions it.
        /// </summary>
        /// <param name="identifierPattern">The <see cref="Regex"/> that matches a requirement identifier.</param>
        /// <returns>
        /// The identifiers of each section, in the order of the sections, each list in the order of the first appearances in
        /// the section.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="identifierPattern"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="RegexMatchTimeoutException">
        /// Thrown when <paramref name="identifierPattern"/> takes longer than its timeout to match a line.
        /// </exception>
        public IReadOnlyList<IReadOnlyList<string>> FindIdentifiers(Regex identifierPattern)
        {
            ArgumentNullException.ThrowIfNull(identifierPattern);

            var mentions = this.Sections
                .SelectMany((section, sectionIndex) => section.Lines.Prepend(section.Title)
                    .SelectMany(line => identifierPattern.Matches(line)
                        .Where(match => match.Length > 0)
                        .Select(match => (SectionIndex: sectionIndex, Identifier: match.Value, IsDefinition: IsDefinition(line, match)))))
                .ToList();

            var owningSections = mentions
                .GroupBy(mention => mention.Identifier, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.FirstOrDefault(mention => mention.IsDefinition, group.First()).SectionIndex, StringComparer.Ordinal);

            var mentionsBySection = mentions.ToLookup(mention => mention.SectionIndex);

            return
            [
                .. this.Sections.Select((_, sectionIndex) => (IReadOnlyList<string>)
                [
                    .. mentionsBySection[sectionIndex]
                        .Where(mention => owningSections[mention.Identifier] == sectionIndex)
                        .Select(mention => mention.Identifier)
                        .Distinct(StringComparer.Ordinal)
                ])
            ];
        }

        /// <summary>
        /// Tells whether a match of an identifier defines it: only white space, markers and a numbering come before it on its
        /// line.
        /// </summary>
        /// <param name="line">The line, or the title, that holds the match.</param>
        /// <param name="match">The <see cref="Match"/> of the identifier.</param>
        /// <returns><c>true</c> when the identifier starts the line; otherwise <c>false</c>.</returns>
        private static bool IsDefinition(string line, Match match)
        {
            return line.AsSpan(0, match.Index).TrimStart(DefinitionPrefixCharacters).IsEmpty;
        }
    }
}
