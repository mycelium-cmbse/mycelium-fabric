// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffProblemTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TradeOff
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffProblem"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffProblemTestFixture
    {
        /// <summary>
        /// The selections of the problem: the current or the light camera, each with the small fuel tank.
        /// </summary>
        private static readonly int[][] BothCameras = [[0, 1], [1, 1]];

        /// <summary>
        /// The only selection left when the light camera is excluded.
        /// </summary>
        private static readonly int[][] CurrentCamera = [[0, 1]];

        private TradeOffProblem problem;

        [SetUp]
        public void SetUp()
        {
            var camera = new TradeOffDecision("camera", Guid.NewGuid(), "EOSat1::camera",
            [
                new TradeOffOption("current", 1, true, new Dictionary<string, double> { ["mass"] = 38, ["power"] = 55 }),
                new TradeOffOption("light", 1, false, new Dictionary<string, double> { ["mass"] = 30, ["power"] = 55 })
            ], false);

            var fuelTank = new TradeOffDecision("fuelTank", null, null,
            [
                new TradeOffOption("none", 0, true, new Dictionary<string, double>()),
                new TradeOffOption("small", 1, false, new Dictionary<string, double> { ["mass"] = 8.0004 })
            ], true);

            TradeOffLimit[] limits =
            [
                new("mass", TradeOffOperator.LessThanOrEqual, 150, null, 20),
                new("power", TradeOffOperator.LessThanOrEqual, null, "powerOutput", 0),
                new("mass", TradeOffOperator.GreaterThan, 10, null, 0)
            ];

            this.problem = new TradeOffProblem([camera, fuelTank], new Dictionary<string, double> { ["mass"] = 87.8, ["power"] = 172, ["powerOutput"] = 180 },
                [new TradeOffObjective { Attribute = "power" }, new TradeOffObjective { Attribute = "mass" }], limits, [], 1, null);
        }

        [Test]
        public void VerifyGetAttributes()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.problem.GetAttributes(), Is.EqualTo(["power", "mass", "powerOutput"]));
                Assert.That((this.problem with { Objectives = [], Limits = [] }).GetAttributes(), Is.Empty);
            }
        }

        [Test]
        public void VerifyGetAlternatives()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.problem.GetAlternatives(), Is.EqualTo(new TradeOffVariantReference[] { null }));
                Assert.That((this.problem with { CompareDecisionIndex = 0 }).GetAlternatives(), Is.EqualTo([new TradeOffVariantReference(0, "current"), new TradeOffVariantReference(0, "light")]));
                Assert.That((this.problem with { CompareDecisionIndex = 1 }).GetAlternatives(), Is.EqualTo([new TradeOffVariantReference(1, "small")]));
            }
        }

        [Test]
        public void VerifyGetCurrentSelection()
        {
            Assert.That(this.problem.GetCurrentSelection(), Is.EqualTo([0, 0]));
        }

        [Test]
        public void VerifyGetTotalUnits()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.problem.GetTotalUnits("mass", null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.problem.GetTotalUnits("mass", [0, 0]), Is.EqualTo(125800));
                Assert.That(this.problem.GetTotalUnits("mass", [1, 1]), Is.EqualTo(125800));
                Assert.That(this.problem.GetTotalUnits("power", [1, 1]), Is.EqualTo(227000));
                Assert.That(this.problem.GetTotalUnits("temperature", [1, 1]), Is.EqualTo(0));
            }
        }

        [Test]
        public void VerifyIsSatisfied()
        {
            var massWithMargin = this.problem.Limits[0];
            var powerBalance = this.problem.Limits[1];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.problem.IsSatisfied(null, [0, 0]), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.problem.IsSatisfied(massWithMargin, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.problem.IsSatisfied(massWithMargin, [0, 0]), Is.False);
                Assert.That(this.problem.IsSatisfied(massWithMargin, [1, 0]), Is.True);
                Assert.That(this.problem.IsSatisfied(powerBalance, [0, 0]), Is.False);
                Assert.That(this.problem.IsSatisfied(this.problem.Limits[2], [0, 0]), Is.True);
            }
        }

        [Test]
        public void VerifyIsAllowed()
        {
            var withoutLight = this.problem with { Excluded = [new TradeOffVariantReference(0, "light")] };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.problem.IsAllowed(0, 0), Is.True);
                Assert.That(this.problem.IsAllowed(0, 1), Is.True);
                Assert.That(this.problem.IsAllowed(1, 0), Is.False);
                Assert.That(this.problem.IsAllowed(1, 1), Is.True);
                Assert.That(this.problem.IsExcluded(new TradeOffVariantReference(0, "light")), Is.False);
                Assert.That(withoutLight.IsExcluded(new TradeOffVariantReference(0, "light")), Is.True);
                Assert.That(withoutLight.IsAllowed(0, 1), Is.False);
                Assert.That((withoutLight with { CompareDecisionIndex = 0 }).GetAlternatives(), Is.EqualTo([new TradeOffVariantReference(0, "current")]));
            }
        }

        [Test]
        public void VerifyTryEnumerateSelections()
        {
            var withoutLight = this.problem with { Excluded = [new TradeOffVariantReference(0, "light")] };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.problem.TryEnumerateSelections(10, out var bothCameras), Is.True);
                Assert.That(bothCameras, Is.EqualTo(BothCameras));
                Assert.That(this.problem.TryEnumerateSelections(1, out var tooMany), Is.False);
                Assert.That(tooMany, Is.Empty);
                Assert.That(withoutLight.TryEnumerateSelections(10, out var currentCamera), Is.True);
                Assert.That(currentCamera, Is.EqualTo(CurrentCamera));
            }
        }

        [Test]
        public void VerifyIsRespected()
        {
            var requires = new TradeOffDependency(TradeOffRuleKind.Requires, new TradeOffVariantReference(0, "light"), [new TradeOffVariantReference(1, "small")], "camera.light requires fuelTank.small");
            var excludes = requires with { Kind = TradeOffRuleKind.Excludes };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.problem.IsRespected(null, [0, 0]), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.problem.IsRespected(requires, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.problem.IsRespected(requires, [0, 0]), Is.True);
                Assert.That(this.problem.IsRespected(requires, [1, 0]), Is.False);
                Assert.That(this.problem.IsRespected(requires, [1, 1]), Is.True);
                Assert.That(this.problem.IsRespected(excludes, [1, 1]), Is.False);
                Assert.That(this.problem.IsRespected(excludes, [1, 0]), Is.True);
            }
        }

        [Test]
        public void VerifyCountChanges()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.problem.CountChanges(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.problem.CountChanges([0, 0]), Is.EqualTo(0));
                Assert.That(this.problem.CountChanges([1, 0]), Is.EqualTo(1));
                Assert.That(this.problem.CountChanges([1, 1]), Is.EqualTo(2));
            }
        }

        [Test]
        public void VerifyGetRangeAndGetReference()
        {
            var camera = new TradeOffDecision("camera", null, null,
            [
                new TradeOffOption("current", 1, true, new Dictionary<string, double> { ["mass"] = 38, ["power"] = 55 }),
                new TradeOffOption("A", 1, false, new Dictionary<string, double> { ["mass"] = 30, ["power"] = 70 })
            ], false);

            var fixedTotals = new Dictionary<string, double> { ["mass"] = 80, ["power"] = 100, ["powerOutput"] = 200 };
            var massBudget = new TradeOffLimit("mass", TradeOffOperator.LessThanOrEqual, 150, null, 20);
            var powerBalance = new TradeOffLimit("power", TradeOffOperator.LessThanOrEqual, null, "powerOutput", 0);
            var massOverNothing = new TradeOffLimit("mass", TradeOffOperator.GreaterThanOrEqual, null, "spare", 0);
            var nothingOverNothing = new TradeOffLimit("spare", TradeOffOperator.LessThanOrEqual, null, "other", 0);
            var problem = new TradeOffProblem([camera], fixedTotals, [], [massBudget, powerBalance], [], 1, null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => problem.GetRange(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => problem.GetReference(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(problem.GetRange(massBudget), Is.EqualTo((1_320_000_000L, 1_416_000_000L)));
                Assert.That(problem.GetRange(powerBalance), Is.EqualTo((-450_000_000L, -300_000_000L)));

                // The excess of a broken limit is compared to the limit, or to the current total that stands for it.
                Assert.That(problem.GetReference(massBudget), Is.EqualTo(1_500_000_000L));
                Assert.That(problem.GetReference(powerBalance), Is.EqualTo(2_000_000_000L));
                Assert.That(problem.GetReference(massOverNothing), Is.EqualTo(1_180_000_000L));
                Assert.That(problem.GetReference(nothingOverNothing), Is.EqualTo(TradeOffScale.UnitsPerFactor * TradeOffScale.UnitsPerValue));
            }
        }

        [Test]
        public void VerifyCountBrokenAndTheTotalsOfTheModel()
        {
            var camera = new TradeOffDecision("camera", null, null,
            [
                new TradeOffOption("current", 1, true, new Dictionary<string, double> { ["mass"] = 38, ["satellite.mass"] = 38 }),
                new TradeOffOption("A", 1, false, new Dictionary<string, double> { ["mass"] = 30, ["satellite.mass"] = 30 })
            ], false);

            var fixedTotals = new Dictionary<string, double> { ["mass"] = 80, ["satellite.mass"] = 80 };
            var given = new TradeOffLimit("mass", TradeOffOperator.LessThanOrEqual, 100, null, 0);
            var fromModel = new TradeOffLimit("satellite.mass", TradeOffOperator.LessThanOrEqual, 115, null, 0, Name: "REQ-SYS-001") { IsFromModel = true };

            var problem = new TradeOffProblem([camera], fixedTotals, [], [given, fromModel], [], 1, null)
            {
                Penalties = [new TradeOffPenalty(0, new Dictionary<int, int> { [0] = 1 }, 1)]
            };

            var manyModelLimits = Enumerable.Range(0, TradeOffProblem.MaximumModelTotals + 1)
                .Select(index => new TradeOffLimit($"part{index}.mass", TradeOffOperator.LessThanOrEqual, 10, null, 0) { IsFromModel = true })
                .ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => problem.CountBroken(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(problem.Requirements, Is.Empty);
                Assert.That(problem.Space, Is.Null);
                Assert.That(problem.CountBroken([0]), Is.EqualTo(2));
                Assert.That(problem.CountBroken([1]), Is.EqualTo(2));
                Assert.That((problem with { Penalties = [] }).CountBroken([1]), Is.EqualTo(1));

                // The totals that the requirements of the model compare are shown when the request names no attribute, unless
                // they are too many.
                Assert.That(problem.GetAttributes(), Is.EqualTo(["mass"]));
                Assert.That((problem with { Limits = [fromModel] }).GetAttributes(), Is.EqualTo(["satellite.mass"]));
                Assert.That((problem with { Limits = [.. manyModelLimits] }).GetAttributes(), Is.Empty);
            }
        }
    }
}
