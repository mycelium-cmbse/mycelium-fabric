// ------------------------------------------------------------------------------------------------
//  <copyright file="WordDocumentParserTestFixture.cs" company="Starion Group S.A.">
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

    using DocumentFormat.OpenXml.Wordprocessing;

    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    /// <summary>
    /// Suite of tests for the <see cref="WordDocumentParser"/> class.
    /// </summary>
    [TestFixture]
    public class WordDocumentParserTestFixture
    {
        private string dataDirectory;

        private WordDocumentParser parser;

        [SetUp]
        public void SetUp()
        {
            this.dataDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
            this.parser = new WordDocumentParser();
        }

        [Test]
        public void VerifyParse()
        {
            // EOSat1-requirements.docx has the styles of a French Word: Titre1 to Titre3, named heading 1 to heading 3.
            using var content = File.OpenRead(Path.Combine(this.dataDirectory, "EOSat1-requirements.docx"));
            var sections = this.parser.Parse(content);
            string[] extensions = [".docx"];
            int[] levels = [0, 1, 2, 2, 2, 3, 3, 2];
            int[] lineCounts = [1, 0, 1, 4, 0, 3, 2, 1];

            string[] titles =
            [
                string.Empty, "EOSat-1 System Requirements", "1. Introduction", "2. System requirements", "3. Payload requirements", "3.1 Camera", "3.2 Data handling",
                "4. Ground segment"
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.parser.Parse(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.parser.FormatName, Is.EqualTo("Word"));
                Assert.That(this.parser.Extensions, Is.EqualTo(extensions));
                Assert.That(sections.Select(section => section.Level), Is.EqualTo(levels));
                Assert.That(sections.Select(section => section.Title), Is.EqualTo(titles));
                Assert.That(sections.Select(section => section.Lines.Count), Is.EqualTo(lineCounts));
                Assert.That(sections[3].Lines[0], Is.EqualTo("| ID | Requirement | Priority |"));
                Assert.That(sections[3].Lines[1], Does.StartWith("| REQ-SYS-001 | The satellite launch mass").And.EndWith("shall not exceed 150 kg. | H |"));
                Assert.That(sections[6].Lines[0], Is.EqualTo("REQ-PAY-003: The mass memory shall store at least 512 Gbit of images."));
            }

            var styles = new[]
            {
                OpenXmlDocumentBuilder.CreateStyle("Normal", "Normal"),
                OpenXmlDocumentBuilder.CreateStyle("Titre2", "heading 2", "Normal", 1),
                OpenXmlDocumentBuilder.CreateStyle("ExigenceTitre", "Requirement heading", "Titre2"),
                OpenXmlDocumentBuilder.CreateStyle("Heading4", "Heading 4", "Normal"),
                OpenXmlDocumentBuilder.CreateStyle("Corps7", "Outline 7", "Normal", 6),
                OpenXmlDocumentBuilder.CreateStyle("Loop1", "Loop 1", "Loop2"),
                OpenXmlDocumentBuilder.CreateStyle("Loop2", "Loop 2", "Loop1")
            };

            var explicitOutline = new Paragraph(new ParagraphProperties(new OutlineLevel { Val = 0 }), new Run(new Text("Explicit outline")));
            var contentControl = new SdtBlock(new SdtContentBlock(OpenXmlDocumentBuilder.CreateParagraph("Inside a content control")));
            var trackedChanges = new Paragraph(new Run(new Text("Kept ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }),
                new DeletedRun(new Run(new DeletedText("removed "))), new Run(new FieldCode(" PAGE ")), new Run(new Text("text")));

            var table = new Table(new TableRow(
                new TableCell(OpenXmlDocumentBuilder.CreateParagraph("a"), OpenXmlDocumentBuilder.CreateParagraph("b")),
                new TableCell(OpenXmlDocumentBuilder.CreateParagraph("x|y")),
                new TableCell(new Paragraph())));

            using var edgeCases = OpenXmlDocumentBuilder.CreateWordDocument(styles,
                explicitOutline,
                OpenXmlDocumentBuilder.CreateParagraph("Based on heading 2", "ExigenceTitre"),
                OpenXmlDocumentBuilder.CreateParagraph("Outline level 7 is body text", "Corps7"),
                OpenXmlDocumentBuilder.CreateParagraph("Style loop", "Loop1"),
                OpenXmlDocumentBuilder.CreateParagraph("Named heading 4", "Heading4"),
                OpenXmlDocumentBuilder.CreateParagraph(" ", "Titre2"),
                contentControl,
                trackedChanges,
                table,
                OpenXmlDocumentBuilder.CreateParagraph("Unknown style", "Missing"));

            var edgeSections = this.parser.Parse(edgeCases);
            string[] edgeTitles = ["Explicit outline", "Based on heading 2", "Named heading 4"];
            int[] edgeLevels = [1, 2, 4];
            string[] bodyLines = ["Outline level 7 is body text", "Style loop"];
            string[] lastLines = ["Inside a content control", "Kept text", @"| a b | x\|y |  |", "Unknown style"];

            using var workbook = File.OpenRead(Path.Combine(this.dataDirectory, "EOSat1-requirements.xlsx"));
            using var notAZip = new MemoryStream([1, 2, 3, 4]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(edgeSections.Select(section => section.Title), Is.EqualTo(edgeTitles));
                Assert.That(edgeSections.Select(section => section.Level), Is.EqualTo(edgeLevels));
                Assert.That(edgeSections[0].Lines, Is.Empty);
                Assert.That(edgeSections[1].Lines, Is.EqualTo(bodyLines));
                Assert.That(edgeSections[2].Lines, Is.EqualTo(lastLines));
                Assert.That(() => this.parser.Parse(workbook), Throws.TypeOf<InvalidDataException>());
                Assert.That(() => this.parser.Parse(notAZip), Throws.InstanceOf<InvalidDataException>());
            }
        }
    }
}
