// ------------------------------------------------------------------------------------------------
//  <copyright file="MarkdownDocumentParserTestFixture.cs" company="Starion Group S.A.">
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
    using System.Text;

    using Mycelium.Fabric.Mcp.Documents;

    /// <summary>
    /// Suite of tests for the <see cref="MarkdownDocumentParser"/> class.
    /// </summary>
    [TestFixture]
    public class MarkdownDocumentParserTestFixture
    {
        private MarkdownDocumentParser parser;

        [SetUp]
        public void SetUp()
        {
            this.parser = new MarkdownDocumentParser();
        }

        [Test]
        public void VerifyParse()
        {
            using var content = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "EOSat1-requirements.md"));
            var sections = this.parser.Parse(content);
            string[] extensions = [".md", ".markdown", ".txt"];
            int[] levels = [0, 1, 2, 2, 2, 3, 3, 2];
            int[] lineCounts = [1, 0, 1, 5, 0, 4, 7, 1];

            string[] titles =
            [
                string.Empty, "EOSat-1 System Requirements", "1. Introduction", "2. System requirements", "3. Payload requirements", "3.1 Camera", "3.2 Data handling",
                "4. Ground segment"
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.parser.Parse(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.parser.FormatName, Is.EqualTo("Markdown or text"));
                Assert.That(this.parser.Extensions, Is.EqualTo(extensions));
                Assert.That(content.CanRead, Is.True);
                Assert.That(sections.Select(section => section.Number), Is.EqualTo(Enumerable.Range(1, 8)));
                Assert.That(sections.Select(section => section.Level), Is.EqualTo(levels));
                Assert.That(sections.Select(section => section.Title), Is.EqualTo(titles));
                Assert.That(sections.Select(section => section.Lines.Count), Is.EqualTo(lineCounts));
                Assert.That(sections[0].Lines[0], Does.StartWith("Requirements of the EOSat-1"));
                Assert.That(sections[3].Lines[2], Does.StartWith("| REQ-SYS-001 |"));
                Assert.That(sections[6].Lines[0], Is.EqualTo("REQ-PAY-003: The mass memory shall store at least 512 Gbit of images."));
                Assert.That(sections[6].Lines[1], Is.Empty);
                Assert.That(sections[6].Lines[5], Is.EqualTo("# This line is in a code block, not a heading."));
                Assert.That(sections[7].Lines[0], Does.Contain("REQ-GND-001"));
            }

            var plainSections = this.parser.Parse(ToStream("\nline one\n\nline two\n\n", false));
            var emptySections = this.parser.Parse(ToStream(string.Empty, false));
            var fencedSections = this.parser.Parse(ToStream("# A\n~~~\n## Not a heading\n```\n## Still not a heading\n~~~\n   ## B ##\n#hashtag\n####### Seven\n##\n", false));
            var markedSections = this.parser.Parse(ToStream("# Été\r\nligne", true));
            string[] plainLines = ["line one", string.Empty, "line two"];
            string[] fencedLines = ["~~~", "## Not a heading", "```", "## Still not a heading", "~~~"];
            string[] unheadedLines = ["#hashtag", "####### Seven", "##"];
            string[] fencedTitles = ["A", "B"];
            string[] markedLines = ["ligne"];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(plainSections, Has.Count.EqualTo(1));
                Assert.That(plainSections[0], Has.Property("Level").EqualTo(0).And.Property("Title").Empty);
                Assert.That(plainSections[0].Lines, Is.EqualTo(plainLines));
                Assert.That(emptySections, Is.Empty);
                Assert.That(fencedSections.Select(section => section.Title), Is.EqualTo(fencedTitles));
                Assert.That(fencedSections[0].Lines, Is.EqualTo(fencedLines));
                Assert.That(fencedSections[1].Lines, Is.EqualTo(unheadedLines));
                Assert.That(markedSections[0], Has.Property("Title").EqualTo("Été"));
                Assert.That(markedSections[0].Lines, Is.EqualTo(markedLines));
            }
        }

        /// <summary>
        /// Writes a text to a stream in UTF-8.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="withByteOrderMark">Whether the stream starts with the UTF-8 byte order mark.</param>
        /// <returns>The <see cref="MemoryStream"/>, at its start.</returns>
        private static MemoryStream ToStream(string text, bool withByteOrderMark)
        {
            return new MemoryStream([.. new UTF8Encoding(withByteOrderMark).GetPreamble(), .. Encoding.UTF8.GetBytes(text)]);
        }
    }
}
