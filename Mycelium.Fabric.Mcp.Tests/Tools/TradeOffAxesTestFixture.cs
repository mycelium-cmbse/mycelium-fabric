// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffAxesTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Tools;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffAxes"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffAxesTestFixture
    {
        /// <summary>
        /// The totals of an orbiter: its mass, the same mass named after the root, its power and its fuel need.
        /// </summary>
        private static readonly string[] Attributes = ["mass", "orbiter.mass", "power", "fuelNeed"];

        /// <summary>
        /// Today, with a thermal engine, then two electric options.
        /// </summary>
        private static readonly IReadOnlyList<IReadOnlyDictionary<string, double>> Compared =
        [
            Totals(92, 70, 40),
            Totals(98, 235, 0),
            Totals(89, 235, 0)
        ];

        [Test]
        public void VerifyChoose()
        {
            IReadOnlyList<IReadOnlyDictionary<string, double>> unchanged = [Totals(92, 70, 40), Totals(92, 70, 40)];
            IReadOnlyList<IReadOnlyDictionary<string, double>> others = [Totals(100, 70, 40)];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => TradeOffAxes.Choose(null, [], [], []), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => TradeOffAxes.Choose(Attributes, null, [], []), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => TradeOffAxes.Choose(Attributes, [], null, []), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => TradeOffAxes.Choose(Attributes, [], [], null), Throws.TypeOf<ArgumentNullException>());

                // The first objective comes first, then the total that changes the most; the mass named after the root, equal to
                // the mass on every combination, is left out.
                Assert.That(TradeOffAxes.Choose(Attributes, ["mass", "power"], Compared, others), Is.EqualTo(["mass", "fuelNeed"]));

                // Without objective, the two totals that change the most.
                Assert.That(TradeOffAxes.Choose(Attributes, [], Compared, others), Is.EqualTo(["fuelNeed", "power"]));

                // An objective on a total that the study does not show is not drawn.
                Assert.That(TradeOffAxes.Choose(Attributes, ["volume"], Compared, others), Is.EqualTo(["fuelNeed", "power"]));

                // When nothing changes between the compared combinations, all the combinations are looked at.
                Assert.That(TradeOffAxes.Choose(Attributes, [], unchanged, others), Is.EqualTo(["mass"]));

                // When nothing changes at all, the first total is drawn alone; without total, nothing is.
                Assert.That(TradeOffAxes.Choose(Attributes, [], unchanged, []), Is.EqualTo(["mass"]));
                Assert.That(TradeOffAxes.Choose([], [], unchanged, []), Is.Empty);

                // A total that is zero everywhere, or that a combination does not have, changes nothing.
                Assert.That(TradeOffAxes.Choose(["volume", "power"], [], [new Dictionary<string, double> { ["volume"] = 0, ["power"] = 1 }, new Dictionary<string, double> { ["power"] = 2 }], []),
                    Is.EqualTo(["power"]));
            }
        }

        /// <summary>
        /// Builds the totals of a combination.
        /// </summary>
        /// <param name="mass">The mass, under both of its names.</param>
        /// <param name="power">The power.</param>
        /// <param name="fuelNeed">The fuel need.</param>
        /// <returns>The total of each attribute.</returns>
        private static Dictionary<string, double> Totals(double mass, double power, double fuelNeed)
        {
            return new Dictionary<string, double> { ["mass"] = mass, ["orbiter.mass"] = mass, ["power"] = power, ["fuelNeed"] = fuelNeed };
        }
    }
}
