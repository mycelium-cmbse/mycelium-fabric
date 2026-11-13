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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Enumerations;
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
                Assert.That(new EnumerationUsage().GetTypeNames(), Is.Empty);
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
                Assert.That(new EnumerationUsage().DescribeType(), Is.EqualTo("EnumerationUsage"));
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

        [Test]
        public void CollectValues_OfPartsTypedByACatalogue_CountsTheVariantThatEachOneTakes()
        {
            var modelProvider = OrbiterChangesHelper.CreateModel();
            var parts = modelProvider.Elements.OfType<IPartUsage>().ToDictionary(part => part.qualifiedName);
            var orbiter = parts["Orbiter::orbiter"];

            var mass = orbiter.CollectValues("mass");
            var powerOutput = orbiter.CollectValues("powerOutput");
            var open = orbiter.CollectValues("mass", new HashSet<Guid> { parts["Orbiter::orbiter::powerSystem::battery"].Id, parts["Orbiter::orbiter::powerSystem::solarPanel"].Id });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(mass.Contributions.Select(contribution => contribution.QualifiedName), Is.EqualTo(
                [
                    "Orbiter::orbiter::structure", "Orbiter::orbiter::computer", "Orbiter::Catalogues::CameraOptions::wide", "Orbiter::Catalogues::EngineOptions::thermal",
                    "Orbiter::Catalogues::TankOptions::large", "Orbiter::Catalogues::BatteryOptions::liIon150", "Orbiter::Catalogues::SolarPanelOptions::regular"
                ]));

                Assert.That(mass.Contributions.Sum(contribution => contribution.GetTotal().Number), Is.EqualTo(92));
                Assert.That(mass.MissingValues, Is.Empty);
                Assert.That(mass.Slots, Is.Empty);
                Assert.That(powerOutput.Contributions.Single().Count, Is.EqualTo(2));
                Assert.That(powerOutput.Contributions.Single().GetTotal().Number, Is.EqualTo(120));
                Assert.That(open.Contributions.Sum(contribution => contribution.GetTotal().Number), Is.EqualTo(84));
                Assert.That(open.Slots.Select(slot => (slot.Part.DeclaredName, slot.Count)), Is.EqualTo([("battery", 1), ("solarPanel", 1)]));
            }

            // A part taken 0 times counts for 0, and a part that takes no variant yet misses its value.
            var result = modelProvider.ApplyChanges(
            [
                new ModelChange { Identity = "tank", Payload = new ElementPayload { Multiplicity = "0" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "powerSystem", Name = "spare", Definition = "BatteryOptions" } }
            ]);

            orbiter = modelProvider.Elements.OfType<IPartUsage>().Single(part => part.qualifiedName == "Orbiter::orbiter");
            var fuelCapacity = orbiter.CollectValues("fuelCapacity");
            var massWithSpare = orbiter.CollectValues("mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(fuelCapacity.Contributions.Single().Count, Is.Zero);
                Assert.That(fuelCapacity.Contributions.Single().GetTotal().Number, Is.Zero);
                Assert.That(massWithSpare.Contributions.Sum(contribution => contribution.GetTotal().Number), Is.EqualTo(83));
                Assert.That(massWithSpare.MissingValues.Select(missingValue => missingValue.QualifiedName), Is.EqualTo(["Orbiter::orbiter::powerSystem::spare"]));
                Assert.That(orbiter.CollectValues("fuelNeed").MissingValues, Is.Empty);
            }
        }
    }
}
