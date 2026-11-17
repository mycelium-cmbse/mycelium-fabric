// ------------------------------------------------------------------------------------------------
//  <copyright file="FeatureExtensionsTestFixture.cs" company="Starion Group S.A.">
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
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Multiplicities;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="FeatureExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class FeatureExtensionsTestFixture
    {
        [Test]
        public void VerifyGetPartsBeside()
        {
            // The spacecraft and the solar array are both owned by the Budget package; the wheels are inside the spacecraft.
            var parts = BudgetChangesHelper.CreateModel(withMissingValues: false, withPartOutside: true).Elements.OfType<IPartUsage>().ToList();

            IPartUsage Get(string name) => parts.Single(part => part.DeclaredName == name);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IFeature)null).GetPartsBeside(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(Get(BudgetChangesHelper.SpacecraftName).GetPartsBeside().Select(part => part.DeclaredName), Is.EqualTo([BudgetChangesHelper.SolarArrayName]));
                Assert.That(Get(BudgetChangesHelper.SolarArrayName).GetPartsBeside().Select(part => part.DeclaredName), Is.EqualTo([BudgetChangesHelper.SpacecraftName]));
                Assert.That(Get("wheel1").GetPartsBeside(), Is.Empty);
                Assert.That(new PartUsage().GetPartsBeside(), Is.Empty);
            }
        }

        [Test]
        public void VerifyGetOwnOrRedefinedDocumentation()
        {
            // The mass of the camera redefines the one of OpticalCamera, which is documented, without documentation of its own.
            var cameraMass = RedefinedValueHelper.CreateModel().GetElementById(RedefinedValueHelper.CameraId).GetAttributeUsage("mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IFeature)null).GetOwnOrRedefinedDocumentation(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new PartUsage().GetOwnOrRedefinedDocumentation(), Is.Null);
                Assert.That(cameraMass.GetDocumentationBodies(), Is.Null);
                Assert.That(cameraMass.GetOwnOrRedefinedDocumentation(), Is.EqualTo("Dry mass of the unit [kg]."));
            }
        }

        [Test]
        public void VerifyGetInheritingUsages()
        {
            // Both cameras take the mass of Instrument, which Camera specializes, until one of them redefines it.
            var modelProvider = RedefinitionChangesHelper.CreateModel();

            IFeature Get(string qualifiedName) => modelProvider.Elements.OfType<IFeature>().Single(feature => feature.qualifiedName == qualifiedName);
            List<string> GetInheritingUsages(string qualifiedName) => [.. Get(qualifiedName).GetInheritingUsages(modelProvider.Elements).Select(usage => usage.qualifiedName)];

            var before = GetInheritingUsages("Garage::Instrument::mass");
            var redefinition = modelProvider.ApplyChanges([new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "camera", Name = "mass", Value = 38, Unit = "kg" } }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IFeature)null).GetInheritingUsages([]), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new AttributeUsage().GetInheritingUsages(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new AttributeUsage().GetInheritingUsages(modelProvider.Elements), Is.Empty);
                Assert.That(redefinition.Applied, Is.True);
                Assert.That(before, Is.EquivalentTo(["Garage::rover::camera", "Garage::rover::spareCamera"]));
                Assert.That(GetInheritingUsages("Garage::Instrument::mass"), Is.EqualTo(["Garage::rover::spareCamera"]));
                Assert.That(GetInheritingUsages("Garage::Rover::wheels"), Is.EqualTo(["Garage::rover"]));
                Assert.That(GetInheritingUsages("Garage::rover::length"), Is.Empty);
            }
        }

        [Test]
        public void VerifyGetTypesAddedByRedefinition()
        {
            // A Battery is not an Engine; a BigEngine is.
            var modelProvider = RedefinitionChangesHelper.CreateModel();

            var result = modelProvider.ApplyChanges(
            [
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "rover", Redefines = "engine", Definition = "Battery" } },
                new ModelChange { Identity = "BigRover", Payload = new ElementPayload { Type = "PartDefinition", Owner = "Garage", Name = "BigRover", Specializes = "Rover" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "BigRover", Name = "engine", Definition = "BigEngine" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "BigRover", Redefines = "wheels", Multiplicity = "6" } }
            ]);

            IFeature Get(string qualifiedName) => modelProvider.Elements.OfType<IFeature>().Single(feature => feature.qualifiedName == qualifiedName);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IFeature)null).GetTypesAddedByRedefinition(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(result.Applied, Is.True);
                Assert.That(Get("Garage::rover::engine").GetTypesAddedByRedefinition().Select(types => (types.Added.name, types.Redefined.name)), Is.EqualTo([("Battery", "Engine")]));
                Assert.That(Get("Garage::BigRover::engine").GetTypesAddedByRedefinition(), Is.Empty);
                Assert.That(Get("Garage::BigRover::wheels").GetTypesAddedByRedefinition(), Is.Empty);
                Assert.That(Get("Garage::Rover::engine").GetTypesAddedByRedefinition(), Is.Empty);
            }
        }

        [Test]
        public void VerifyDescribeMultiplicityAndGetUnitCount()
        {
            var parts = MultiplicityChangesHelper.CreateModel(withUncountedPart: true).Elements.OfType<IPartUsage>().ToList();

            IPartUsage Get(string name) => parts.Single(part => part.DeclaredName == name);

            var fixedRange = CreatePart(new LiteralInteger { Value = 4 }, new LiteralInteger { Value = 4 });
            var unbounded = CreatePart(new LiteralInteger { Value = 0 }, new LiteralInfinity());
            var computed = CreatePart(new FeatureReferenceExpression());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IFeature)null).DescribeMultiplicity(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => ((IFeature)null).GetUnitCount(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(Get("bogies").DescribeMultiplicity(), Is.EqualTo("2"));
                Assert.That(Get("bogies").GetUnitCount(), Is.EqualTo(2));
                Assert.That(Get("heaters").DescribeMultiplicity(), Is.EqualTo("1..4"));
                Assert.That(Get("heaters").GetUnitCount(), Is.Null);
                Assert.That(Get("battery").DescribeMultiplicity(), Is.Null);
                Assert.That(Get("battery").GetUnitCount(), Is.EqualTo(1));
                Assert.That(fixedRange.DescribeMultiplicity(), Is.EqualTo("4..4"));
                Assert.That(fixedRange.GetUnitCount(), Is.EqualTo(4));
                Assert.That(unbounded.DescribeMultiplicity(), Is.EqualTo("0..*"));
                Assert.That(unbounded.GetUnitCount(), Is.Null);
                Assert.That(computed.DescribeMultiplicity(), Is.EqualTo("?"));
                Assert.That(computed.GetUnitCount(), Is.Null);
            }
        }

        /// <summary>
        /// Creates a part whose multiplicity has the given bounds, as <c>part x[0..*];</c> does.
        /// </summary>
        /// <param name="bounds">The bounds of the multiplicity, the lower one first when there are two.</param>
        /// <returns>The <see cref="PartUsage"/>.</returns>
        private static PartUsage CreatePart(params IElement[] bounds)
        {
            var part = new PartUsage { DeclaredName = "part" };
            var range = new MultiplicityRange();
            part.AssignOwnership(new OwningMembership(), range);

            foreach (var bound in bounds)
            {
                range.AssignOwnership(new OwningMembership(), bound);
            }

            return part;
        }

        [Test]
        public void VerifyGetVariantsAndGetChosenVariant()
        {
            var parts = OrbiterChangesHelper.CreateModel().Elements.OfType<IPartUsage>().ToList();
            var engine = parts.Single(part => part.qualifiedName == "Orbiter::orbiter::propulsion::engine");
            var structure = parts.Single(part => part.DeclaredName == "structure");
            var thermal = parts.Single(part => part.qualifiedName == "Orbiter::Catalogues::EngineOptions::thermal");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => ((IFeature)null).GetVariants(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(engine.GetVariants().Select(variant => variant.DeclaredName), Is.EqualTo(["thermal", "electric"]));
                Assert.That(engine.GetChosenVariant(), Is.SameAs(thermal));
                Assert.That(structure.GetVariants(), Is.Empty);
                Assert.That(structure.GetChosenVariant(), Is.Null);
                Assert.That(thermal.GetTypeNames(), Is.EqualTo(["EngineOptions"]));
                Assert.That(thermal.GetVariants(), Is.Empty);
                Assert.That(thermal.GetChosenVariant(), Is.Null);
            }
        }
    }
}
