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
        /// <summary>
        /// The <c>ElementId</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string PayloadSubsystemId = "95a8c184-a12e-1125-c0ee-bbe7a025de5b";

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
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(0));
                Assert.That(this.modelProvider.RootElements, Has.Count.EqualTo(0));
                Assert.That(this.modelProvider.GetElementById(PayloadSubsystemId), Is.Null);
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
        public void VerifyElements()
        {
            this.modelProvider.LoadModel(this.satelliteModelPath);

            Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(548));

            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(0));
        }

        [Test]
        public void VerifyRootElements()
        {
            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.RootElements, Has.Count.EqualTo(0));

            this.modelProvider.LoadModel(this.satelliteModelPath);
            var rootElements = this.modelProvider.RootElements;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rootElements, Has.Count.EqualTo(1));
                Assert.That(rootElements[0], Is.InstanceOf<INamespace>());
                Assert.That(rootElements[0].OwningRelationship, Is.Null);
            }
        }

        [Test]
        public void VerifyGetElementById()
        {
            this.modelProvider.LoadModel(this.satelliteModelPath);

            var payloadSubsystem = this.modelProvider.GetElementById(PayloadSubsystemId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.modelProvider.GetElementById(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.modelProvider.GetElementById(" "), Throws.TypeOf<ArgumentException>());
                Assert.That(this.modelProvider.GetElementById("unknown-id"), Is.Null);
                Assert.That(payloadSubsystem, Is.Not.Null);
                Assert.That(payloadSubsystem.ElementId, Is.EqualTo(PayloadSubsystemId));
                Assert.That(payloadSubsystem.DeclaredName, Is.EqualTo("payloadSubsystem"));
            }

            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.GetElementById(PayloadSubsystemId), Is.Null);
        }
    }
}