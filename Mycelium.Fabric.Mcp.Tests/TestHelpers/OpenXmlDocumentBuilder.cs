// ------------------------------------------------------------------------------------------------
//  <copyright file="OpenXmlDocumentBuilder.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;

    using W = DocumentFormat.OpenXml.Wordprocessing;
    using X = DocumentFormat.OpenXml.Spreadsheet;

    /// <summary>
    /// Builds small Word and Excel documents in memory, to test the cases that the EOSat-1 test files do not hold.
    /// </summary>
    public static class OpenXmlDocumentBuilder
    {
        /// <summary>
        /// Builds a Word document with the given styles and blocks.
        /// </summary>
        /// <param name="styles">The styles of the document.</param>
        /// <param name="blocks">The paragraphs, tables and content controls of the body, in order.</param>
        /// <returns>The <see cref="MemoryStream"/> of the <c>.docx</c> file, at its start.</returns>
        public static MemoryStream CreateWordDocument(IEnumerable<W.Style> styles, params OpenXmlElement[] blocks)
        {
            var content = new MemoryStream();

            using (var document = WordprocessingDocument.Create(content, WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(styles);
                mainPart.Document = new W.Document(new W.Body(blocks));
            }

            content.Position = 0;

            return content;
        }

        /// <summary>
        /// Builds a paragraph style.
        /// </summary>
        /// <param name="styleId">The identifier of the style.</param>
        /// <param name="name">The name of the style.</param>
        /// <param name="basedOn">The identifier of the style it is based on, or <c>null</c>.</param>
        /// <param name="outlineLevel">Its outline level, from 0 for a heading 1, or <c>null</c>.</param>
        /// <returns>The <see cref="W.Style"/>.</returns>
        public static W.Style CreateStyle(string styleId, string name, string basedOn = null, int? outlineLevel = null)
        {
            var style = new W.Style(new W.StyleName { Val = name }) { Type = W.StyleValues.Paragraph, StyleId = styleId };

            if (basedOn != null)
            {
                style.Append(new W.BasedOn { Val = basedOn });
            }

            if (outlineLevel != null)
            {
                style.Append(new W.StyleParagraphProperties(new W.OutlineLevel { Val = outlineLevel }));
            }

            return style;
        }

        /// <summary>
        /// Builds a paragraph of one run of text.
        /// </summary>
        /// <param name="text">The text of the paragraph.</param>
        /// <param name="styleId">The identifier of its style, or <c>null</c>.</param>
        /// <returns>The <see cref="W.Paragraph"/>.</returns>
        public static W.Paragraph CreateParagraph(string text, string styleId = null)
        {
            var paragraph = new W.Paragraph();

            if (styleId != null)
            {
                paragraph.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleId }));
            }

            paragraph.Append(new W.Run(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));

            return paragraph;
        }

        /// <summary>
        /// Builds an Excel workbook with the given number formats, cell formats, shared strings and worksheets.
        /// </summary>
        /// <param name="customFormats">The custom number formats.</param>
        /// <param name="cellFormats">The cell formats, referenced by the style index of the cells.</param>
        /// <param name="sharedStrings">The shared strings, referenced by the index in the cells of type shared string.</param>
        /// <param name="sheets">The worksheets: their name and rows, in the order of the tabs.</param>
        /// <returns>The <see cref="MemoryStream"/> of the <c>.xlsx</c> file, at its start.</returns>
        public static MemoryStream CreateWorkbook(IEnumerable<X.NumberingFormat> customFormats, IEnumerable<X.CellFormat> cellFormats, IEnumerable<X.SharedStringItem> sharedStrings,
            params (string Name, X.Row[] Rows)[] sheets)
        {
            var content = new MemoryStream();

            using (var workbook = SpreadsheetDocument.Create(content, SpreadsheetDocumentType.Workbook))
            {
                var workbookPart = workbook.AddWorkbookPart();
                var sheetList = new X.Sheets();
                workbookPart.Workbook = new X.Workbook(sheetList);
                workbookPart.AddNewPart<WorkbookStylesPart>().Stylesheet = new X.Stylesheet(new X.NumberingFormats(customFormats), new X.CellFormats(cellFormats));
                workbookPart.AddNewPart<SharedStringTablePart>().SharedStringTable = new X.SharedStringTable(sharedStrings);

                foreach (var (sheet, index) in sheets.Select((sheet, index) => (sheet, index)))
                {
                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    worksheetPart.Worksheet = new X.Worksheet(new X.SheetData(sheet.Rows));
                    sheetList.Append(new X.Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = (uint)index + 1, Name = sheet.Name });
                }
            }

            content.Position = 0;

            return content;
        }
    }
}
