// ------------------------------------------------------------------------------------------------
//  <copyright file="TextDocumentExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extensions
{
    using System;
    using System.IO;
    using System.Linq;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="TextDocumentExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class TextDocumentExtensionsTestFixture
    {
        private static readonly string[] PayloadTitles = ["3. Payload requirements", "3.1 Camera", "3.2 Data handling"];

        private static readonly string[] SystemIds = ["REQ-SYS-001", "REQ-SYS-002", "REQ-SYS-003"];

        private static readonly string[] GroundIds = ["REQ-GND-001"];

        private static readonly int[] PreambleSection = [1];

        private static readonly int[] SectionWithoutSubsection = [4];

        private static readonly int[] LastSection = [8];

        private TextDocument document;

        [SetUp]
        public void SetUp()
        {
            using var content = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "EOSat1-requirements.md"));
            this.document = new TextDocument("EOSat1-requirements.md", new MarkdownDocumentParser().Parse(content));
        }

        [Test]
        public void VerifyGetSectionAndSubsections()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.document.GetSectionAndSubsections(5).Select(section => section.Title), Is.EqualTo(PayloadTitles));
                Assert.That(this.document.GetSectionAndSubsections(4).Select(section => section.Number), Is.EqualTo(SectionWithoutSubsection));
                Assert.That(this.document.GetSectionAndSubsections(2).Select(section => section.Number), Is.EqualTo(Enumerable.Range(2, 7)));
                Assert.That(this.document.GetSectionAndSubsections(1).Select(section => section.Number), Is.EqualTo(PreambleSection));
                Assert.That(this.document.GetSectionAndSubsections(8).Select(section => section.Number), Is.EqualTo(LastSection));
                Assert.That(() => this.document.GetSectionAndSubsections(0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => this.document.GetSectionAndSubsections(9), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => ((TextDocument)null).GetSectionAndSubsections(1), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public void VerifyFindIdentifiersMatching()
        {
            var identifiers = this.document.FindIdentifiersMatching(@"REQ-[A-Z]+-\d{3}");
            var slowDocument = new TextDocument("Slow.md", [new DocumentSection(1, 1, "Slow line", [new string('a', 5000) + "!"])]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(identifiers, Has.Count.EqualTo(8));
                Assert.That(identifiers[3], Is.EqualTo(SystemIds));
                Assert.That(identifiers[7], Is.EqualTo(GroundIds));
                Assert.That(() => this.document.FindIdentifiersMatching("REQ-["), Throws.TypeOf<McpException>().With.Message.Contains("not a valid regular expression"));
                Assert.That(() => slowDocument.FindIdentifiersMatching("(a+)+$"), Throws.TypeOf<McpException>().With.Message.Contains("takes too long"));
                Assert.That(() => ((TextDocument)null).FindIdentifiersMatching("REQ"), Throws.TypeOf<ArgumentNullException>());
            }
        }
    }
}
