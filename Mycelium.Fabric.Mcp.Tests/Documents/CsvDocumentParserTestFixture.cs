// ------------------------------------------------------------------------------------------------
//  <copyright file="CsvDocumentParserTestFixture.cs" company="Starion Group S.A.">
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
    using System.Text;

    using Mycelium.Fabric.Mcp.Documents;

    /// <summary>
    /// Suite of tests for the <see cref="CsvDocumentParser"/> class.
    /// </summary>
    [TestFixture]
    public class CsvDocumentParserTestFixture
    {
        private CsvDocumentParser parser;

        [SetUp]
        public void SetUp()
        {
            this.parser = new CsvDocumentParser();
        }

        [Test]
        public void VerifyParse()
        {
            // EOSat1-requirements.csv: quoted cells with commas, a line break, a '|' and doubled double quotes, empty cells at
            // the end of the records, and a record of empty cells.
            using var content = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "EOSat1-requirements.csv"));
            var sections = this.parser.Parse(content);
            string[] extensions = [".csv"];

            string[] lines =
            [
                "| ID | Requirement | Priority | Limit | Margin |",
                "| REQ-SYS-001 | The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall not exceed 150 kg. | H | 150 | 20% |",
                "| REQ-SYS-002 | The payload (optical camera and mass memory) shall consume less than 70 W in imaging mode. | H | 70 |",
                "| REQ-SYS-003 | The AOCS shall provide an attitude knowledge accuracy better than 10 arcsec, using at least two star trackers for redundancy. | H | 10 |",
                "| REQ-PAY-001 | The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km. | H | 5 |",
                "| REQ-PAY-002 | The camera mass shall not exceed 40 kg, as allocated by REQ-SYS-001. | M | 40 |",
                @"| REQ-PAY-003 | The mass memory shall store at least 512 Gbit of images. Capacity \| end of life. | H |",
                "| REQ-PAY-004 | The images shall be downlinked within a \"responsive\" time (target TBD). | L |"
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.parser.Parse(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.parser.FormatName, Is.EqualTo("CSV"));
                Assert.That(this.parser.Extensions, Is.EqualTo(extensions));
                Assert.That(sections, Has.Count.EqualTo(1));
                Assert.That(sections[0].Number, Is.EqualTo(1));
                Assert.That(sections[0].Level, Is.EqualTo(0));
                Assert.That(sections[0].Title, Is.Empty);
                Assert.That(sections[0].Lines, Is.EqualTo(lines));
            }

            // A semicolon file with a byte order mark and CRLF, a blank line, a quote inside a cell, and no line break at the end.
            using var semicolons = Open("﻿Name;Text\r\nA;\"He said \"\"go\"\"\r\nnow; really\"\r\n\r\n;;\r\n  B  ; x ;;\r\nC\"D", Encoding.UTF8);
            string[] semicolonLines = ["| Name | Text |", "| A | He said \"go\" now; really |", "| B | x |", "| C\"D |"];

            using var tabs = Open("ID\tText\nX\t1,5\rY\t\"\"\n", Encoding.UTF8);
            string[] tabLines = ["| ID | Text |", "| X | 1,5 |", "| Y |"];

            using var empty = Open(string.Empty, Encoding.UTF8);
            using var unclosed = Open("ID,Text\nX,\"open\nstill open", Encoding.UTF8);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.parser.Parse(semicolons)[0].Lines, Is.EqualTo(semicolonLines));
                Assert.That(this.parser.Parse(tabs)[0].Lines, Is.EqualTo(tabLines));
                Assert.That(this.parser.Parse(empty), Is.Empty);
                Assert.That(() => this.parser.Parse(unclosed), Throws.TypeOf<InvalidDataException>().With.Message.EqualTo("The quoted cell that starts on line 2 is not closed."));
            }
        }

        /// <summary>
        /// Opens a text as the content of a file.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="encoding">The <see cref="Encoding"/> of the content.</param>
        /// <returns>The <see cref="MemoryStream"/> of the content.</returns>
        private static MemoryStream Open(string text, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(text));
        }
    }
}
