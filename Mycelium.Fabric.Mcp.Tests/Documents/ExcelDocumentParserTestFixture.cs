// ------------------------------------------------------------------------------------------------
//  <copyright file="ExcelDocumentParserTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Documents
{
    using System;
    using System.IO;
    using System.Linq;

    using DocumentFormat.OpenXml.Spreadsheet;

    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    /// <summary>
    /// Suite of tests for the <see cref="ExcelDocumentParser"/> class.
    /// </summary>
    [TestFixture]
    public class ExcelDocumentParserTestFixture
    {
        private string dataDirectory;

        private ExcelDocumentParser parser;

        [SetUp]
        public void SetUp()
        {
            this.dataDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
            this.parser = new ExcelDocumentParser();
        }

        [Test]
        public void VerifyParse()
        {
            // EOSat1-requirements.xlsx: shared strings, a 0% margin, a short date, a formula with its result, a boolean, a
            // cell alone in column D, and a chart sheet after the three worksheets.
            using var content = File.OpenRead(Path.Combine(this.dataDirectory, "EOSat1-requirements.xlsx"));
            var sections = this.parser.Parse(content);
            string[] extensions = [".xlsx"];
            string[] titles = ["System", "Payload", "Notes"];

            string[] systemLines =
            [
                "| ID | Requirement | Priority | Limit | Margin |",
                "| REQ-SYS-001 | The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall not exceed 150 kg. | H | 150 | 20% |",
                "| REQ-SYS-002 | The payload (optical camera and mass memory) shall consume less than 70 W in imaging mode. | H | 70 |",
                "| REQ-SYS-003 | The AOCS shall provide an attitude knowledge accuracy better than 10 arcsec, using at least two star trackers for redundancy. | H | 10 |"
            ];

            string[] notesLines =
            [
                "| The ground station follows REQ-GND-001, defined in the ground segment specification. |",
                "| Reviewed on | 2026-10-13 |",
                "| Mass limit with margin | 180 |",
                "| Approved | TRUE |",
                "|  |  |  | Only in column D |"
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.parser.Parse(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.parser.FormatName, Is.EqualTo("Excel"));
                Assert.That(this.parser.Extensions, Is.EqualTo(extensions));
                Assert.That(sections.Select(section => section.Title), Is.EqualTo(titles));
                Assert.That(sections.Select(section => section.Level), Has.All.EqualTo(1));
                Assert.That(sections[0].Lines, Is.EqualTo(systemLines));
                Assert.That(sections[1].Lines, Has.Count.EqualTo(5));
                Assert.That(sections[1].Lines[3], Is.EqualTo(@"| REQ-PAY-003 | The mass memory shall store at least 512 Gbit of images. Capacity \| end of life. | H |"));
                Assert.That(sections[2].Lines, Is.EqualTo(notesLines));
            }

            NumberingFormat[] customFormats =
            [
                new() { NumberFormatId = 164, FormatCode = "0.0%" },
                new() { NumberFormatId = 165, FormatCode = "dd/mm/yyyy" },
                new() { NumberFormatId = 166, FormatCode = "\"days\" 0" },
                new() { NumberFormatId = 167, FormatCode = "[Red]0.00" }
            ];

            CellFormat[] cellFormats = [new() { NumberFormatId = 0 }, new() { NumberFormatId = 164 }, new() { NumberFormatId = 165 }, new() { NumberFormatId = 166 },
                new() { NumberFormatId = 14 }, new() { NumberFormatId = 167 }, new() { NumberFormatId = 10 }];

            SharedStringItem[] sharedStrings =
            [
                new(new Run(new Text("Rich ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }), new Run(new Text("text"))),
                new(new Text("漢字"), new PhoneticRun(new Text("かんじ")) { BaseTextStartIndex = 0, EndingBaseIndex = 2 })
            ];

            Row[] rows =
            [
                new(Shared("A1", "0"), Shared("B1", "1"), Shared("C1", "99"), Shared("D1", "x")) { RowIndex = 1 },
                new(Inline("A2", "Inline"), Number("B2", "0.125", 1), Number("C2", "46308", 2), Number("D2", "7", 3), Number("E2", "46308.5", 4), Number("F2", "0.5", 4),
                    Number("G2", "-3.5", 5), Number("H2", "0.5", 6)) { RowIndex = 2 },
                new(Inline(null, "first"), Inline(null, "second")) { RowIndex = 3 },
                new(new Cell { CellReference = "A4", DataType = CellValues.Error, CellValue = new CellValue("#DIV/0!") },
                    new Cell { CellReference = "B4", DataType = CellValues.String, CellFormula = new CellFormula("\"a\"&\"bc\""), CellValue = new CellValue("abc") }) { RowIndex = 4 },
                new(Inline("A5", "   "), new Cell { CellReference = "B5" }) { RowIndex = 5 },
                new(Number("A6", "42", 99)) { RowIndex = 6 }
            ];

            using var edgeCases = OpenXmlDocumentBuilder.CreateWorkbook(customFormats, cellFormats, sharedStrings, ("Edge", rows), ("Empty", []));
            var edgeSections = this.parser.Parse(edgeCases);
            string[] edgeLines = ["| Rich text | 漢字 |", "| Inline | 12.5% | 2026-10-13 | 7 | 2026-10-13 12:00 | 0.5 | -3.5 | 50% |", "| first | second |", "| #DIV/0! | abc |", "| 42 |"];
            string[] edgeTitles = ["Edge", "Empty"];

            using var document = File.OpenRead(Path.Combine(this.dataDirectory, "EOSat1-requirements.docx"));
            using var notAZip = new MemoryStream([1, 2, 3, 4]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(edgeSections.Select(section => section.Title), Is.EqualTo(edgeTitles));
                Assert.That(edgeSections[0].Lines, Is.EqualTo(edgeLines));
                Assert.That(edgeSections[1].Lines, Is.Empty);
                Assert.That(() => this.parser.Parse(document), Throws.TypeOf<InvalidDataException>());
                Assert.That(() => this.parser.Parse(notAZip), Throws.InstanceOf<InvalidDataException>());
            }
        }

        /// <summary>
        /// Builds a cell that refers to a shared string.
        /// </summary>
        /// <param name="reference">The reference of the cell.</param>
        /// <param name="index">The index of the shared string, as stored.</param>
        /// <returns>The <see cref="Cell"/>.</returns>
        private static Cell Shared(string reference, string index)
        {
            return new Cell { CellReference = reference, DataType = CellValues.SharedString, CellValue = new CellValue(index) };
        }

        /// <summary>
        /// Builds a cell of an inline string.
        /// </summary>
        /// <param name="reference">The reference of the cell, or <c>null</c>.</param>
        /// <param name="text">The text of the cell.</param>
        /// <returns>The <see cref="Cell"/>.</returns>
        private static Cell Inline(string reference, string text)
        {
            return new Cell { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)) };
        }

        /// <summary>
        /// Builds a cell of a number.
        /// </summary>
        /// <param name="reference">The reference of the cell.</param>
        /// <param name="value">The number, as stored.</param>
        /// <param name="styleIndex">The index of the cell format.</param>
        /// <returns>The <see cref="Cell"/>.</returns>
        private static Cell Number(string reference, string value, uint styleIndex)
        {
            return new Cell { CellReference = reference, CellValue = new CellValue(value), StyleIndex = styleIndex };
        }
    }
}
