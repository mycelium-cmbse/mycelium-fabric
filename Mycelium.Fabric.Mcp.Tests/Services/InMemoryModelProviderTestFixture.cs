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
        private string satelliteModelFilePath;

        private string emptyModelFilePath;

        [SetUp]
        public void SetUp()
        {
            var dataDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");

            this.satelliteModelFilePath = Path.Combine(dataDirectory, "Satellite.json");
            this.emptyModelFilePath = Path.Combine(dataDirectory, "Empty.json");
        }

        [Test]
        public void VerifyConstructor()
        {
            Assert.That(() => new InMemoryModelProvider(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new InMemoryModelProvider(" "), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new InMemoryModelProvider("Missing.json"), Throws.TypeOf<FileNotFoundException>());
            Assert.That(() => new InMemoryModelProvider(this.satelliteModelFilePath), Throws.Nothing);
        }

        [Test]
        public void VerifyGetElements()
        {
            var modelProvider = new InMemoryModelProvider(this.emptyModelFilePath);

            Assert.That(modelProvider.GetElements(), Has.Count.EqualTo(0));

            modelProvider = new InMemoryModelProvider(this.satelliteModelFilePath);

            Assert.That(modelProvider.GetElements(), Has.Count.EqualTo(548));
        }

        [Test]
        public void VerifyGetRootElements()
        {
            var modelProvider = new InMemoryModelProvider(this.emptyModelFilePath);

            Assert.That(modelProvider.GetRootElements(), Has.Count.EqualTo(0));

            modelProvider = new InMemoryModelProvider(this.satelliteModelFilePath);
            var rootElements = modelProvider.GetRootElements();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rootElements, Has.Count.EqualTo(1));
                Assert.That(rootElements[0], Is.InstanceOf<INamespace>());
                Assert.That(rootElements[0].OwningRelationship, Is.Null);
            }
        }
    }
}