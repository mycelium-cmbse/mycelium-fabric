// ------------------------------------------------------------------------------------------------
//  <copyright file="ExcelDocumentParser.cs" company="Starion Group S.A.">
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
    using DocumentFormat.OpenXml.Spreadsheet;

    /// <summary>
    /// The <see cref="IDocumentParser"/> of Excel workbooks (<c>.xlsx</c>): one section per worksheet, titled with its name,
    /// and one line <c>| cell | cell |</c> per row that is not empty. The cells keep their column, so that the columns stay
    /// aligned from row to row.
    /// </summary>
    public sealed partial class ExcelDocumentParser : IDocumentParser
    {
        /// <summary>
        /// The built-in number formats of Excel that show a percentage: <c>0%</c> and <c>0.00%</c>.
        /// </summary>
        private static readonly HashSet<uint> PercentFormats = [9, 10];

        /// <summary>
        /// The built-in number formats of Excel that show a date: from <c>m/d/yyyy</c> (14) to <c>m/d/yy h:mm</c> (22).
        /// </summary>
        private static readonly HashSet<uint> DateFormats = [14, 15, 16, 17, 22];

        /// <summary>
        /// The letters of a custom number format that show a date: the year and the day.
        /// </summary>
        private static readonly char[] DateLetters = ['y', 'Y', 'd', 'D'];

        /// <summary>
        /// Gets the name of the format: <c>Excel</c>.
        /// </summary>
        public string FormatName => "Excel";

        /// <summary>
        /// Gets the extension of the files of this format: <c>.xlsx</c>.
        /// </summary>
        public IReadOnlyCollection<string> Extensions { get; } = [".xlsx"];

        /// <summary>
        /// Splits an Excel workbook into one section per worksheet, in the order of the tabs. A cell gives the value that
        /// Excel stores, the result for a formula; a percentage is written with <c>%</c> and a date as <c>yyyy-MM-dd</c>.
        /// </summary>
        /// <param name="content">The content of the <c>.xlsx</c> file.</param>
        /// <returns>The sections of the workbook, numbered from 1 in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidDataException">Thrown when the content is not an Excel workbook.</exception>
        public IReadOnlyList<DocumentSection> Parse(Stream content)
        {
            ArgumentNullException.ThrowIfNull(content);

            try
            {
                using var workbook = SpreadsheetDocument.Open(content, false);
                var workbookPart = workbook.WorkbookPart ?? throw new InvalidDataException("The file has no workbook part.");
                var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(GetText).ToList() ?? [];
                var cellFormats = workbookPart.WorkbookStylesPart?.Stylesheet?.CellFormats?.Elements<CellFormat>().ToList() ?? [];
                var customFormats = workbookPart.WorkbookStylesPart?.Stylesheet?.NumberingFormats?.Elements<NumberingFormat>()
                    .Where(format => format.NumberFormatId?.Value != null)
                    .ToDictionary(format => format.NumberFormatId.Value, format => format.FormatCode?.Value ?? string.Empty) ?? [];

                var builder = new SectionListBuilder();

                foreach (var sheet in workbookPart.Workbook?.Sheets?.Elements<Sheet>() ?? [])
                {
                    // A chart sheet has no cells: only the worksheets are sections.
                    if (sheet.Id?.Value is not { } partId || workbookPart.GetPartById(partId) is not WorksheetPart worksheetPart)
                    {
                        continue;
                    }

                    builder.StartSection(1, sheet.Name?.Value ?? string.Empty);

                    foreach (var row in worksheetPart.Worksheet?.GetFirstChild<SheetData>()?.Elements<Row>() ?? [])
                    {
                        var cells = GetCells(row, sharedStrings, cellFormats, customFormats);

                        if (cells.Count > 0)
                        {
                            builder.AddLine(MarkdownTable.FormatRow(cells));
                        }
                    }
                }

                return builder.Build();
            }
            catch (Exception exception) when (exception is OpenXmlPackageException or FileFormatException)
            {
                throw new InvalidDataException(exception.Message, exception);
            }
        }

        /// <summary>
        /// Gets the texts of the cells of a row, each at the position of its column, without the empty cells at the end.
        /// </summary>
        /// <param name="row">The <see cref="Row"/>.</param>
        /// <param name="sharedStrings">The shared strings of the workbook.</param>
        /// <param name="cellFormats">The cell formats of the workbook.</param>
        /// <param name="customFormats">The format codes of the custom number formats, by identifier.</param>
        /// <returns>The texts of the cells, or an empty list when the row is empty.</returns>
        private static List<string> GetCells(Row row, List<string> sharedStrings, List<CellFormat> cellFormats, Dictionary<uint, string> customFormats)
        {
            var cells = new List<string>();

            foreach (var cell in row.Elements<Cell>())
            {
                var column = GetColumnIndex(cell.CellReference?.Value) ?? cells.Count;

                while (cells.Count < column)
                {
                    cells.Add(string.Empty);
                }

                cells.Add(GetValue(cell, sharedStrings, cellFormats, customFormats));
            }

            var lastValue = cells.FindLastIndex(text => !string.IsNullOrWhiteSpace(text));

            return cells[..(lastValue + 1)];
        }

        /// <summary>
        /// Gets the text of a cell: a shared or inline string, <c>TRUE</c> or <c>FALSE</c> for a boolean, or its stored
        /// value, written as a percentage or a date when its number format shows one.
        /// </summary>
        /// <param name="cell">The <see cref="Cell"/>.</param>
        /// <param name="sharedStrings">The shared strings of the workbook.</param>
        /// <param name="cellFormats">The cell formats of the workbook.</param>
        /// <param name="customFormats">The format codes of the custom number formats, by identifier.</param>
        /// <returns>The text of the cell, trimmed.</returns>
        private static string GetValue(Cell cell, List<string> sharedStrings, List<CellFormat> cellFormats, Dictionary<uint, string> customFormats)
        {
            var value = cell.CellValue?.Text ?? string.Empty;
            var dataType = cell.DataType?.Value;

            if (dataType == CellValues.SharedString)
            {
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;
            }

            if (dataType == CellValues.InlineString)
            {
                return cell.InlineString == null ? string.Empty : GetText(cell.InlineString);
            }

            if (dataType == CellValues.Boolean)
            {
                return value == "1" ? "TRUE" : "FALSE";
            }

            if ((dataType == null || dataType == CellValues.Number) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return FormatNumber(number, GetNumberFormat(cell, cellFormats, customFormats));
            }

            return value.Trim();
        }

        /// <summary>
        /// Gets the number format of a cell: its built-in identifier, and the format code of a custom format.
        /// </summary>
        /// <param name="cell">The <see cref="Cell"/>.</param>
        /// <param name="cellFormats">The cell formats of the workbook.</param>
        /// <param name="customFormats">The format codes of the custom number formats, by identifier.</param>
        /// <returns>The identifier of the number format, 0 for <c>General</c>, and its format code or an empty text.</returns>
        private static (uint Id, string Code) GetNumberFormat(Cell cell, List<CellFormat> cellFormats, Dictionary<uint, string> customFormats)
        {
            var styleIndex = (int)(cell.StyleIndex?.Value ?? 0);
            var formatId = styleIndex < cellFormats.Count ? cellFormats[styleIndex].NumberFormatId?.Value ?? 0 : 0;

            return (formatId, customFormats.GetValueOrDefault(formatId, string.Empty));
        }

        /// <summary>
        /// Writes a number as Excel shows it for the formats that change its meaning: a percentage (0.2 is 20%) or a date
        /// (46308 is 2026-10-13). Any other number is written in its shortest form, with a dot.
        /// </summary>
        /// <param name="number">The stored number.</param>
        /// <param name="format">The number format of the cell.</param>
        /// <returns>The text of the number.</returns>
        private static string FormatNumber(double number, (uint Id, string Code) format)
        {
            // The quoted texts, the colors and conditions in brackets and the escaped characters of a format code are not formats.
            var code = LiteralFormatParts().Replace(format.Code, string.Empty);

            if (PercentFormats.Contains(format.Id) || code.Contains('%'))
            {
                return $"{Math.Round(number * 100, 10).ToString(CultureInfo.InvariantCulture)}%";
            }

            if ((DateFormats.Contains(format.Id) || code.IndexOfAny(DateLetters) >= 0) && number is >= 1 and < 2958466)
            {
                var date = DateTime.FromOADate(number);

                return date.TimeOfDay == TimeSpan.Zero
                    ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }

            return number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the visible text of a shared or inline string: its text runs, without its phonetic guide.
        /// </summary>
        /// <param name="item">The shared string item or inline string.</param>
        /// <returns>The text, trimmed.</returns>
        private static string GetText(OpenXmlElement item)
        {
            return string.Concat(item.Descendants<Text>().Where(text => !text.Ancestors<PhoneticRun>().Any()).Select(text => text.Text)).Trim();
        }

        /// <summary>
        /// Gets the index of the column of a cell from its reference: 0 for <c>A1</c>, 1 for <c>B7</c>, 26 for <c>AA3</c>.
        /// </summary>
        /// <param name="reference">The reference of the cell, or <c>null</c>.</param>
        /// <returns>The index of the column, or <c>null</c> when the cell has no reference.</returns>
        private static int? GetColumnIndex(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return null;
            }

            var letters = reference.TakeWhile(char.IsAsciiLetter).Select(char.ToUpperInvariant).ToList();

            return letters.Count == 0 ? null : letters.Aggregate(0, (index, letter) => (index * 26) + letter - 'A' + 1) - 1;
        }

        /// <summary>
        /// Matches the parts of a number format code that are shown as they are: quoted texts, brackets and escaped characters.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of the literal parts of a format code.</returns>
        [GeneratedRegex(@"""[^""]*""|\[[^\]]*\]|\\.", RegexOptions.None, 1000)]
        private static partial Regex LiteralFormatParts();
    }
}
