// ------------------------------------------------------------------------------------------------
//  <copyright file="CpSatTradeOffSolverTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="CpSatTradeOffSolver"/> class.
    /// </summary>
    /// <remarks>
    /// Most tests use the propulsion example: a thermal engine requires a fuel tank, an electric engine excludes it and
    /// requires a larger battery, consumes more power, hence needs more solar panels, whose power the power unit must handle.
    /// </remarks>
    [TestFixture]
    public class CpSatTradeOffSolverTestFixture
    {
        private CpSatTradeOffSolver solver;

        [SetUp]
        public void SetUp()
        {
            this.solver = new CpSatTradeOffSolver();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new CpSatTradeOffSolver(TimeSpan.FromSeconds(-1)), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => new CpSatTradeOffSolver(TimeSpan.FromSeconds(2.5)), Throws.Nothing);
                Assert.That(CpSatTradeOffSolver.DefaultTimeLimit, Is.EqualTo(TimeSpan.FromSeconds(5)));
            }

            var withoutTime = new CpSatTradeOffSolver(TimeSpan.Zero).Solve(CreatePropulsionProblem(1) with { CompareDecisionIndex = 0 });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutTime.Status, Is.EqualTo(TradeOffStatus.Unknown));
                Assert.That(withoutTime.Alternatives.Select(alternative => alternative.Status), Is.All.EqualTo(TradeOffStatus.Unknown));
                Assert.That(withoutTime.Alternatives.SelectMany(alternative => alternative.Conflicts), Is.Empty);
            }
        }

        [Test]
        public void VerifySolve()
        {
            Assert.That(() => this.solver.Solve(null), Throws.TypeOf<ArgumentNullException>());

            var problem = CreatePropulsionProblem(3);
            var solution = this.solver.Solve(problem);
            var alternative = solution.Alternatives.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(solution.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(alternative.Variant, Is.Null);
                Assert.That(alternative.Conflicts, Is.Empty);
                Assert.That(alternative.Selections.Select(selection => GetMass(problem, selection)), Is.EqualTo([102, 104, 105]));
                Assert.That(alternative.Selections[0], Is.EqualTo([1, 0, 1, 2, 1]));
                Assert.That(alternative.Selections[1], Is.EqualTo([0, 1, 0, 1, 0]));
                Assert.That(problem.Limits.All(limit => alternative.Selections.All(selection => problem.IsSatisfied(limit, selection))), Is.True);
            }

            var compared = this.solver.Solve(problem with { Count = 1, CompareDecisionIndex = 0 });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(compared.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(compared.Alternatives.Select(comparedAlternative => comparedAlternative.Variant), Is.EqualTo(["thermal", "electric"]));
                Assert.That(compared.Alternatives[0].Selections.Single(), Is.EqualTo([0, 1, 0, 1, 0]));
                Assert.That(compared.Alternatives[1].Selections.Single(), Is.EqualTo([1, 0, 1, 2, 1]));
            }
        }

        [Test]
        public void VerifySolveWithObjectives()
        {
            var camera = new TradeOffDecision("camera", null, null,
                [Option("current", true, ("mass", 38), ("power", 55)), Option("A", false, ("mass", 30), ("power", 70)), Option("B", false, ("mass", 30), ("power", 60))], false);

            var problem = new TradeOffProblem([camera], new Dictionary<string, double>(), [Minimize("mass"), Minimize("power")], [], [], 1, null);

            var lowestMassThenPower = this.solver.Solve(problem);
            var lowestMassThenHighestPower = this.solver.Solve(problem with { Objectives = [Minimize("mass"), Maximize("power")] });
            var highestPower = this.solver.Solve(problem with { Objectives = [Maximize("power")] });
            var closestUnderLimits = this.solver.Solve(problem with { Objectives = [], Limits = [Limit("mass", TradeOffOperator.LessThan, 38), Limit("power", TradeOffOperator.LessThanOrEqual, 65)] });
            var unchangedUnderLimits = this.solver.Solve(problem with { Objectives = [], Limits = [Limit("mass", TradeOffOperator.LessThanOrEqual, 38)] });
            var allCombinations = this.solver.Solve(problem with { Objectives = [Minimize("mass")], Count = 5 });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(lowestMassThenPower.Alternatives[0].Selections.Single(), Is.EqualTo([2]));
                Assert.That(lowestMassThenHighestPower.Alternatives[0].Selections.Single(), Is.EqualTo([1]));
                Assert.That(highestPower.Alternatives[0].Selections.Single(), Is.EqualTo([1]));
                Assert.That(closestUnderLimits.Alternatives[0].Selections.Single(), Is.EqualTo([2]));
                Assert.That(unchangedUnderLimits.Alternatives[0].Selections.Single(), Is.EqualTo([0]));
                Assert.That(allCombinations.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(allCombinations.Alternatives[0].Selections, Has.Count.EqualTo(3));
                Assert.That(allCombinations.Alternatives[0].Selections.Take(2).Select(selection => selection[0]), Is.EquivalentTo([1, 2]));
                Assert.That(allCombinations.Alternatives[0].Selections[2], Is.EqualTo([0]));
            }
        }

        [Test]
        public void VerifySolveWhenInfeasible()
        {
            var problem = CreatePropulsionProblem(1);

            var tooLight = this.solver.Solve(problem with { Limits = [.. problem.Limits, Limit("mass", TradeOffOperator.LessThanOrEqual, 90)] });
            var slightlyTooLight = this.solver.Solve(problem with { Limits = [.. problem.Limits, Limit("mass", TradeOffOperator.LessThanOrEqual, 101)] });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(tooLight.Status, Is.EqualTo(TradeOffStatus.Infeasible));
                Assert.That(tooLight.Alternatives.Single().Status, Is.EqualTo(TradeOffStatus.Infeasible));
                Assert.That(tooLight.Alternatives.Single().Selections, Is.Empty);
                Assert.That(tooLight.Alternatives.Single().Conflicts, Is.EqualTo(["constraint 3: mass <= 90"]));
                Assert.That(slightlyTooLight.Status, Is.EqualTo(TradeOffStatus.Infeasible));

                Assert.That(slightlyTooLight.Alternatives.Single().Conflicts, Is.EqualTo(
                [
                    "constraint 1: power * 1.1 <= powerOutput",
                    "constraint 2: powerOutput <= maxPower",
                    "constraint 3: mass <= 101",
                    "rule 1: propulsion.thermal requires fuelTank.small or fuelTank.large",
                    "rule 3: propulsion.electric requires battery.large"
                ]));
            }

            var requiredTank = problem.Decisions[1] with { Required = true };
            var compared = this.solver.Solve(problem with { Decisions = [problem.Decisions[0], requiredTank, .. problem.Decisions.Skip(2)], CompareDecisionIndex = 0 });
            var comparedTank = this.solver.Solve(problem with { Decisions = [problem.Decisions[0], requiredTank, .. problem.Decisions.Skip(2)], CompareDecisionIndex = 1 });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(compared.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(compared.Alternatives[0].Selections.Single(), Is.EqualTo([0, 1, 0, 1, 0]));
                Assert.That(compared.Alternatives[1].Status, Is.EqualTo(TradeOffStatus.Infeasible));
                Assert.That(compared.Alternatives[1].Conflicts, Is.EqualTo(["rule 2: propulsion.electric excludes fuelTank.small and fuelTank.large"]));
                Assert.That(comparedTank.Alternatives.Select(alternative => alternative.Variant), Is.EqualTo(["small", "large"]));
            }
        }

        [Test]
        public void VerifySolveWithExcludedVariants()
        {
            var problem = CreatePropulsionProblem(1) with { Excluded = [new TradeOffVariantReference(0, "electric")] };

            var withoutElectric = this.solver.Solve(problem);
            var compared = this.solver.Solve(problem with { CompareDecisionIndex = 0 });
            var namedLimit = this.solver.Solve(problem with { Limits = [.. problem.Limits, Limit("mass", TradeOffOperator.LessThanOrEqual, 90) with { Name = "REQ-SYS-001" }] });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutElectric.Status, Is.EqualTo(TradeOffStatus.Optimal));
                Assert.That(withoutElectric.Alternatives.Single().Selections.Single(), Is.EqualTo([0, 1, 0, 1, 0]));
                Assert.That(compared.Alternatives.Select(alternative => alternative.Variant), Is.EqualTo(["thermal"]));
                Assert.That(namedLimit.Alternatives.Single().Conflicts, Is.EqualTo(["REQ-SYS-001: mass <= 90"]));
            }
        }

        /// <summary>
        /// Creates the propulsion example: the best combination is electric (102 kg: large battery, 3 panels B, high power
        /// unit), then thermal (104 kg: small tank, 2 panels B).
        /// </summary>
        /// <param name="count">The number of combinations to find.</param>
        /// <returns>The <see cref="TradeOffProblem"/> of the example.</returns>
        private static TradeOffProblem CreatePropulsionProblem(int count)
        {
            var propulsion = new TradeOffDecision("propulsion", null, null, [Option("thermal", true, ("mass", 20), ("power", 5)), Option("electric", false, ("mass", 15), ("power", 150))], false);
            var fuelTank = new TradeOffDecision("fuelTank", null, null, [Option("none", true), Option("small", false, ("mass", 8)), Option("large", false, ("mass", 14))], false);
            var battery = new TradeOffDecision("battery", null, null, [Option("current", true, ("mass", 6)), Option("large", false, ("mass", 12))], false);

            List<TradeOffOption> solarArrayOptions = [Option("current", true, ("mass", 13), ("powerOutput", 180))];
            solarArrayOptions.AddRange(Enumerable.Range(2, 5).Select(quantity => new TradeOffOption("B", quantity, false, new Dictionary<string, double> { ["mass"] = 3 * quantity, ["powerOutput"] = 100 * quantity })));
            var solarArray = new TradeOffDecision("solarArray", null, null, solarArrayOptions, false);

            var powerUnit = new TradeOffDecision("pcdu", null, null, [Option("current", true, ("mass", 4), ("maxPower", 250)), Option("high", false, ("mass", 6), ("maxPower", 600))], false);

            TradeOffLimit[] limits =
            [
                new("power", TradeOffOperator.LessThanOrEqual, null, "powerOutput", 10),
                new("powerOutput", TradeOffOperator.LessThanOrEqual, null, "maxPower", 0)
            ];

            TradeOffDependency[] dependencies =
            [
                new(TradeOffRuleKind.Requires, new TradeOffVariantReference(0, "thermal"), [new TradeOffVariantReference(1, "small"), new TradeOffVariantReference(1, "large")],
                    "propulsion.thermal requires fuelTank.small or fuelTank.large"),
                new(TradeOffRuleKind.Excludes, new TradeOffVariantReference(0, "electric"), [new TradeOffVariantReference(1, "small"), new TradeOffVariantReference(1, "large")],
                    "propulsion.electric excludes fuelTank.small and fuelTank.large"),
                new(TradeOffRuleKind.Requires, new TradeOffVariantReference(0, "electric"), [new TradeOffVariantReference(2, "large")], "propulsion.electric requires battery.large")
            ];

            var fixedTotals = new Dictionary<string, double> { ["mass"] = 60, ["power"] = 100 };

            return new TradeOffProblem([propulsion, fuelTank, battery, solarArray, powerUnit], fixedTotals, [Minimize("mass")], limits, dependencies, count, null);
        }

        /// <summary>
        /// Creates an option of one unit.
        /// </summary>
        /// <param name="variant">The name of the variant.</param>
        /// <param name="isCurrent">Whether the option is the current state.</param>
        /// <param name="values">The values of the option.</param>
        /// <returns>The <see cref="TradeOffOption"/>.</returns>
        private static TradeOffOption Option(string variant, bool isCurrent, params (string Attribute, double Value)[] values)
        {
            return new TradeOffOption(variant, 1, isCurrent, values.ToDictionary(value => value.Attribute, value => value.Value));
        }

        /// <summary>
        /// Creates a limit on a number, without margin.
        /// </summary>
        /// <param name="attribute">The constrained attribute.</param>
        /// <param name="comparison">The <see cref="TradeOffOperator"/>.</param>
        /// <param name="limit">The limit.</param>
        /// <returns>The <see cref="TradeOffLimit"/>.</returns>
        private static TradeOffLimit Limit(string attribute, TradeOffOperator comparison, double limit)
        {
            return new TradeOffLimit(attribute, comparison, limit, null, 0);
        }

        /// <summary>
        /// Creates an objective that minimizes an attribute.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <returns>The <see cref="TradeOffObjective"/>.</returns>
        private static TradeOffObjective Minimize(string attribute)
        {
            return new TradeOffObjective { Attribute = attribute, Direction = TradeOffDirection.Minimize };
        }

        /// <summary>
        /// Creates an objective that maximizes an attribute.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <returns>The <see cref="TradeOffObjective"/>.</returns>
        private static TradeOffObjective Maximize(string attribute)
        {
            return new TradeOffObjective { Attribute = attribute, Direction = TradeOffDirection.Maximize };
        }

        /// <summary>
        /// Gets the total mass of a selection.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="selection">The selection.</param>
        /// <returns>The total mass.</returns>
        private static double GetMass(TradeOffProblem problem, IReadOnlyList<int> selection)
        {
            return TradeOffScale.ToValue(problem.GetTotalUnits("mass", selection));
        }
    }
}
