// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonModelExporterTestFixture.cs" company="Starion Group S.A.">
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
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// Suite of tests for the <see cref="JsonModelExporter"/> class.
    /// </summary>
    [TestFixture]
    public class JsonModelExporterTestFixture
    {
        private string satelliteModelPath;

        private string exportDirectory;

        private InMemoryModelProvider modelProvider;

        private JsonModelExporter exporter;

        [SetUp]
        public void SetUp()
        {
            this.satelliteModelPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json");
            this.exportDirectory = Path.Combine(Path.GetTempPath(), $"mycelium-export-{Guid.NewGuid()}");

            this.modelProvider = new InMemoryModelProvider(new Mock<IModelChangeApplier>().Object);
            this.modelProvider.LoadModel(new Uri(this.satelliteModelPath));

            this.exporter = new JsonModelExporter(new Uri(this.exportDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(this.exportDirectory))
            {
                Directory.Delete(this.exportDirectory, true);
            }
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new JsonModelExporter(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new JsonModelExporter(new Uri("exports", UriKind.Relative)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => new JsonModelExporter(new Uri("https://example.com/exports")), Throws.TypeOf<ArgumentException>());
                Assert.That(this.exporter.ExportDirectory, Is.EqualTo(new Uri(this.exportDirectory)));
            }
        }

        [Test]
        public void VerifyExport()
        {
            var elements = this.modelProvider.Elements;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.exporter.Export(null, "EOSat1"), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.exporter.Export(elements, "../EOSat1"), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.exporter.Export(elements, "exports/EOSat1"), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.exporter.Export(elements, @"exports\EOSat1"), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.exporter.Export(elements, ".EOSat1"), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(() => this.exporter.Export(elements, new string('a', 101)), Throws.TypeOf<McpException>().With.Message.Contains("not a simple name"));
                Assert.That(Directory.Exists(this.exportDirectory), Is.False);
            }

            var path = this.exporter.Export(elements, "EOSat1");
            var defaultPath = this.exporter.Export([], " ");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(path, Is.EqualTo(Path.Combine(this.exportDirectory, "EOSat1.json")));
                Assert.That(Serialize(ReadDtos(path)), Is.EqualTo(Serialize(ReadDtos(this.satelliteModelPath))));
                Assert.That(Path.GetFileName(defaultPath), Does.Match(@"^model-\d{8}-\d{6}\.json$"));
                Assert.That(ReadDtos(defaultPath), Has.Count.EqualTo(0));
                Assert.That(() => this.exporter.Export(elements, "EOSat1.json"), Throws.TypeOf<McpException>().With.Message.Contains("already has a file named 'EOSat1.json'"));
            }
        }

        /// <summary>
        /// Reads the DTOs of the elements stored in a JSON file of the Systems Modeling API.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The DTOs of the elements.</returns>
        private static List<DtoElement> ReadDtos(string path)
        {
            using var stream = File.OpenRead(path);

            return new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();
        }

        /// <summary>
        /// Serializes DTOs in the order of their <c>Id</c>, to compare two models whatever the order of their elements.
        /// </summary>
        /// <param name="dtos">The DTOs to serialize.</param>
        /// <returns>The JSON of the DTOs.</returns>
        private static string Serialize(IEnumerable<DtoElement> dtos)
        {
            using var stream = new MemoryStream();
            new Serializer().Serialize(dtos.OrderBy(dto => dto.Id), SerializationModeKind.JSON, false, stream, new JsonWriterOptions());

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
