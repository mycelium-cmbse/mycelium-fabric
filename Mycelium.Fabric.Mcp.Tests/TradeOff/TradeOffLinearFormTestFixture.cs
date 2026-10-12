// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffLinearFormTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TradeOff
{
    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffLinearForm"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffLinearFormTestFixture
    {
        [Test]
        public void VerifyIsSatisfiedBy()
        {
            var massWithMargin = new TradeOffLimit("mass", TradeOffOperator.LessThanOrEqual, 150, null, 20).GetLinearForm();
            var strictMass = new TradeOffLimit("mass", TradeOffOperator.LessThan, 150, null, 0).GetLinearForm();
            var powerBalance = new TradeOffLimit("power", TradeOffOperator.LessThanOrEqual, null, "powerOutput", 10).GetLinearForm();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(massWithMargin.IsSatisfiedBy(125000, 0), Is.True);
                Assert.That(massWithMargin.IsSatisfiedBy(125001, 0), Is.False);
                Assert.That(massWithMargin.IsSatisfiedBy(125800, 0), Is.False);
                Assert.That(strictMass.IsSatisfiedBy(149999, 0), Is.True);
                Assert.That(strictMass.IsSatisfiedBy(150000, 0), Is.False);
                Assert.That(powerBalance.IsSatisfiedBy(200000, 220000), Is.True);
                Assert.That(powerBalance.IsSatisfiedBy(200001, 220000), Is.False);
            }
        }
    }
}
