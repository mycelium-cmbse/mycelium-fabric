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
    }
}
