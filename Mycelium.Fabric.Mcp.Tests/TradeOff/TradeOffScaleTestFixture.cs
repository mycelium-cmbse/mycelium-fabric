// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffScaleTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="TradeOffScale"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffScaleTestFixture
    {
        [Test]
        public void VerifyToUnits()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(TradeOffScale.ToUnits(0), Is.EqualTo(0));
                Assert.That(TradeOffScale.ToUnits(38.25), Is.EqualTo(38250));
                Assert.That(TradeOffScale.ToUnits(125.8 - 38), Is.EqualTo(87800));
                Assert.That(TradeOffScale.ToUnits(0.0005), Is.EqualTo(1));
                Assert.That(TradeOffScale.ToUnits(-1.5), Is.EqualTo(-1500));
            }
        }

        [Test]
        public void VerifyToValue()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(TradeOffScale.ToValue(141360), Is.EqualTo(141.36));
                Assert.That(TradeOffScale.ToValue(-8000), Is.EqualTo(-8));
                Assert.That(TradeOffScale.ToValue(1), Is.EqualTo(0.001));
            }
        }

        [Test]
        public void VerifyToFactorUnits()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(TradeOffScale.ToFactorUnits(0), Is.EqualTo(10000));
                Assert.That(TradeOffScale.ToFactorUnits(20), Is.EqualTo(12000));
                Assert.That(TradeOffScale.ToFactorUnits(12.5), Is.EqualTo(11250));
            }
        }
    }
}
