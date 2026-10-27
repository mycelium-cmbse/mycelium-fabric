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

    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffProblem"/> class.
    /// </summary>
    [TestFixture]
    public class TradeOffProblemTestFixture
    {
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
        public void VerifyEnumerateSelections()
        {
            var withoutLight = this.problem with { Excluded = [new TradeOffVariantReference(0, "light")] };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.problem.EnumerateSelections(10), Is.EqualTo(new[] { new[] { 0, 1 }, new[] { 1, 1 } }));
                Assert.That(this.problem.EnumerateSelections(1), Is.Null);
                Assert.That(withoutLight.EnumerateSelections(10), Is.EqualTo(new[] { new[] { 0, 1 } }));
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
    }
}
