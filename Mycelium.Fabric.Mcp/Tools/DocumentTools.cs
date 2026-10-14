// ------------------------------------------------------------------------------------------------
//  <copyright file="DocumentTools.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// The MCP tools that let an AI assistant read a long document, such as a specification, one piece at a time, and see
    /// which of its requirements the model already has.
    /// </summary>
    [McpServerToolType]
    public class DocumentTools
    {
        /// <summary>
        /// The maximum number of sections in one page of the outline.
        /// </summary>
        private const int MaximumOutlineCount = 50;

        /// <summary>
        /// The maximum number of characters of one page of the text of a section, about 2 000 tokens, so that a small language
        /// model can read it. A page holds whole lines: a longer line is returned alone.
        /// </summary>
        private const int MaximumPageLength = 8000;

        /// <summary>
        /// The maximum number of missing requirement identifiers listed in a <see cref="RequirementCoverage"/>.
        /// </summary>
        private const int MaximumMissingCount = 50;

        /// <summary>
        /// The title shown for the text before the first heading, which has none.
        /// </summary>
        private const string UntitledSectionTitle = "(text before the first heading)";

        /// <summary>
        /// The time after which the identifier pattern given by the AI assistant stops matching a line.
        /// </summary>
        private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="IDocumentReader"/> that reads the documents.
        /// </summary>
        private readonly IDocumentReader documentReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="documentReader">The <see cref="IDocumentReader"/> that reads the documents.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> or <paramref name="documentReader"/> is <c>null</c>.
        /// </exception>
        public DocumentTools(IModelProvider modelProvider, IDocumentReader documentReader)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(documentReader);

            this.modelProvider = modelProvider;
            this.documentReader = documentReader;
        }

        /// <summary>
        /// Reads a document of the document folder piece by piece: one page of its outline, or one page of one of its
        /// sections, with the requirement identifiers that it holds and that the model does not have yet.
        /// </summary>
        /// <param name="fileName">The simple name of the file in the document folder.</param>
        /// <param name="section">The number of the section to read, or <c>null</c> for the outline.</param>
        /// <param name="idPattern">
        /// The regular expression that matches a requirement identifier of the document, or <c>null</c> to count no
        /// requirement.
        /// </param>
        /// <param name="offset">The number of outline entries, or of lines of the section, to skip.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> of the request, canceled when the client cancels it. It is not a parameter of the
        /// tool.
        /// </param>
        /// <returns>A <see cref="Task"/> whose result is the <see cref="DocumentContent"/> of the requested page.</returns>
        /// <exception cref="McpException">
        /// Thrown when the document cannot be read, when <paramref name="section"/> is not the number of a section, when
        /// <paramref name="offset"/> is negative, or when <paramref name="idPattern"/> is not a valid regular expression or is
        /// too slow.
        /// </exception>
        [McpServerTool(Name = "read_document", ReadOnly = true, OpenWorld = false)]
        [Description("Reads a document of the document folder (Markdown, text, Word .docx or Excel .xlsx) piece by piece, so that a long specification is never read at once. "
            + "Without section: the outline, one entry per heading with its number, level, title and line count. With section: one page of the text of that section, without its subsections; an Excel worksheet is a section and a table row a line | cell | cell |. "
            + "With idPattern, a regular expression that matches the requirement identifiers of the document (for example REQ-[A-Z]+-\\d{3}), it also counts the identifiers found "
            + "and the ones already modelled (a requirement of the model has this reqId), and lists the missing ones. An identifier belongs to the section where it starts a line or a table row, else to the first section that mentions it. "
            + "The results are paged: when nextOffset is not null, call the tool again with this offset.")]
        [return: Description("The file name and section count, then either one page of the outline or the read section with one page of its text; "
            + "with idPattern, the identifiers found, modelled and missing (the first 50) in the document or the section; and the offset of the next page (null on the last page).")]
        public async Task<DocumentContent> ReadDocumentAsync(
            [Description("The simple name of the file in the document folder, with its extension, for example 'EOSat1-requirements.md'.")] string fileName,
            [Description("Optional: the number of the section to read, as given by the outline. By default: the outline.")] int? section = null,
            [Description("Optional: a regular expression that matches a requirement identifier of the document, for example SSS-[A-Z]+-[A-Z0-9]+-[A-Z0-9]+.")] string idPattern = null,
            [Description("The number of outline entries, or of lines of the section, to skip: 0 for the first page, then the nextOffset of the previous result.")] int offset = 0,
            [Description("Bound by the SDK to the cancellation of the request, not part of the tool schema.")] CancellationToken cancellationToken = default)
        {
            if (offset < 0)
            {
                throw new McpException("The offset must be 0 or more.");
            }

            var document = await this.documentReader.ReadAsync(fileName, cancellationToken);

            if (section is { } number && (number < 1 || number > document.Sections.Count))
            {
                throw new McpException($"The document '{document.FileName}' has no section {number}: use a number from 1 to {document.Sections.Count}, as given by the outline.");
            }

            var identifiers = string.IsNullOrWhiteSpace(idPattern) ? null : FindIdentifiers(document, idPattern);
            var modelledIdentifiers = identifiers == null ? null : this.GetModelledIdentifiers();

            if (section == null)
            {
                var outline = document.Sections
                    .Skip(offset)
                    .Take(MaximumOutlineCount)
                    .Select(documentSection => Summarize(documentSection, identifiers?[documentSection.Number - 1], modelledIdentifiers))
                    .ToList();

                var nextOutlineOffset = offset + outline.Count < document.Sections.Count ? offset + outline.Count : (int?)null;
                var documentCoverage = Cover(identifiers?.SelectMany(sectionIdentifiers => sectionIdentifiers).ToList(), modelledIdentifiers);

                return new DocumentContent(document.FileName, document.Sections.Count, outline, null, null, documentCoverage, nextOutlineOffset);
            }

            var readSection = document.Sections[section.Value - 1];
            var readIdentifiers = identifiers?[section.Value - 1];
            var (text, nextOffset) = GetPage(readSection.Lines, offset);

            return new DocumentContent(document.FileName, document.Sections.Count, null, Summarize(readSection, readIdentifiers, modelledIdentifiers), text,
                Cover(readIdentifiers, modelledIdentifiers), nextOffset);
        }

        /// <summary>
        /// Finds the requirement identifiers of each section of a document with the pattern given by the AI assistant.
        /// </summary>
        /// <param name="document">The <see cref="TextDocument"/> to search.</param>
        /// <param name="idPattern">The regular expression that matches a requirement identifier.</param>
        /// <returns>The identifiers of each section, in the order of the sections.</returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="idPattern"/> is not a valid regular expression, or takes too long to match a line.
        /// </exception>
        private static IReadOnlyList<IReadOnlyList<string>> FindIdentifiers(TextDocument document, string idPattern)
        {
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

        /// <summary>
        /// Gets one page of the lines of a section: whole lines from the offset, as many as fit in
        /// <see cref="MaximumPageLength"/> characters, and always at least one.
        /// </summary>
        /// <param name="lines">The lines of the section.</param>
        /// <param name="offset">The number of lines to skip.</param>
        /// <returns>The text of the page, its lines separated by line feeds, and the offset of the next page or <c>null</c>.</returns>
        private static (string Text, int? NextOffset) GetPage(IReadOnlyList<string> lines, int offset)
        {
            var text = new StringBuilder();
            var index = offset;

            while (index < lines.Count && (index == offset || text.Length + 1 + lines[index].Length <= MaximumPageLength))
            {
                if (index > offset)
                {
                    text.Append('\n');
                }

                text.Append(lines[index]);
                index++;
            }

            return (text.ToString(), index < lines.Count ? index : null);
        }

        /// <summary>
        /// Summarizes a section for the outline.
        /// </summary>
        /// <param name="section">The <see cref="DocumentSection"/> to summarize.</param>
        /// <param name="identifiers">The requirement identifiers of the section, or <c>null</c> when none are searched.</param>
        /// <param name="modelledIdentifiers">The identifiers of the requirements of the model, or <c>null</c>.</param>
        /// <returns>The <see cref="SectionSummary"/> of the section.</returns>
        private static SectionSummary Summarize(DocumentSection section, IReadOnlyList<string> identifiers, HashSet<string> modelledIdentifiers)
        {
            var title = section.Level == 0 ? UntitledSectionTitle : section.Title;

            return new SectionSummary(section.Number, section.Level, title, section.Lines.Count, identifiers?.Count, identifiers?.Count(modelledIdentifiers.Contains));
        }

        /// <summary>
        /// Tells how many of the given requirement identifiers the model has, and which ones it does not have.
        /// </summary>
        /// <param name="identifiers">The requirement identifiers found, or <c>null</c> when none are searched.</param>
        /// <param name="modelledIdentifiers">The identifiers of the requirements of the model, or <c>null</c>.</param>
        /// <returns>The <see cref="RequirementCoverage"/>, or <c>null</c> when no identifier is searched.</returns>
        private static RequirementCoverage Cover(IReadOnlyList<string> identifiers, HashSet<string> modelledIdentifiers)
        {
            if (identifiers == null)
            {
                return null;
            }

            var missingIdentifiers = identifiers.Where(identifier => !modelledIdentifiers.Contains(identifier)).ToList();

            return new RequirementCoverage(identifiers.Count, identifiers.Count - missingIdentifiers.Count, [.. missingIdentifiers.Take(MaximumMissingCount)]);
        }

        /// <summary>
        /// Gets the identifiers of the requirements of the model: the <c>reqId</c> of its requirement definitions and usages.
        /// </summary>
        /// <returns>The identifiers, compared character by character.</returns>
        private HashSet<string> GetModelledIdentifiers()
        {
            var elements = this.modelProvider.Elements;

            return new HashSet<string>(elements.OfType<IRequirementUsage>().Select(requirement => requirement.ReqId)
                .Concat(elements.OfType<IRequirementDefinition>().Select(requirement => requirement.ReqId))
                .Where(identifier => !string.IsNullOrWhiteSpace(identifier)), StringComparer.Ordinal);
        }
    }
}
