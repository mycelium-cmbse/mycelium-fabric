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
        public void VerifyGetValueWithMargin()
        {
            UnitCatalog.TryParse("kg", out var kilogram);

            var withMargin = new Contribution(Guid.NewGuid(), "Demo::wheel", [], 5, kilogram, 10).GetValueWithMargin();
            var withoutMargin = new Contribution(Guid.NewGuid(), "Demo::computer", [], 3).GetValueWithMargin();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withMargin.Number, Is.EqualTo(5.5).Within(1e-9));
                Assert.That(withMargin.Unit, Is.SameAs(kilogram));
                Assert.That(withoutMargin.Number, Is.EqualTo(3));
                Assert.That(withoutMargin.Unit, Is.Null);
            }
        }
    }
}
