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
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Core.Features;
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

        [Test]
        public void VerifyGetUnitRange()
        {
            var modelProvider = OrbiterChangesHelper.CreateModel();

            var result = modelProvider.ApplyChanges(
            [
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = OrbiterChangesHelper.OrbiterName, Name = "spares", Multiplicity = "0..*" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = OrbiterChangesHelper.OrbiterName, Name = "antennas", Multiplicity = "*" } }
            ]);

            var parts = modelProvider.Elements.OfType<IPartUsage>().ToDictionary(part => part.qualifiedName);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(() => ((IFeature)null).GetUnitRange(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(parts["Orbiter::Catalogues::BatteryOptions::liIon150"].GetUnitRange(), Is.EqualTo((1, (int?)4)));
                Assert.That(parts["Orbiter::Catalogues::TankOptions::small"].GetUnitRange(), Is.EqualTo((0, (int?)1)));
                Assert.That(parts["Orbiter::orbiter::powerSystem::solarPanel"].GetUnitRange(), Is.EqualTo((2, (int?)2)));
                Assert.That(parts["Orbiter::orbiter::spares"].GetUnitRange(), Is.EqualTo((0, (int?)null)));
                Assert.That(parts["Orbiter::orbiter::antennas"].GetUnitRange(), Is.EqualTo((0, (int?)null)));
                Assert.That(parts["Orbiter::Catalogues::EngineOptions::thermal"].GetUnitRange(), Is.Null);
            }
        }
    }
}
