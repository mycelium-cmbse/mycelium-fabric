// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffSearchTestFixture.cs" company="Starion Group S.A.">
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
    using System.Globalization;
    using System.Linq;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.TradeOff;

    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="TradeOffSearch"/> class, on the orbiter of the <see cref="OrbiterChangesHelper"/>.
    /// </summary>
    [TestFixture]
    public class TradeOffSearchTestFixture
    {
        private InMemoryModelProvider modelProvider;

        private Guid orbiterId;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = OrbiterChangesHelper.CreateModel();
            this.orbiterId = this.modelProvider.Elements.OfType<IPartUsage>().Single(part => part.DeclaredName == OrbiterChangesHelper.OrbiterName).Id;
        }

        [Test]
        public void VerifySolve()
        {
            var verifier = this.CreateVerifier(new TradeOffRequest(this.orbiterId, null, null, null, null, 1, null));
            var current = verifier.Problem.GetCurrentSelection();
            var proposal = new TradeOffSolution(TradeOffStatus.Optimal, [new TradeOffAlternative(null, TradeOffStatus.Optimal, [current], [])]);
            var solver = new Mock<ITradeOffSolver>();
            solver.Setup(mock => mock.Solve(It.IsAny<TradeOffProblem>())).Returns(proposal);

            var (problem, solution) = TradeOffSearch.Solve(solver.Object, verifier.Problem, verifier);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => TradeOffSearch.Solve(null, verifier.Problem, verifier), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => TradeOffSearch.Solve(solver.Object, null, verifier), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => TradeOffSearch.Solve(solver.Object, verifier.Problem, null), Throws.TypeOf<ArgumentNullException>());

                // The check of the proposal teaches one penalty; the solver is asked again, and proposes nothing new.
                Assert.That(solution, Is.SameAs(proposal));
                Assert.That(problem.Penalties.Select(penalty => penalty.Key), Is.EqualTo(["2:3=0"]));
                Assert.That(verifier.Problem.Penalties, Is.Empty);
                solver.Verify(mock => mock.Solve(It.Is<TradeOffProblem>(solved => solved.Penalties.Count == 0)), Times.Once);
                solver.Verify(mock => mock.Solve(It.Is<TradeOffProblem>(solved => solved.Penalties.Count == 1)), Times.Once);
            }
        }

        [Test]
        public void VerifySolveStopsAfterTheLastRound()
        {
            // The requirement on the mass gets a second constraint: the solver cannot read it as a limit any more, and no
            // combination meets it, so that each proposal teaches a new penalty.
            var massBudgetId = this.modelProvider.Elements.Single(element => element.DeclaredName == "massBudget").Id.ToString();

            var result = this.modelProvider.ApplyChanges(
            [
                new ModelChange
                {
                    Identity = massBudgetId,
                    Payload = new ElementPayload
                    {
                        Constraints = [new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 50, Unit = "kg" }, new ConstraintPayload { Attribute = "power", Operator = "<=", Limit = 10000, Unit = "W" }]
                    }
                }
            ]);

            var verifier = this.CreateVerifier(new TradeOffRequest(this.orbiterId, null, null, null, null, 1, null));
            verifier.Problem.TryEnumerateSelections(1000, out var selections);
            var calls = 0;
            var solver = new Mock<ITradeOffSolver>();

            solver.Setup(mock => mock.Solve(It.IsAny<TradeOffProblem>()))
                .Returns(() => new TradeOffSolution(TradeOffStatus.Optimal, [new TradeOffAlternative(null, TradeOffStatus.Optimal, [selections[calls++]], [])]));

            var (problem, solution) = TradeOffSearch.Solve(solver.Object, verifier.Problem, verifier);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(verifier.Problem.Requirements.Single(requirement => requirement.Name == "REQ-SYS-001").LimitIndex, Is.Null);
                Assert.That(calls, Is.EqualTo(TradeOffSearch.MaximumRounds));
                Assert.That(solution.Status, Is.EqualTo(TradeOffStatus.Feasible));
                Assert.That(problem.Penalties, Has.Count.GreaterThanOrEqualTo(TradeOffSearch.MaximumRounds - 1));
            }
        }

        [Test]
        public void VerifySolveFindsTheBestCombinationsOfAllOfThem()
        {
            TradeOffRequest[] requests =
            [
                new TradeOffRequest(this.orbiterId, null, [new TradeOffObjective { Attribute = "mass" }], null, null, 5, null),
                new TradeOffRequest(this.orbiterId, null, [new TradeOffObjective { Attribute = "mass" }], null, null, 5, null, ["engine.electric"]),
                new TradeOffRequest(this.orbiterId, null, [new TradeOffObjective { Attribute = "mass" }], [new TradeOffConstraint { Attribute = "mass", Operator = "<=", Limit = 80 }], null, 5, null)
            ];

            foreach (var request in requests)
            {
                var verifier = this.CreateVerifier(request);
                var (problem, solution) = TradeOffSearch.Solve(new CpSatTradeOffSolver(), verifier.Problem, verifier);
                var found = solution.Alternatives.Single().Selections;

                // Every combination, ranked as the search ranks them: the fewest requirements and constraints not met first.
                problem.TryEnumerateSelections(1000, out var selections);

                var expected = selections
                    .Select(selection => (Broken: verifier.CountBroken(selection), Mass: problem.GetTotalUnits("mass", selection)))
                    .OrderBy(key => key.Broken)
                    .ThenBy(key => key.Mass)
                    .Take(5)
                    .ToList();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(solution.Status, Is.EqualTo(TradeOffStatus.Optimal));
                    Assert.That(found.Select(verifier.CountBroken), Is.EqualTo(expected.Select(key => key.Broken)));
                    Assert.That(found.Select(selection => problem.GetTotalUnits("mass", selection)), Is.EqualTo(expected.Select(key => key.Mass)));

                    // The solver and the checks agree on every combination once the search is over, not only on the best.
                    Assert.That(found.Select(problem.CountBroken), Is.EqualTo(found.Select(verifier.CountBroken)));
                }
            }
        }

        [Test]
        public void VerifySolveGivesTheChecksOfTheChangedModel()
        {
            var verifier = this.CreateVerifier(new TradeOffRequest(this.orbiterId, null, [new TradeOffObjective { Attribute = "mass" }], null, null, 3, "engine"));
            var (problem, solution) = TradeOffSearch.Solve(new CpSatTradeOffSolver(), verifier.Problem, verifier);
            var selections = solution.Alternatives.SelectMany(alternative => alternative.Selections).ToList();

            Assert.That(selections, Has.Count.EqualTo(6));

            foreach (var selection in selections)
            {
                // The combination is applied to a copy of the model with apply_changes, then checked as check_requirements does.
                var changedModel = OrbiterChangesHelper.CreateModel();
                var partIds = changedModel.Elements.OfType<IPartUsage>().ToDictionary(part => part.qualifiedName, part => part.Id.ToString());

                var changes = problem.Decisions
                    .Select((decision, index) => (decision, option: decision.Options[selection[index]]))
                    .Where(pair => !pair.option.IsCurrent)
                    .Select(pair => new ModelChange
                    {
                        Identity = partIds[pair.decision.QualifiedName],
                        Payload = new ElementPayload
                        {
                            Variant = pair.option.Quantity == 0 ? null : pair.option.Variant,
                            Multiplicity = pair.option.Quantity.ToString(CultureInfo.InvariantCulture)
                        }
                    })
                    .ToList();

                var result = changes.Count == 0 ? null : changedModel.ApplyChanges(changes);

                var modelChecks = new RequirementChecker(changedModel.Elements).CheckRequirements().Where(check => check.Status != RequirementStatus.NotVerifiable).ToList();
                var combinationChecks = verifier.Verify(selection).Select(check => check.Check).ToList();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(result?.Applied ?? true, Is.True, string.Join(" ", result?.Problems ?? []));
                    Assert.That(combinationChecks.Select(check => check.ReqId), Is.EqualTo(modelChecks.Select(check => check.ReqId)));
                    Assert.That(combinationChecks.Select(check => check.Status), Is.EqualTo(modelChecks.Select(check => check.Status)));
                    Assert.That(combinationChecks.Select(check => check.Explanation), Is.EqualTo(modelChecks.Select(check => check.Explanation)));
                    Assert.That(combinationChecks.Select(check => check.Value), Is.EqualTo(modelChecks.Select(check => check.Value)));
                    Assert.That(combinationChecks.Select(check => check.Gap), Is.EqualTo(modelChecks.Select(check => check.Gap)));
                }
            }
        }

        /// <summary>
        /// Builds the problem of a request on the orbiter, with what checks its combinations.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The <see cref="TradeOffVerifier"/> of the problem.</returns>
        private TradeOffVerifier CreateVerifier(TradeOffRequest request)
        {
            return new TradeOffVerifier(TradeOffProblemBuilder.Build(this.modelProvider, request));
        }
    }
}
