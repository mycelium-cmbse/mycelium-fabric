// ------------------------------------------------------------------------------------------------
//  <copyright file="DocumentToolsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="DocumentTools"/> class.
    /// </summary>
    [TestFixture]
    public class DocumentToolsTestFixture
    {
        private const string IdPattern = @"REQ-[A-Z]+-\d{3}";

        private Mock<IModelProvider> modelProvider;

        private Mock<IDocumentReader> documentReader;

        private TextDocument document;

        [SetUp]
        public void SetUp()
        {
            using (var content = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "EOSat1-requirements.md")))
            {
                this.document = new TextDocument("EOSat1-requirements.md", new MarkdownDocumentParser().Parse(content));
            }

            // REQ-SYS-001 and REQ-SYS-002 are modelled as requirement usages, REQ-SYS-003 as a requirement definition.
            IReadOnlyList<IElement> elements =
            [
                Mock.Of<IRequirementUsage>(requirement => requirement.ReqId == "REQ-SYS-001"),
                Mock.Of<IRequirementUsage>(requirement => requirement.ReqId == "REQ-SYS-002"),
                Mock.Of<IRequirementDefinition>(requirement => requirement.ReqId == "REQ-SYS-003"),
                Mock.Of<IRequirementUsage>(requirement => requirement.ReqId == "REQ-SYS-006"),
                Mock.Of<IRequirementUsage>(requirement => requirement.ReqId == null),
                new Mock<IElement>().Object
            ];

            this.modelProvider = new Mock<IModelProvider>();
            this.modelProvider.Setup(provider => provider.Elements).Returns(elements);

            this.documentReader = new Mock<IDocumentReader>();
            this.documentReader.Setup(reader => reader.ReadAsync("EOSat1-requirements.md", It.IsAny<CancellationToken>())).ReturnsAsync(this.document);
            this.documentReader.Setup(reader => reader.ReadAsync("Missing.md", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new McpException("The document folder has no file named 'Missing.md'."));

            // A long document: 120 sections, a first one of 20 lines of 1 000 characters, a line longer than a page, a slow line.
            var longSections = Enumerable.Range(1, 120).Select(number => new DocumentSection(number, 1, $"Section {number}", [])).ToList();
            longSections[0] = new DocumentSection(1, 1, "Long section", [.. Enumerable.Range(0, 20).Select(index => $"{index:D2}{new string('x', 998)}")]);
            longSections[1] = new DocumentSection(2, 1, "Long line", [new string('y', 9000), "next"]);
            longSections[2] = new DocumentSection(3, 1, "Slow line", [new string('a', 5000) + "!"]);

            this.documentReader.Setup(reader => reader.ReadAsync("Long.md", It.IsAny<CancellationToken>())).ReturnsAsync(new TextDocument("Long.md", longSections));
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new DocumentTools(null, this.documentReader.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new DocumentTools(this.modelProvider.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new DocumentTools(this.modelProvider.Object, this.documentReader.Object), Throws.Nothing);
            }
        }

        [Test]
        public async Task VerifyReadDocumentAsync()
        {
            var tools = new DocumentTools(this.modelProvider.Object, this.documentReader.Object);
            using var cancellation = new CancellationTokenSource();

            var outline = await tools.ReadDocumentAsync("EOSat1-requirements.md", cancellationToken: cancellation.Token);
            var coveredOutline = await tools.ReadDocumentAsync("EOSat1-requirements.md", idPattern: IdPattern);
            var blankPatternOutline = await tools.ReadDocumentAsync("EOSat1-requirements.md", idPattern: " ");
            var lastOutlinePage = await tools.ReadDocumentAsync("EOSat1-requirements.md", offset: 6);
            var section = await tools.ReadDocumentAsync("EOSat1-requirements.md", 7, IdPattern);
            var uncoveredSection = await tools.ReadDocumentAsync("EOSat1-requirements.md", 4, offset: 2);
            var emptySection = await tools.ReadDocumentAsync("EOSat1-requirements.md", 2, IdPattern);
            int[] lineCounts = [1, 0, 1, 5, 0, 4, 7, 1];
            int?[] requirementCounts = [0, 0, 0, 3, 0, 2, 2, 1];
            int?[] modelledCounts = [0, 0, 0, 3, 0, 0, 0, 0];
            string[] missingIdentifiers = ["REQ-PAY-001", "REQ-PAY-002", "REQ-PAY-003", "REQ-PAY-004", "REQ-GND-001"];
            string[] missingSectionIdentifiers = ["REQ-PAY-003", "REQ-PAY-004"];
            int[] lastSectionNumbers = [7, 8];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(outline.FileName, Is.EqualTo("EOSat1-requirements.md"));
                Assert.That(outline.SectionCount, Is.EqualTo(8));
                Assert.That(outline.Sections.Select(summary => summary.Number), Is.EqualTo(Enumerable.Range(1, 8)));
                Assert.That(outline.Sections[0], Is.EqualTo(new SectionSummary(1, 0, "(text before the first heading)", 1, null, null)));
                Assert.That(outline.Sections[6], Is.EqualTo(new SectionSummary(7, 3, "3.2 Data handling", 7, null, null)));
                Assert.That(outline.Sections.Select(summary => summary.LineCount), Is.EqualTo(lineCounts));
                Assert.That(outline.Section, Is.Null);
                Assert.That(outline.Text, Is.Null);
                Assert.That(outline.Requirements, Is.Null);
                Assert.That(outline.NextOffset, Is.Null);

                Assert.That(coveredOutline.Sections.Select(summary => summary.RequirementCount), Is.EqualTo(requirementCounts));
                Assert.That(coveredOutline.Sections.Select(summary => summary.ModelledCount), Is.EqualTo(modelledCounts));
                Assert.That(coveredOutline.Requirements, Has.Property("Found").EqualTo(8).And.Property("Modelled").EqualTo(3));
                Assert.That(coveredOutline.Requirements.Missing, Is.EqualTo(missingIdentifiers));
                Assert.That(blankPatternOutline.Requirements, Is.Null);
                Assert.That(lastOutlinePage.Sections.Select(summary => summary.Number), Is.EqualTo(lastSectionNumbers));
                Assert.That(lastOutlinePage.NextOffset, Is.Null);

                Assert.That(section.Sections, Is.Null);
                Assert.That(section.Section, Is.EqualTo(new SectionSummary(7, 3, "3.2 Data handling", 7, 2, 0)));
                Assert.That(section.Text, Does.StartWith("REQ-PAY-003: The mass memory").And.EndWith("not a heading.\n```"));
                Assert.That(section.Text.Split('\n'), Has.Length.EqualTo(7));
                Assert.That(section.Requirements, Has.Property("Found").EqualTo(2).And.Property("Modelled").EqualTo(0));
                Assert.That(section.Requirements.Missing, Is.EqualTo(missingSectionIdentifiers));
                Assert.That(section.NextOffset, Is.Null);

                Assert.That(uncoveredSection.Section, Is.EqualTo(new SectionSummary(4, 2, "2. System requirements", 5, null, null)));
                Assert.That(uncoveredSection.Text, Does.StartWith("| REQ-SYS-001 |").And.Contains("| REQ-SYS-003 |"));
                Assert.That(uncoveredSection.Requirements, Is.Null);
                Assert.That(emptySection.Text, Is.Empty);
                Assert.That(emptySection.Requirements, Has.Property("Found").EqualTo(0).And.Property("Missing").Empty);

                this.documentReader.Verify(reader => reader.ReadAsync("EOSat1-requirements.md", cancellation.Token), Times.Once);
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.ReadDocumentAsync("EOSat1-requirements.md", offset: -1), Throws.TypeOf<McpException>().With.Message.Contains("0 or more"));
                Assert.That(() => tools.ReadDocumentAsync("Missing.md"), Throws.TypeOf<McpException>().With.Message.Contains("no file named"));
                Assert.That(() => tools.ReadDocumentAsync("EOSat1-requirements.md", 0),
                    Throws.TypeOf<McpException>().With.Message.Contains("has no section 0: use a number from 1 to 8"));
                Assert.That(() => tools.ReadDocumentAsync("EOSat1-requirements.md", 9), Throws.TypeOf<McpException>().With.Message.Contains("has no section 9"));
                Assert.That(() => tools.ReadDocumentAsync("EOSat1-requirements.md", idPattern: "REQ-["),
                    Throws.TypeOf<McpException>().With.Message.Contains("not a valid regular expression"));
            }

            var longOutlinePage = await tools.ReadDocumentAsync("Long.md");
            var longOutlineLastPage = await tools.ReadDocumentAsync("Long.md", offset: 100);
            var longOutlinePageAfterEnd = await tools.ReadDocumentAsync("Long.md", offset: 200);
            var longFirstPage = await tools.ReadDocumentAsync("Long.md", 1);
            var longLastPage = await tools.ReadDocumentAsync("Long.md", 1, offset: 14);
            var longLinePage = await tools.ReadDocumentAsync("Long.md", 2);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(longOutlinePage.Sections, Has.Count.EqualTo(50));
                Assert.That(longOutlinePage.NextOffset, Is.EqualTo(50));
                Assert.That(longOutlineLastPage.Sections.Select(summary => summary.Number), Is.EqualTo(Enumerable.Range(101, 20)));
                Assert.That(longOutlineLastPage.NextOffset, Is.Null);
                Assert.That(longOutlinePageAfterEnd.Sections, Is.Empty);
                Assert.That(longOutlinePageAfterEnd.NextOffset, Is.Null);

                Assert.That(longFirstPage.Text.Split('\n').Select(line => line[..2]), Is.EqualTo(Enumerable.Range(0, 7).Select(index => $"{index:D2}")));
                Assert.That(longFirstPage.Text, Has.Length.EqualTo(7006));
                Assert.That(longFirstPage.NextOffset, Is.EqualTo(7));
                Assert.That(longLastPage.Text.Split('\n').Select(line => line[..2]), Is.EqualTo(Enumerable.Range(14, 6).Select(index => $"{index:D2}")));
                Assert.That(longLastPage.NextOffset, Is.Null);
                Assert.That(longLinePage.Text, Has.Length.EqualTo(9000));
                Assert.That(longLinePage.NextOffset, Is.EqualTo(1));
                Assert.That(() => tools.ReadDocumentAsync("Long.md", idPattern: "(a+)+$"), Throws.TypeOf<McpException>().With.Message.Contains("takes too long"));
            }
        }
    }
}
