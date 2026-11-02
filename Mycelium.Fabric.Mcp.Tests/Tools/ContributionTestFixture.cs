// ------------------------------------------------------------------------------------------------
//  <copyright file="ContributionTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;

    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// Suite of tests for the <see cref="Contribution"/> class.
    /// </summary>
    [TestFixture]
    public class ContributionTestFixture
    {
        [Test]
        public void VerifyGetTotal()
        {
            var isUnitKnown = UnitCatalog.TryParse("kg", out var kilogram);

            var wheels = new Contribution(Guid.NewGuid(), "Demo::wheels", [], 5, kilogram, 10, 4).GetTotal();
            var computer = new Contribution(Guid.NewGuid(), "Demo::computer", [], 3).GetTotal();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(isUnitKnown, Is.True);
                Assert.That(wheels.Number, Is.EqualTo(20));
                Assert.That(wheels.Unit, Is.SameAs(kilogram));
                Assert.That(computer.Number, Is.EqualTo(3));
                Assert.That(computer.Unit, Is.Null);
            }
        }

        [Test]
        public void VerifyGetTotalWithMargin()
        {
            var isUnitKnown = UnitCatalog.TryParse("kg", out var kilogram);

            var withMargin = new Contribution(Guid.NewGuid(), "Demo::wheel", [], 5, kilogram, 10).GetTotalWithMargin();
            var withoutMargin = new Contribution(Guid.NewGuid(), "Demo::computer", [], 3).GetTotalWithMargin();
            var severalWithMargin = new Contribution(Guid.NewGuid(), "Demo::wheels", [], 5, kilogram, 10, 4).GetTotalWithMargin();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(isUnitKnown, Is.True);
                Assert.That(withMargin.Number, Is.EqualTo(5.5).Within(1e-9));
                Assert.That(withMargin.Unit, Is.SameAs(kilogram));
                Assert.That(withoutMargin.Number, Is.EqualTo(3));
                Assert.That(withoutMargin.Unit, Is.Null);
                Assert.That(severalWithMargin.Number, Is.EqualTo(22).Within(1e-9));
            }
        }
    }
}
