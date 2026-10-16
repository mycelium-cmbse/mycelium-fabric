// ------------------------------------------------------------------------------------------------
//  <copyright file="DocumentReaderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="DocumentReader"/> class.
    /// </summary>
    [TestFixture]
    public class DocumentReaderTestFixture
    {
        private string documentDirectory;

        private Mock<IDocumentParser> markdownParser;

        private Mock<IDocumentParser> csvParser;

        private DocumentReader reader;

        [SetUp]
        public void SetUp()
        {
            this.documentDirectory = Path.Combine(Path.GetTempPath(), $"mycelium-documents-{Guid.NewGuid()}");

            this.markdownParser = new Mock<IDocumentParser>();
            this.markdownParser.Setup(parser => parser.FormatName).Returns("Markdown or text");
            this.markdownParser.Setup(parser => parser.Extensions).Returns([".md", ".txt"]);

            this.csvParser = new Mock<IDocumentParser>();
            this.csvParser.Setup(parser => parser.FormatName).Returns("CSV");
            this.csvParser.Setup(parser => parser.Extensions).Returns([".csv"]);

            this.reader = new DocumentReader(new Uri(this.documentDirectory), [this.markdownParser.Object, this.csvParser.Object]);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(this.documentDirectory))
            {
                Directory.Delete(this.documentDirectory, true);
            }
        }

        [Test]
        public void VerifyConstructor()
        {
            var directory = new Uri(this.documentDirectory);
            IDocumentParser[] parsers = [this.markdownParser.Object];
            IDocumentParser[] duplicateParsers = [this.markdownParser.Object, this.markdownParser.Object];
            string[] supportedExtensions = [".csv", ".md", ".txt"];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new DocumentReader(null, parsers), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new DocumentReader(directory, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new DocumentReader(new Uri("documents", UriKind.Relative), parsers), Throws.TypeOf<ArgumentException>());
                Assert.That(() => new DocumentReader(new Uri("https://example.com/documents"), parsers), Throws.TypeOf<ArgumentException>());
                Assert.That(() => new DocumentReader(directory, duplicateParsers), Throws.TypeOf<ArgumentException>().With.Message.Contains("'.md'"));
                Assert.That(this.reader.DocumentDirectory, Is.EqualTo(directory));
                Assert.That(this.reader.SupportedExtensions, Is.EqualTo(supportedExtensions));
            }
        }

        [Test]
        public async Task VerifyReadAsync()
        {
            var none = CancellationToken.None;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.reader.ReadAsync(null, none), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.reader.ReadAsync(" ", none), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.reader.ReadAsync("../Spec.md", none), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.reader.ReadAsync(@"Data\Spec.md", none), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.reader.ReadAsync(".Spec.md", none), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));

                Assert.That(() => this.reader.ReadAsync("Spec.pdf", none),
                    Throws.TypeOf<McpException>().With.Message.Contains("is not a supported document (.csv, .md, .txt): convert it"));

                Assert.That(() => this.reader.ReadAsync("Missing.md", none), Throws.TypeOf<McpException>().With.Message.EndsWith("The folder has no supported document."));
            }

            Directory.CreateDirectory(this.documentDirectory);
            await File.WriteAllTextAsync(Path.Combine(this.documentDirectory, "Spec.md"), "# Spec", none);
            await File.WriteAllTextAsync(Path.Combine(this.documentDirectory, "Broken.CSV"), "ID,\"open", none);
            await File.WriteAllTextAsync(Path.Combine(this.documentDirectory, "Spec.pdf"), "%PDF", none);

            IReadOnlyList<DocumentSection> sections = [new DocumentSection(1, 1, "Spec", [])];
            this.markdownParser.Setup(parser => parser.Parse(It.Is<Stream>(content => new StreamReader(content).ReadToEnd() == "# Spec"))).Returns(sections);
            this.csvParser.Setup(parser => parser.Parse(It.IsAny<Stream>())).Throws(new InvalidDataException("The quoted cell that starts on line 1 is not closed."));

            var document = await this.reader.ReadAsync("Spec.md", none);

            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(document.FileName, Is.EqualTo("Spec.md"));
                Assert.That(document.Sections, Is.SameAs(sections));

                Assert.That(() => this.reader.ReadAsync("Broken.CSV", none),
                    Throws.TypeOf<McpException>().With.Message.EqualTo("The file 'Broken.CSV' cannot be read as a CSV document: The quoted cell that starts on line 1 is not closed."));

                Assert.That(() => this.reader.ReadAsync("Missing.md", none),
                    Throws.TypeOf<McpException>().With.Message.Contains($"The document folder ({this.documentDirectory}) has no file named 'Missing.md'.")
                        .And.Message.EndsWith("Its documents are: Broken.CSV, Spec.md."));

                Assert.That(() => this.reader.ReadAsync("Spec.md", cancellation.Token), Throws.InstanceOf<OperationCanceledException>());

                this.markdownParser.Verify(parser => parser.Parse(It.IsAny<Stream>()), Times.Once);
                this.csvParser.Verify(parser => parser.Parse(It.IsAny<Stream>()), Times.Once);
            }
        }
    }
}
