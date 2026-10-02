// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelElementExtensionsTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="ModelElementExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class ModelElementExtensionsTestFixture
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
        public void VerifyGetDefinition()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).GetDefinition(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new Package().GetDefinition(), Is.Null);
                Assert.That(this.camera.GetDefinition().DeclaredName, Is.EqualTo("OpticalCamera"));
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
        public void VerifyGetDocumentation()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).GetDocumentation(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.camera.GetDocumentation(), Is.Null);
                Assert.That(this.payloadSubsystem.GetDocumentation(), Is.EqualTo("Payload: the imaging instrument and its data storage."));
            }
        }

        [Test]
        public void VerifyGetAttributes()
        {
            // An untyped part that owns one attribute.
            var part = new PartUsage();
            part.AssignOwnership(new FeatureMembership(), new AttributeUsage { DeclaredName = "margin" });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).GetAttributes(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new Package().GetAttributes(), Has.Count.EqualTo(0));
                Assert.That(part.GetAttributes().Select(attribute => attribute.DeclaredName), Is.EqualTo(["margin"]));
                Assert.That(this.camera.GetAttributes().Select(attribute => attribute.DeclaredName), Is.EqualTo(["mass", "power"]));
            }
        }

        [Test]
        public void VerifyGetNumericValue()
        {
            var integerAttribute = new AttributeUsage();
            integerAttribute.AssignOwnership(new FeatureValue(), new LiteralInteger { Value = 55 });

            var expressionAttribute = new AttributeUsage();
            expressionAttribute.AssignOwnership(new FeatureValue(), new OperatorExpression());

            var cameraMass = this.camera.GetAttributes()[0];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IAttributeUsage)null).GetNumericValue(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new AttributeUsage().GetNumericValue(), Is.Null);
                Assert.That(expressionAttribute.GetNumericValue(), Is.Null);
                Assert.That(integerAttribute.GetNumericValue(), Is.EqualTo(55));
                Assert.That(cameraMass.GetNumericValue(), Is.EqualTo(38));
            }
        }
    }
}
