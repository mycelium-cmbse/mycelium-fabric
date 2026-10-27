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

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Serializer.Json;

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
            var modelProvider = new InMemoryModelProvider(new Mock<IModelChangeApplier>().Object, new DeSerializer());
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

        [Test]
        public void VerifyCollectValues()
        {
            // wheel1 has a mass and a margin; wheel2, of the same type, and the battery, which declares its mass, have none
            // yet; the harness has no mass at all and is not counted as missing.
            var spacecraft = BudgetChangesHelper.CreateModel(withMissingValues: true, withPartOutside: false).Elements
                .OfType<IPartUsage>()
                .Single(part => part.DeclaredName == BudgetChangesHelper.SpacecraftName);

            var spacecraftMass = spacecraft.CollectValues("mass");
            var payloadMass = this.payloadSubsystem.CollectValues("mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IElement)null).CollectValues("mass"), Throws.TypeOf<ArgumentNullException>());
                Assert.That(spacecraftMass.Contributions.Select(contribution => (contribution.QualifiedName, contribution.Value, contribution.Margin)),
                    Is.EqualTo(new (string, double, double?)[] { ("Budget::spacecraft::wheel1", 5, 10), ("Budget::spacecraft::computer", 3, null) }));
                Assert.That(spacecraftMass.MissingValues.Select(missingValue => missingValue.QualifiedName), Is.EqualTo(["Budget::spacecraft::wheel2", "Budget::spacecraft::battery"]));
                Assert.That(spacecraftMass.MissingValues[0].Types, Is.EqualTo(["Wheel"]));
                Assert.That(spacecraftMass.MissingValues[1].Types, Is.Empty);
                Assert.That(payloadMass.Contributions.Select(contribution => contribution.Margin), Is.All.Null);
                Assert.That(payloadMass.MissingValues, Is.Empty);
                Assert.That(this.payloadSubsystem.CollectValues("temperature").MissingValues, Is.Empty);
            }
        }

        [Test]
        public void CollectContributions_OfPartsDeclaredInADefinition_CountsThemForEachTypedPart()
        {
            var modelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            modelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            var result = modelProvider.ApplyChanges(DefinitionPartsChangesHelper.CreateChanges());
            var parts = modelProvider.Elements.OfType<IPartUsage>().ToList();

            var stageMass = parts.Single(part => part.DeclaredName == DefinitionPartsChangesHelper.StageName).CollectContributions("mass");
            var launcherMass = parts.Single(part => part.DeclaredName == DefinitionPartsChangesHelper.LauncherName).CollectContributions("mass");
            var nodeMass = parts.Single(part => part.DeclaredName == DefinitionPartsChangesHelper.NodeName).CollectContributions("mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(stageMass.Select(contribution => contribution.QualifiedName), Is.EqualTo(["Demo::Stage::engine1", "Demo::Stage::engine2"]));
                Assert.That(stageMass.Select(contribution => contribution.Value), Is.EqualTo([2, 2]));
                Assert.That(launcherMass, Has.Count.EqualTo(4));
                Assert.That(launcherMass.Sum(contribution => contribution.Value), Is.EqualTo(8));
                Assert.That(nodeMass, Is.Empty);
            }
        }
    }
}
