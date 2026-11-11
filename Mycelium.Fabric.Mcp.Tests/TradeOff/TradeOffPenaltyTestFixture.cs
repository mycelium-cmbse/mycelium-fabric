// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffPenaltyTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="TradeOffPenalty"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffPenaltyTestFixture
    {
        [Test]
        public void VerifyKeyAndAppliesTo()
        {
            var penalty = new TradeOffPenalty(2, new Dictionary<int, int> { [3] = 0, [1] = 2 }, 1);
            var sameOptions = new TradeOffPenalty(2, new Dictionary<int, int> { [1] = 2, [3] = 0 }, -1);
            var forAll = new TradeOffPenalty(0, new Dictionary<int, int>(), 1);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(penalty.Key, Is.EqualTo("2:1=2,3=0"));
                Assert.That(sameOptions.Key, Is.EqualTo(penalty.Key));
                Assert.That(forAll.Key, Is.EqualTo("0:"));
                Assert.That(penalty.AppliesTo([0, 2, 5, 0]), Is.True);
                Assert.That(penalty.AppliesTo([0, 2, 5, 1]), Is.False);
                Assert.That(penalty.AppliesTo([0, 1, 5, 0]), Is.False);
                Assert.That(forAll.AppliesTo([4, 4]), Is.True);
            }
        }
    }
}
