// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementExtensionsTestFixture.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="ElementExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ElementExtensionsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        private IElement payloadSubsystem;

        private IElement camera;

        [SetUp]
        public void SetUp()
        {
            var modelProvider = new InMemoryModelProvider();
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));

            this.payloadSubsystem = modelProvider.GetElementById(PayloadSubsystemId);
            this.camera = modelProvider.GetElementById(CameraId);
        }

        [Test]
        public void VerifyGetTypeNames()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).GetTypeNames(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new Package().GetTypeNames(), Is.Empty);
                Assert.That(new PartUsage().GetTypeNames(), Is.Empty);
                Assert.That(this.camera.GetTypeNames(), Is.EqualTo(["OpticalCamera"]));
            }
        }

        [Test]
        public void VerifyDescribeType()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).DescribeType(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new Package().DescribeType(), Is.EqualTo("Package"));
                Assert.That(this.camera.DescribeType(), Is.EqualTo("PartUsage : OpticalCamera"));
            }
        }

        [Test]
        public void VerifyGetDocumentationBodies()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).GetDocumentationBodies(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.camera.GetDocumentationBodies(), Is.Null);
                Assert.That(this.payloadSubsystem.GetDocumentationBodies(), Is.EqualTo("Payload: the imaging instrument and its data storage."));
            }
        }

        [Test]
        public void VerifyGetLiteralValue()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).GetLiteralValue(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new Package().GetLiteralValue(), Is.Null);
                Assert.That(new LiteralInteger { Value = 55 }.GetLiteralValue(), Is.EqualTo(55));
                Assert.That(new LiteralRational { Value = 1.5 }.GetLiteralValue(), Is.EqualTo(1.5));
            }
        }

        [Test]
        public void VerifyGetAttributeUsages()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).GetAttributeUsages(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new Package().GetAttributeUsages(), Is.Empty);
                Assert.That(this.payloadSubsystem.GetAttributeUsages(), Is.Empty);
                Assert.That(this.camera.GetAttributeUsages().Select(attribute => attribute.DeclaredName), Is.EqualTo(["mass", "power"]));
            }
        }

        [Test]
        public void VerifyCollectContributions()
        {
            var payloadMass = this.payloadSubsystem.CollectContributions("mass");
            var cameraMass = this.camera.CollectContributions("mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).CollectContributions("mass"), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.payloadSubsystem.CollectContributions("temperature"), Has.Count.EqualTo(0));
                Assert.That(payloadMass.Select(contribution => contribution.Value), Is.EquivalentTo([38, 1.5]));
                Assert.That(cameraMass, Has.Count.EqualTo(1));
                Assert.That(cameraMass[0].Id, Is.EqualTo(CameraId));
            }
        }
    }
}
