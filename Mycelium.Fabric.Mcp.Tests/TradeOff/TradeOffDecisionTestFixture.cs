// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffDecisionTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TradeOff
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffDecision"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffDecisionTestFixture
    {
        private TradeOffDecision solarArray;

        [SetUp]
        public void SetUp()
        {
            var noValues = new Dictionary<string, double>();

            this.solarArray = new TradeOffDecision("solarArray", null, null,
                [new TradeOffOption("B", 2, false, noValues), new TradeOffOption("current", 1, true, noValues), new TradeOffOption("B", 3, false, noValues)], false);
        }

        [Test]
        public void VerifyCurrentOptionIndex()
        {
            Assert.That(this.solarArray.CurrentOptionIndex, Is.EqualTo(1));
        }

        [Test]
        public void VerifyGetOptionIndexes()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.solarArray.GetOptionIndexes("B"), Is.EqualTo([0, 2]));
                Assert.That(this.solarArray.GetOptionIndexes("current"), Is.EqualTo([1]));
                Assert.That(this.solarArray.GetOptionIndexes("A"), Is.Empty);
                Assert.That(this.solarArray.GetOptionIndexes(null), Is.Empty);
            }
        }
    }
}
