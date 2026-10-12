// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffLimitTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="TradeOffLimit"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffLimitTestFixture
    {
        [Test]
        public void VerifyIsUpperBound()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new TradeOffLimit("mass", TradeOffOperator.LessThan, 150, null, 0).IsUpperBound, Is.True);
                Assert.That(new TradeOffLimit("mass", TradeOffOperator.LessThanOrEqual, 150, null, 0).IsUpperBound, Is.True);
                Assert.That(new TradeOffLimit("capacity", TradeOffOperator.GreaterThan, 300, null, 0).IsUpperBound, Is.False);
                Assert.That(new TradeOffLimit("capacity", TradeOffOperator.GreaterThanOrEqual, 300, null, 0).IsUpperBound, Is.False);
            }
        }

        [Test]
        public void VerifyDescription()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new TradeOffLimit("mass", TradeOffOperator.LessThanOrEqual, 150, null, 20).Description, Is.EqualTo("mass * 1.2 <= 150"));
                Assert.That(new TradeOffLimit("mass", TradeOffOperator.LessThan, 149.5, null, 0).Description, Is.EqualTo("mass < 149.5"));
                Assert.That(new TradeOffLimit("capacity", TradeOffOperator.GreaterThanOrEqual, 300, null, 12.5).Description, Is.EqualTo("capacity >= 300 * 1.125"));
                Assert.That(new TradeOffLimit("power", TradeOffOperator.LessThanOrEqual, null, "powerOutput", 10).Description, Is.EqualTo("power * 1.1 <= powerOutput"));
                Assert.That(new TradeOffLimit("capacity", TradeOffOperator.GreaterThan, null, "eclipseEnergy", 0).Description, Is.EqualTo("capacity > eclipseEnergy"));
            }
        }

        [Test]
        public void VerifyGetLinearForm()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new TradeOffLimit("mass", TradeOffOperator.LessThanOrEqual, 150, null, 20).GetLinearForm(), Is.EqualTo(new TradeOffLinearForm(12000, 0, 1500000000)));
                Assert.That(new TradeOffLimit("mass", TradeOffOperator.LessThan, 150, null, 0).GetLinearForm(), Is.EqualTo(new TradeOffLinearForm(10000, 0, 1499999999)));
                Assert.That(new TradeOffLimit("capacity", TradeOffOperator.GreaterThanOrEqual, 300, null, 20).GetLinearForm(), Is.EqualTo(new TradeOffLinearForm(-10000, 0, -3600000000)));
                Assert.That(new TradeOffLimit("capacity", TradeOffOperator.GreaterThan, 300, null, 0).GetLinearForm(), Is.EqualTo(new TradeOffLinearForm(-10000, 0, -3000000001)));
                Assert.That(new TradeOffLimit("power", TradeOffOperator.LessThanOrEqual, null, "powerOutput", 10).GetLinearForm(), Is.EqualTo(new TradeOffLinearForm(11000, -10000, 0)));
                Assert.That(new TradeOffLimit("power", TradeOffOperator.LessThan, null, "powerOutput", 0).GetLinearForm(), Is.EqualTo(new TradeOffLinearForm(10000, -10000, -1)));
                Assert.That(new TradeOffLimit("capacity", TradeOffOperator.GreaterThanOrEqual, null, "eclipseEnergy", 20).GetLinearForm(), Is.EqualTo(new TradeOffLinearForm(-10000, 12000, 0)));
            }
        }
    }
}
