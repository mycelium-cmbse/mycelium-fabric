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

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Systems.Parts;

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
    }
}
