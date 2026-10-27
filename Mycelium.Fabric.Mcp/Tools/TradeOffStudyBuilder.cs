// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffStudyBuilder.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// Describes a solved <see cref="TradeOffProblem"/> as a <see cref="TradeOffStudy"/>: the best combinations of the solver,
    /// the settings as the server understood them, and every combination with its totals and the constraints it breaks.
    /// </summary>
    public static class TradeOffStudyBuilder
    {
        /// <summary>
        /// The largest number of combinations listed as points; above it, only the best combinations are.
        /// </summary>
        public const int MaximumPoints = 500;

        /// <summary>
        /// Builds the study of a solved problem.
        /// </summary>
        /// <param name="request">The <see cref="TradeOffRequest"/> of the problem.</param>
        /// <param name="root">The <see cref="TradeOffRoot"/> under which the totals are computed.</param>
        /// <param name="problem">The solved <see cref="TradeOffProblem"/>.</param>
        /// <param name="solution">The <see cref="TradeOffSolution"/> of the solver.</param>
        /// <returns>The <see cref="TradeOffStudy"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/>, <paramref name="problem"/> or <paramref name="solution"/> is <c>null</c>.
        /// </exception>
        public static TradeOffStudy Build(TradeOffRequest request, TradeOffRoot root, TradeOffProblem problem, TradeOffSolution solution)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(problem);
            ArgumentNullException.ThrowIfNull(solution);

            var currentSelection = problem.GetCurrentSelection();
            var compareName = problem.CompareDecisionIndex is { } compareIndex ? problem.Decisions[compareIndex].Name : null;

            var best = solution.Alternatives
                .SelectMany(alternative => alternative.Selections.Select((selection, index) =>
                    CreateCombination(problem, selection, currentSelection, Describe(compareName, alternative), index + 1)))
                .ToList();

            var conflicts = solution.Alternatives
                .SelectMany(alternative => alternative.Conflicts.Select(conflict => compareName == null ? conflict : $"{Describe(compareName, alternative)}: {conflict}"))
                .ToList();

            var allSelections = problem.EnumerateSelections(MaximumPoints);
            var pointSelections = allSelections ?? [.. solution.Alternatives.SelectMany(alternative => alternative.Selections)];

            return new TradeOffStudy(
                request.Title,
                root,
                solution.Status,
                request with { Excluded = request.Excluded ?? [] },
                [.. problem.GetAttributes().Select(attribute => new TradeOffAttribute(attribute, problem.GetUnit(attribute)))],
                [.. problem.Decisions.Select((decision, decisionIndex) => CreateChoice(problem, decision, decisionIndex))],
                problem.Objectives,
                [.. problem.Limits.Select(limit => new TradeOffStudyConstraint(limit.Name, limit.Description, limit.Attribute, limit.Symbol, limit.LimitAttribute == null ? limit.Limit : null,
                    limit.LimitAttribute, limit.Margin, limit.Threshold))],
                [.. problem.Dependencies.Select(dependency => dependency.Description)],
                CreateCombination(problem, currentSelection, currentSelection, null, 0),
                best,
                CreatePoints(problem, pointSelections, currentSelection),
                allSelections != null,
                conflicts);
        }

        /// <summary>
        /// Describes a choice with all its variants.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="decision">The <see cref="TradeOffDecision"/> of the choice.</param>
        /// <param name="decisionIndex">The index of the decision.</param>
        /// <returns>The <see cref="TradeOffStudyChoice"/>.</returns>
        private static TradeOffStudyChoice CreateChoice(TradeOffProblem problem, TradeOffDecision decision, int decisionIndex)
        {
            var variants = decision.Options
                .GroupBy(option => option.Variant)
                .Select(group =>
                {
                    var smallest = group.MinBy(option => option.Quantity);
                    var values = smallest.Values.ToDictionary(pair => pair.Key, pair => Math.Round(smallest.Quantity > 0 ? pair.Value / smallest.Quantity : pair.Value, 3));

                    return new TradeOffStudyVariant(group.Key, group.Any(option => option.IsCurrent), problem.IsExcluded(new TradeOffVariantReference(decisionIndex, group.Key)),
                        smallest.Quantity, group.Max(option => option.Quantity), values);
                });

            return new TradeOffStudyChoice(decision.Name, decision.PartId, decision.QualifiedName, [.. variants]);
        }

        /// <summary>
        /// Describes combinations as points: their totals, whether they are feasible and why not, and the rank of the feasible
        /// ones, ordered like the solver orders them: by each objective in turn, then by the fewest changes.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="selections">The selections of the combinations.</param>
        /// <param name="currentSelection">The selection of the current state.</param>
        /// <returns>The <see cref="TradeOffPoint"/> of each combination, in the order of <paramref name="selections"/>.</returns>
        private static List<TradeOffPoint> CreatePoints(TradeOffProblem problem, IReadOnlyList<IReadOnlyList<int>> selections, IReadOnlyList<int> currentSelection)
        {
            var broken = selections.Select(selection => GetBroken(problem, selection)).ToList();

            var ranks = Enumerable.Range(0, selections.Count)
                .Where(index => broken[index].Count == 0)
                .OrderBy(index => GetRankingKey(problem, selections[index]), Comparer<IReadOnlyList<long>>.Create(CompareKeys))
                .Select((index, position) => (index, rank: position + 1))
                .ToDictionary(pair => pair.index, pair => pair.rank);

            return
            [
                .. selections.Select((selection, index) => new TradeOffPoint(
                    [.. problem.Decisions.Select((decision, decisionIndex) => decision.Options[selection[decisionIndex]])
                        .Select((option, decisionIndex) => new TradeOffPointChoice(problem.Decisions[decisionIndex].Name, option.Variant, option.Quantity))],
                    problem.GetAttributes().ToDictionary(attribute => attribute, attribute => TradeOffScale.ToValue(problem.GetTotalUnits(attribute, selection))),
                    broken[index].Count == 0,
                    ranks.TryGetValue(index, out var rank) ? rank : null,
                    selection.SequenceEqual(currentSelection),
                    broken[index]))
            ];
        }

        /// <summary>
        /// Lists the constraints and rules that a combination breaks, with its values.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="selection">The selection of the combination.</param>
        /// <returns>The text of each breach, for example <c>REQ-SYS-001: mass * 1.2 = 150.96 kg &gt; 150 kg</c>.</returns>
        private static List<string> GetBroken(TradeOffProblem problem, IReadOnlyList<int> selection)
        {
            var brokenLimits = problem.Limits
                .Select((limit, index) => (limit, index))
                .Where(pair => !problem.IsSatisfied(pair.limit, selection))
                .Select(pair =>
                {
                    var (value, limitValue) = GetComparedValues(problem, pair.limit, selection);
                    return $"{pair.limit.Name ?? $"constraint {pair.index + 1}"}: {pair.limit.DescribeBreach(value, limitValue)}";
                });

            var brokenRules = problem.Dependencies
                .Select((dependency, index) => (dependency, index))
                .Where(pair => !problem.IsRespected(pair.dependency, selection))
                .Select(pair => $"rule {pair.index + 1}: {pair.dependency.Description}");

            return [.. brokenLimits.Concat(brokenRules)];
        }

        /// <summary>
        /// Gets the key by which the solver ranks a combination: the total of each objective, negated when it is maximized, then
        /// the number of changes.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="selection">The selection of the combination.</param>
        /// <returns>The key, the smallest first.</returns>
        private static IReadOnlyList<long> GetRankingKey(TradeOffProblem problem, IReadOnlyList<int> selection)
        {
            return
            [
                .. problem.Objectives.Select(objective => objective.Direction == TradeOffDirection.Maximize
                    ? -problem.GetTotalUnits(objective.Attribute, selection)
                    : problem.GetTotalUnits(objective.Attribute, selection)),
                problem.CountChanges(selection)
            ];
        }

        /// <summary>
        /// Compares two ranking keys, element by element.
        /// </summary>
        /// <param name="first">The first key.</param>
        /// <param name="second">The second key.</param>
        /// <returns>A negative number when <paramref name="first"/> comes first, 0 when they are equal, a positive number otherwise.</returns>
        private static int CompareKeys(IReadOnlyList<long> first, IReadOnlyList<long> second)
        {
            return first.Zip(second, (left, right) => left.CompareTo(right)).FirstOrDefault(comparison => comparison != 0);
        }

        /// <summary>
        /// Gets the two sides of the comparison of a constraint for a combination.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="limit">The <see cref="TradeOffLimit"/> of the constraint.</param>
        /// <param name="selection">The selection of the combination.</param>
        /// <returns>The compared value and the limit it is compared with.</returns>
        private static (double Value, double Limit) GetComparedValues(TradeOffProblem problem, TradeOffLimit limit, IReadOnlyList<int> selection)
        {
            var limitTotal = limit.LimitAttribute == null ? 0 : TradeOffScale.ToValue(problem.GetTotalUnits(limit.LimitAttribute, selection));

            return limit.GetComparedValues(TradeOffScale.ToValue(problem.GetTotalUnits(limit.Attribute, selection)), limitTotal);
        }

        /// <summary>
        /// Writes the compared variant of an alternative as <c>choice.variant</c>.
        /// </summary>
        /// <param name="compareName">The name of the compared choice, or <c>null</c> when no choice is compared.</param>
        /// <param name="alternative">The <see cref="TradeOffAlternative"/> to describe.</param>
        /// <returns>The text of the compared variant, or <c>null</c> when no choice is compared.</returns>
        private static string Describe(string compareName, TradeOffAlternative alternative)
        {
            return compareName == null ? null : $"{compareName}.{alternative.Variant}";
        }

        /// <summary>
        /// Describes a selection of the problem: the variant of each choice, the totals and the constraints.
        /// </summary>
        /// <param name="problem">The solved <see cref="TradeOffProblem"/>.</param>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <param name="currentSelection">The selection of the current state, to compute the differences.</param>
        /// <param name="alternative">The compared variant that the selection takes, or <c>null</c>.</param>
        /// <param name="rank">The rank of the selection, 0 for the current state.</param>
        /// <returns>The <see cref="TradeOffCombination"/> of the selection.</returns>
        private static TradeOffCombination CreateCombination(TradeOffProblem problem, IReadOnlyList<int> selection, IReadOnlyList<int> currentSelection, string alternative, int rank)
        {
            var choices = problem.Decisions
                .Select((decision, index) => (decision, option: decision.Options[selection[index]]))
                .Select(pair => new ChosenVariant(pair.decision.Name, pair.decision.PartId, pair.decision.QualifiedName, pair.option.Variant, pair.option.Quantity, !pair.option.IsCurrent))
                .ToList();

            var totals = problem.GetAttributes()
                .Select(attribute => (attribute, units: problem.GetTotalUnits(attribute, selection), currentUnits: problem.GetTotalUnits(attribute, currentSelection)))
                .Select(total => new AttributeTotal(total.attribute, TradeOffScale.ToValue(total.units), TradeOffScale.ToValue(total.units - total.currentUnits), problem.GetUnit(total.attribute)))
                .ToList();

            var constraints = problem.Limits
                .Select(limit => (limit, values: GetComparedValues(problem, limit, selection)))
                .Select(pair => new ConstraintCheck(pair.limit.Description, problem.IsSatisfied(pair.limit, selection), pair.limit.Name, pair.values.Value, pair.values.Limit))
                .ToList();

            return new TradeOffCombination(alternative, rank, problem.CountChanges(selection), choices, totals, constraints);
        }
    }
}
