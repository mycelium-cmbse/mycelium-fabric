// ------------------------------------------------------------------------------------------------
//  <copyright file="WordDocumentParser.cs" company="Starion Group S.A.">
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
    using System.Text.RegularExpressions;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Wordprocessing;

    /// <summary>
    /// The <see cref="IDocumentParser"/> of Word documents (<c>.docx</c>), split at their headings: the paragraphs whose
    /// style is a heading (Heading 1 to Heading 6, in any language of Word) or that have an outline level. A table row is one
    /// line <c>| cell | cell |</c>.
    /// </summary>
    public sealed partial class WordDocumentParser : IDocumentParser
    {
        /// <summary>
        /// The highest level of a heading.
        /// </summary>
        private const int MaximumLevel = 6;

        /// <summary>
        /// The maximum number of styles followed through <c>basedOn</c> to find the outline level of a style.
        /// </summary>
        private const int MaximumStyleDepth = 10;

        /// <summary>
        /// Gets the name of the format: <c>Word</c>.
        /// </summary>
        public string FormatName => "Word";

        /// <summary>
        /// Gets the extension of the files of this format: <c>.docx</c>.
        /// </summary>
        public IReadOnlyCollection<string> Extensions { get; } = [".docx"];

        /// <summary>
        /// Splits a Word document into sections at its headings. The paragraphs and tables are read in the order of the
        /// document, including those inside content controls; the deleted text of tracked changes is left out.
        /// </summary>
        /// <param name="content">The content of the <c>.docx</c> file.</param>
        /// <returns>The sections of the document, numbered from 1 in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidDataException">Thrown when the content is not a Word document.</exception>
        public IReadOnlyList<DocumentSection> Parse(Stream content)
        {
            ArgumentNullException.ThrowIfNull(content);

            try
            {
                using var document = WordprocessingDocument.Open(content, false);
                var mainPart = document.MainDocumentPart ?? throw new InvalidDataException("The file has no main document part.");
                var styles = mainPart.StyleDefinitionsPart?.Styles?.Elements<Style>().Where(style => style.StyleId?.Value != null)
                    .GroupBy(style => style.StyleId.Value, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal) ?? [];

                var builder = new SectionListBuilder();

                if (mainPart.Document?.Body is { } body)
                {
                    AddBlocks(body, styles, builder);
                }

                return builder.Build();
            }
            catch (Exception exception) when (exception is OpenXmlPackageException or FileFormatException)
            {
                throw new InvalidDataException(exception.Message, exception);
            }
        }

        /// <summary>
        /// Adds the paragraphs and the tables of a container of blocks to the sections, in order.
        /// </summary>
        /// <param name="container">The body, or the content of a content control.</param>
        /// <param name="styles">The styles of the document by identifier.</param>
        /// <param name="builder">The <see cref="SectionListBuilder"/> of the document.</param>
        private static void AddBlocks(OpenXmlElement container, Dictionary<string, Style> styles, SectionListBuilder builder)
        {
            foreach (var element in container.ChildElements)
            {
                switch (element)
                {
                    case Paragraph paragraph:
                        AddParagraph(paragraph, styles, builder);
                        break;
                    case Table table:
                        AddTable(table, builder);
                        break;
                    case SdtBlock contentControl when contentControl.SdtContentBlock is { } blockContent:
                        AddBlocks(blockContent, styles, builder);
                        break;
                }
            }
        }

        /// <summary>
        /// Adds a paragraph to the sections: a heading starts a section, any other paragraph is a line.
        /// </summary>
        /// <param name="paragraph">The <see cref="Paragraph"/>.</param>
        /// <param name="styles">The styles of the document by identifier.</param>
        /// <param name="builder">The <see cref="SectionListBuilder"/> of the document.</param>
        private static void AddParagraph(Paragraph paragraph, Dictionary<string, Style> styles, SectionListBuilder builder)
        {
            var text = GetText(paragraph);
            var level = GetHeadingLevel(paragraph, styles);

            if (level > 0 && !string.IsNullOrWhiteSpace(text))
            {
                builder.StartSection(level, text);
            }
            else
            {
                builder.AddLine(text);
            }
        }

        /// <summary>
        /// Adds the rows of a table to the sections, one line per row.
        /// </summary>
        /// <param name="table">The <see cref="Table"/>.</param>
        /// <param name="builder">The <see cref="SectionListBuilder"/> of the document.</param>
        private static void AddTable(Table table, SectionListBuilder builder)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>().Select(cell => string.Join(' ', cell.Descendants<Paragraph>().Select(GetText).Where(text => text.Length > 0)));

                builder.AddLine(MarkdownTable.FormatRow(cells));
            }
        }

        /// <summary>
        /// Gets the visible text of a paragraph: its text runs, without deleted text nor field codes.
        /// </summary>
        /// <param name="paragraph">The <see cref="Paragraph"/>.</param>
        /// <returns>The text of the paragraph, trimmed.</returns>
        private static string GetText(Paragraph paragraph)
        {
            return string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)).Trim();
        }

        /// <summary>
        /// Gets the heading level of a paragraph: its own outline level, else the one of its style or of the styles it is
        /// based on, else the number of a built-in heading style, whose name stays <c>heading 1</c> in every language.
        /// </summary>
        /// <param name="paragraph">The <see cref="Paragraph"/>.</param>
        /// <param name="styles">The styles of the document by identifier.</param>
        /// <returns>The level, from 1 to 6, or 0 when the paragraph is not a heading.</returns>
        private static int GetHeadingLevel(Paragraph paragraph, Dictionary<string, Style> styles)
        {
            var properties = paragraph.ParagraphProperties;

            if (properties?.OutlineLevel?.Val?.Value is { } outlineLevel)
            {
                return ToLevel(outlineLevel);
            }

            var styleId = properties?.ParagraphStyleId?.Val?.Value;

            for (var depth = 0; depth < MaximumStyleDepth && styleId != null && styles.TryGetValue(styleId, out var style); depth++)
            {
                if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } styleOutlineLevel)
                {
                    return ToLevel(styleOutlineLevel);
                }

                if (HeadingStyleName().Match(style.StyleName?.Val?.Value ?? string.Empty) is { Success: true } heading)
                {
                    return int.Parse(heading.Groups["level"].Value, CultureInfo.InvariantCulture);
                }

                styleId = style.BasedOn?.Val?.Value;
            }

            return 0;
        }

        /// <summary>
        /// Converts a Word outline level, from 0 for a heading 1 to 8, or 9 for body text, to a heading level.
        /// </summary>
        /// <param name="outlineLevel">The outline level.</param>
        /// <returns>The heading level, from 1 to 6, or 0 for body text and the levels below 6.</returns>
        private static int ToLevel(int outlineLevel)
        {
            return outlineLevel is >= 0 and < MaximumLevel ? outlineLevel + 1 : 0;
        }

        /// <summary>
        /// Matches the name of a built-in heading style, from <c>heading 1</c> to <c>heading 6</c>, whatever the case.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of a heading style name.</returns>
        [GeneratedRegex("^heading (?<level>[1-6])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
        private static partial Regex HeadingStyleName();
    }
}
