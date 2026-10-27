// ------------------------------------------------------------------------------------------------
//  <copyright file="CpSatTradeOffSolver.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Google.OrTools.Sat;

    /// <summary>
    /// The <see cref="ITradeOffSolver"/> that hands the problem to CP-SAT, the constraint solver of Google OR-Tools. It has no
    /// state, so that a single instance serves every request.
    /// </summary>
    /// <remarks>
    /// Each option of each decision is a Boolean variable, and each decision takes exactly one option. A total is the fixed part
    /// plus the sum of the selected options, so that every limit and dependency is a linear constraint on integers, which
    /// CP-SAT solves exactly. The objectives are optimized one after the other: once the best total of an objective is known,
    /// it is fixed and the next objective is optimized, then the number of changes. Each limit and dependency is enforced by an
    /// assumption, so that an impossible problem can be explained by the assumptions that conflict. When a decision is
    /// compared, the search runs once for each of its variants, with this variant imposed.
    /// </remarks>
    public sealed class CpSatTradeOffSolver : ITradeOffSolver
    {
        /// <summary>
        /// The time limit of each call to CP-SAT when none is given.
        /// </summary>
        public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The parameters of each call to CP-SAT: the time limit, and a single worker so that the search, and the choice between
        /// equally good combinations, is the same from one call to the next.
        /// </summary>
        private readonly string solverParameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="CpSatTradeOffSolver"/> class, with the <see cref="DefaultTimeLimit"/>.
        /// </summary>
        public CpSatTradeOffSolver() : this(DefaultTimeLimit)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CpSatTradeOffSolver"/> class.
        /// </summary>
        /// <param name="timeLimit">The time limit of each call to CP-SAT.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="timeLimit"/> is negative.
        /// </exception>
        public CpSatTradeOffSolver(TimeSpan timeLimit)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(timeLimit, TimeSpan.Zero);

            this.solverParameters = $"max_time_in_seconds:{timeLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} num_workers:1";
        }

        /// <summary>
        /// Finds the best combinations of the problem: each takes exactly one option of each decision and respects every limit
        /// and dependency. They are ranked by the objectives, one after the other by order of priority, then by the number of
        /// changes from the current state. When a decision is compared, each of its variants gets its own best combinations.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/> to solve.</param>
        /// <returns>
        /// The <see cref="TradeOffSolution"/>, with at most <see cref="TradeOffProblem.Count"/> combinations for each alternative,
        /// or the conflicts of an alternative that has none.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="problem"/> is <c>null</c>.
        /// </exception>
        public TradeOffSolution Solve(TradeOffProblem problem)
        {
            ArgumentNullException.ThrowIfNull(problem);

            var alternatives = problem.GetAlternatives().Select(forcedVariant => this.SolveAlternative(problem, forcedVariant)).ToList();

            if (alternatives.TrueForAll(alternative => alternative.Selections.Count == 0))
            {
                var status = alternatives.TrueForAll(alternative => alternative.Status == TradeOffStatus.Infeasible) ? TradeOffStatus.Infeasible : TradeOffStatus.Unknown;
                return new TradeOffSolution(status, alternatives);
            }

            var anyFeasible = alternatives.Exists(alternative => alternative.Status == TradeOffStatus.Feasible);

            return new TradeOffSolution(anyFeasible ? TradeOffStatus.Feasible : TradeOffStatus.Optimal, alternatives);
        }

        /// <summary>
        /// Finds the best combinations that take the given variant, one after the other: each search excludes the combinations
        /// found before it.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/> to solve.</param>
        /// <param name="forcedVariant">The variant that every combination takes, or <c>null</c> for none.</param>
        /// <returns>The <see cref="TradeOffAlternative"/> of the variant.</returns>
        private TradeOffAlternative SolveAlternative(TradeOffProblem problem, TradeOffVariantReference forcedVariant)
        {
            List<IReadOnlyList<int>> selections = [];
            var status = TradeOffStatus.Optimal;

            while (selections.Count < problem.Count)
            {
                var search = new CombinationSearch(problem, this.solverParameters, forcedVariant, selections);
                var (searchStatus, selection) = search.Run();

                if (selection == null && selections.Count == 0)
                {
                    return new TradeOffAlternative(forcedVariant?.Variant, searchStatus, [], searchStatus == TradeOffStatus.Infeasible ? search.FindConflicts() : []);
                }

                if (selection == null)
                {
                    break;
                }

                if (searchStatus == TradeOffStatus.Feasible)
                {
                    status = TradeOffStatus.Feasible;
                }

                selections.Add(selection);
            }

            return new TradeOffAlternative(forcedVariant?.Variant, status, selections, []);
        }

        /// <summary>
        /// The search of one combination: a CP-SAT model of the problem from which the combinations already found are excluded.
        /// </summary>
        private sealed class CombinationSearch
        {
            /// <summary>
            /// The <see cref="TradeOffProblem"/> to solve.
            /// </summary>
            private readonly TradeOffProblem problem;

            /// <summary>
            /// The parameters of each call to CP-SAT.
            /// </summary>
            private readonly string solverParameters;

            /// <summary>
            /// The CP-SAT model of the problem.
            /// </summary>
            private readonly CpModel model = new();

            /// <summary>
            /// The Boolean variable of each option of each decision.
            /// </summary>
            private readonly BoolVar[][] optionLiterals;

            /// <summary>
            /// The assumption that enforces each limit and each dependency, with its description.
            /// </summary>
            private readonly List<(BoolVar Literal, string Description)> assumptions = [];

            /// <summary>
            /// The solver of the first stage of <see cref="Run"/>, which holds the assumptions of an impossible problem.
            /// </summary>
            private CpSolver firstSolver;

            /// <summary>
            /// Initializes a new instance of the <see cref="CombinationSearch"/> class.
            /// </summary>
            /// <param name="problem">The <see cref="TradeOffProblem"/> to solve.</param>
            /// <param name="solverParameters">The parameters of each call to CP-SAT.</param>
            /// <param name="forcedVariant">The variant that the combination takes, or <c>null</c> for none.</param>
            /// <param name="excludedSelections">The combinations already found, which the search must not return again.</param>
            public CombinationSearch(TradeOffProblem problem, string solverParameters, TradeOffVariantReference forcedVariant, IReadOnlyList<IReadOnlyList<int>> excludedSelections)
            {
                this.problem = problem;
                this.solverParameters = solverParameters;

                this.optionLiterals =
                [
                    .. problem.Decisions.Select((decision, decisionIndex) =>
                        decision.Options.Select((_, optionIndex) => this.model.NewBoolVar($"d{decisionIndex}o{optionIndex}")).ToArray())
                ];

                foreach (var literals in this.optionLiterals)
                {
                    this.model.AddExactlyOne(literals);
                }

                for (var decisionIndex = 0; decisionIndex < problem.Decisions.Count; decisionIndex++)
                {
                    for (var optionIndex = 0; optionIndex < problem.Decisions[decisionIndex].Options.Count; optionIndex++)
                    {
                        if (!problem.IsAllowed(decisionIndex, optionIndex))
                        {
                            this.model.Add(this.optionLiterals[decisionIndex][optionIndex] == 0);
                        }
                    }
                }

                if (forcedVariant != null)
                {
                    this.model.Add(LinearExpr.Sum(this.GetVariantLiterals(forcedVariant)) == 1);
                }

                foreach (var (limit, limitIndex) in problem.Limits.Select((limit, limitIndex) => (limit, limitIndex)))
                {
                    this.AddLimit(limit, $"{limit.Name ?? $"constraint {limitIndex + 1}"}: {limit.Description}");
                }

                foreach (var (dependency, dependencyIndex) in problem.Dependencies.Select((dependency, dependencyIndex) => (dependency, dependencyIndex)))
                {
                    this.AddDependency(dependency, $"rule {dependencyIndex + 1}: {dependency.Description}");
                }

                foreach (var selection in excludedSelections)
                {
                    var selectedLiterals = selection.Select((optionIndex, decisionIndex) => this.optionLiterals[decisionIndex][optionIndex]);
                    this.model.Add(LinearExpr.Sum(selectedLiterals) <= selection.Count - 1);
                }

                this.model.AddAssumptions(this.assumptions.Select(assumption => assumption.Literal));
            }

            /// <summary>
            /// Searches the best combination: optimizes each objective in turn and fixes its best total, then minimizes the
            /// number of changes.
            /// </summary>
            /// <returns>
            /// The <see cref="TradeOffStatus"/> of the search, and the selection found or <c>null</c> when none was found.
            /// </returns>
            public (TradeOffStatus Status, IReadOnlyList<int> Selection) Run()
            {
                var stages = this.problem.Objectives
                    .Select(objective => (Expression: this.GetTotalExpression(objective.Attribute), objective.Direction))
                    .Append((Expression: this.GetChangeCountExpression(), Direction: TradeOffDirection.Minimize))
                    .ToList();

                var status = TradeOffStatus.Optimal;
                IReadOnlyList<int> selection = null;

                foreach (var (expression, direction) in stages)
                {
                    if (direction == TradeOffDirection.Maximize)
                    {
                        this.model.Maximize(expression);
                    }
                    else
                    {
                        this.model.Minimize(expression);
                    }

                    var solver = new CpSolver { StringParameters = this.solverParameters };
                    var solverStatus = solver.Solve(this.model);
                    this.firstSolver ??= solver;

                    if (solverStatus is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible) && selection != null)
                    {
                        return (TradeOffStatus.Feasible, selection);
                    }

                    if (solverStatus is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
                    {
                        return (solverStatus == CpSolverStatus.Infeasible ? TradeOffStatus.Infeasible : TradeOffStatus.Unknown, null);
                    }

                    if (solverStatus == CpSolverStatus.Feasible)
                    {
                        status = TradeOffStatus.Feasible;
                    }

                    selection = [.. this.optionLiterals.Select(literals => Array.FindIndex(literals, solver.BooleanValue))];
                    this.model.Add(expression == solver.Value(expression));
                }

                return (status, selection);
            }

            /// <summary>
            /// Explains why no combination exists: starts from the assumptions that CP-SAT found sufficient for the conflict,
            /// and removes each one whose removal leaves the problem impossible, so that only the limits and dependencies that
            /// really conflict remain.
            /// </summary>
            /// <returns>The descriptions of the conflicting limits and dependencies, in the order of the problem.</returns>
            public IReadOnlyList<string> FindConflicts()
            {
                var coreIndexes = this.firstSolver.SufficientAssumptionsForInfeasibility().ToHashSet();
                var conflicts = this.assumptions.Where(assumption => coreIndexes.Contains(assumption.Literal.Index)).ToList();

                foreach (var assumption in conflicts.ToList())
                {
                    var remaining = conflicts.Where(candidate => candidate != assumption).ToList();

                    this.model.ClearAssumptions();
                    this.model.AddAssumptions(remaining.Select(candidate => candidate.Literal));

                    if (new CpSolver { StringParameters = this.solverParameters }.Solve(this.model) == CpSolverStatus.Infeasible)
                    {
                        conflicts = remaining;
                    }
                }

                return [.. conflicts.Select(assumption => assumption.Description)];
            }

            /// <summary>
            /// Adds a limit, enforced by a new assumption.
            /// </summary>
            /// <param name="limit">The <see cref="TradeOffLimit"/> to add.</param>
            /// <param name="description">The description of the limit in a conflict.</param>
            private void AddLimit(TradeOffLimit limit, string description)
            {
                var literal = this.model.NewBoolVar(description);
                var form = limit.GetLinearForm();

                var expression = form.TotalCoefficient * this.GetTotalExpression(limit.Attribute);

                if (limit.LimitAttribute != null)
                {
                    expression += form.LimitCoefficient * this.GetTotalExpression(limit.LimitAttribute);
                }

                this.model.Add(expression <= form.Bound).OnlyEnforceIf(literal);
                this.assumptions.Add((literal, description));
            }

            /// <summary>
            /// Adds a dependency, enforced by a new assumption. Since each decision takes exactly one option, the sum of the
            /// literals of a variant is 1 when the variant is taken and 0 otherwise.
            /// </summary>
            /// <param name="dependency">The <see cref="TradeOffDependency"/> to add.</param>
            /// <param name="description">The description of the dependency in a conflict.</param>
            private void AddDependency(TradeOffDependency dependency, string description)
            {
                var literal = this.model.NewBoolVar(description);
                var ifTaken = LinearExpr.Sum(this.GetVariantLiterals(dependency.If));

                if (dependency.Kind == TradeOffRuleKind.Requires)
                {
                    this.model.Add(LinearExpr.Sum(dependency.Then.SelectMany(this.GetVariantLiterals)) >= ifTaken).OnlyEnforceIf(literal);
                }
                else
                {
                    foreach (var reference in dependency.Then)
                    {
                        this.model.Add(ifTaken + LinearExpr.Sum(this.GetVariantLiterals(reference)) <= 1).OnlyEnforceIf(literal);
                    }
                }

                this.assumptions.Add((literal, description));
            }

            /// <summary>
            /// Gets the literals of the options of a variant.
            /// </summary>
            /// <param name="reference">The <see cref="TradeOffVariantReference"/> of the variant.</param>
            /// <returns>The literals of the options of the variant, one for each number of units.</returns>
            private IEnumerable<BoolVar> GetVariantLiterals(TradeOffVariantReference reference)
            {
                return this.problem.Decisions[reference.DecisionIndex].GetOptionIndexes(reference.Variant)
                    .Select(optionIndex => this.optionLiterals[reference.DecisionIndex][optionIndex]);
            }

            /// <summary>
            /// Gets the total of an attribute, in units, as a linear expression of the literals of the options.
            /// </summary>
            /// <param name="attribute">The name of the attribute.</param>
            /// <returns>The <see cref="LinearExpr"/> of the total.</returns>
            private LinearExpr GetTotalExpression(string attribute)
            {
                var terms = this.problem.Decisions
                    .SelectMany((decision, decisionIndex) => decision.Options.Select((option, optionIndex) =>
                        (Literal: this.optionLiterals[decisionIndex][optionIndex], Units: TradeOffScale.ToUnits(option.Values.GetValueOrDefault(attribute)))))
                    .ToList();

                return LinearExpr.WeightedSum(terms.Select(term => term.Literal), terms.Select(term => term.Units))
                    + TradeOffScale.ToUnits(this.problem.FixedTotals.GetValueOrDefault(attribute));
            }

            /// <summary>
            /// Gets the number of decisions whose option is not the current state, as a linear expression.
            /// </summary>
            /// <returns>The <see cref="LinearExpr"/> of the number of changes.</returns>
            private LinearExpr GetChangeCountExpression()
            {
                return LinearExpr.Sum(this.problem.Decisions
                    .SelectMany((decision, decisionIndex) => decision.Options
                        .Select((option, optionIndex) => (option, optionIndex))
                        .Where(pair => !pair.option.IsCurrent)
                        .Select(pair => this.optionLiterals[decisionIndex][pair.optionIndex])));
            }
        }
    }
}
