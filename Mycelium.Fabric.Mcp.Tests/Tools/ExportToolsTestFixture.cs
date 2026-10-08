// ------------------------------------------------------------------------------------------------
//  <copyright file="ExportToolsTestFixture.cs" company="Starion Group S.A.">
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

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Suite of tests for the <see cref="ExportTools"/> class.
    /// </summary>
    [TestFixture]
    public class ExportToolsTestFixture
    {
        private Mock<IModelProvider> modelProvider;

        private Mock<IModelExporter> modelExporter;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
            this.modelExporter = new Mock<IModelExporter>();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ExportTools(null, this.modelExporter.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ExportTools(this.modelProvider.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ExportTools(this.modelProvider.Object, this.modelExporter.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyExportModel()
        {
            IReadOnlyList<IElement> elements = [new Mock<IElement>().Object, new Mock<IElement>().Object];
            this.modelProvider.Setup(provider => provider.Elements).Returns(elements);
            this.modelExporter.Setup(exporter => exporter.Export(elements, "EOSat1")).Returns("exports/EOSat1.json");
            this.modelExporter.Setup(exporter => exporter.Export(elements, null)).Returns("exports/model-20261008-120000.json");
            this.modelExporter.Setup(exporter => exporter.Export(elements, "EOSat1.json")).Throws(new McpException("The export folder already has a file named 'EOSat1.json'."));

            var tools = new ExportTools(this.modelProvider.Object, this.modelExporter.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(tools.ExportModel("EOSat1"), Is.EqualTo(new ExportResult("exports/EOSat1.json", 2)));
                Assert.That(tools.ExportModel(), Is.EqualTo(new ExportResult("exports/model-20261008-120000.json", 2)));
                Assert.That(() => tools.ExportModel("EOSat1.json"), Throws.TypeOf<McpException>().With.Message.Contains("already has a file"));
            }

            this.modelExporter.Verify(exporter => exporter.Export(elements, It.IsAny<string>()), Times.Exactly(3));
        }
    }
}
