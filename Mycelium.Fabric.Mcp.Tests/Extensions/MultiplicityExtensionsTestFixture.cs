// ------------------------------------------------------------------------------------------------
//  <copyright file="MultiplicityExtensionsTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Multiplicities;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="MultiplicityExtensions"/> class.
    /// </summary>
    [TestFixture]
    public class MultiplicityExtensionsTestFixture
    {
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

            var computed = new MultiplicityRange();
            computed.AssignOwnership(new OwningMembership(), new FeatureReferenceExpression());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(() => ((IMultiplicity)null).GetUnitRange(), Throws.TypeOf<ArgumentNullException>());
                Assert.That(parts["Orbiter::Catalogues::BatteryOptions::liIon150"].multiplicity.GetUnitRange(), Is.EqualTo((1, (int?)4)));
                Assert.That(parts["Orbiter::Catalogues::TankOptions::small"].multiplicity.GetUnitRange(), Is.EqualTo((0, (int?)1)));
                Assert.That(parts["Orbiter::orbiter::powerSystem::solarPanel"].multiplicity.GetUnitRange(), Is.EqualTo((2, (int?)2)));
                Assert.That(parts["Orbiter::orbiter::spares"].multiplicity.GetUnitRange(), Is.EqualTo((0, (int?)null)));
                Assert.That(parts["Orbiter::orbiter::antennas"].multiplicity.GetUnitRange(), Is.EqualTo((0, (int?)null)));
                Assert.That(computed.GetUnitRange(), Is.Null);
            }
        }
    }
}
