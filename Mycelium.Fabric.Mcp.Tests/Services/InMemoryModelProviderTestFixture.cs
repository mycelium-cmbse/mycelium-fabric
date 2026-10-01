// ------------------------------------------------------------------------------------------------
//  <copyright file="InMemoryModelProviderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System;
    using System.IO;

    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Root.Namespaces;

    /// <summary>
    /// Suite of tests for the <see cref="InMemoryModelProvider"/> class.
    /// </summary>
    [TestFixture]
    public class InMemoryModelProviderTestFixture
    {
        private string dataDirectory;

        private Uri satelliteModelPath;

        private Uri emptyModelPath;

        private InMemoryModelProvider modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.dataDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
            this.satelliteModelPath = new Uri(Path.Combine(this.dataDirectory, "Satellite.json"));
            this.emptyModelPath = new Uri(Path.Combine(this.dataDirectory, "Empty.json"));

            this.modelProvider = new InMemoryModelProvider();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.modelProvider.GetElements(), Has.Count.EqualTo(0));
                Assert.That(this.modelProvider.GetRootElements(), Has.Count.EqualTo(0));
            }
        }

        [Test]
        public void VerifyLoadModel()
        {
            var missingModelPath = new Uri(Path.Combine(this.dataDirectory, "Missing.json"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.modelProvider.LoadModel(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.modelProvider.LoadModel(new Uri("Satellite.json", UriKind.Relative)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.LoadModel(new Uri("https://example.com/Satellite.json")), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.LoadModel(missingModelPath), Throws.TypeOf<FileNotFoundException>());
                Assert.That(() => this.modelProvider.LoadModel(this.satelliteModelPath), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyGetElements()
        {
            this.modelProvider.LoadModel(this.satelliteModelPath);

            Assert.That(this.modelProvider.GetElements(), Has.Count.EqualTo(548));

            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.GetElements(), Has.Count.EqualTo(0));
        }

        [Test]
        public void VerifyGetRootElements()
        {
            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.GetRootElements(), Has.Count.EqualTo(0));

            this.modelProvider.LoadModel(this.satelliteModelPath);
            var rootElements = this.modelProvider.GetRootElements();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rootElements, Has.Count.EqualTo(1));
                Assert.That(rootElements[0], Is.InstanceOf<INamespace>());
                Assert.That(rootElements[0].OwningRelationship, Is.Null);
            }
        }
    }
}