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
        /// <param name="verifier">
        /// The <see cref="TradeOffVerifier"/> that checks the requirements of the model on the combinations, or <c>null</c> to
        /// create one.
        /// </param>
        /// <returns>The <see cref="TradeOffStudy"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/>, <paramref name="problem"/> or <paramref name="solution"/> is <c>null</c>.
        /// </exception>
        public static TradeOffStudy Build(TradeOffRequest request, TradeOffRoot root, TradeOffProblem problem, TradeOffSolution solution, TradeOffVerifier verifier = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(problem);
            ArgumentNullException.ThrowIfNull(solution);

            verifier ??= new TradeOffVerifier(problem);

            var currentSelection = problem.GetCurrentSelection();
            var compareName = problem.CompareDecisionIndex is { } compareIndex ? problem.Decisions[compareIndex].Name : null;

            var best = solution.Alternatives
                .SelectMany(alternative => alternative.Selections.Select((selection, index) =>
                    CreateCombination(problem, verifier, selection, currentSelection, Describe(compareName, alternative), index + 1)))
                .ToList();

            var conflicts = solution.Alternatives
                .SelectMany(alternative => alternative.Conflicts.Select(conflict => compareName == null ? conflict : $"{Describe(compareName, alternative)}: {conflict}"))
                .ToList();

            var pointsComplete = problem.TryEnumerateSelections(MaximumPoints, out var allSelections);
            IReadOnlyList<IReadOnlyList<int>> pointSelections = pointsComplete ? allSelections : GetNearSelections(problem, [.. solution.Alternatives.SelectMany(alternative => alternative.Selections)]);

            var settings = request with
            {
                Choices = request.Choices ?? [],
                Objectives = request.Objectives ?? [],
                Constraints = request.Constraints ?? [],
                Excluded = request.Excluded ?? [],
                IgnoredRequirements = request.IgnoredRequirements ?? []
            };

            var attributes = problem.GetAttributes().Select(attribute => new TradeOffAttribute(attribute, problem.GetUnit(attribute))).ToList();
            var choices = problem.Decisions.Select((decision, decisionIndex) => CreateChoice(problem, decision, decisionIndex)).ToList();
            var constraints = problem.Limits.Where(limit => !limit.IsFromModel).Select(CreateConstraint).ToList();
            var dependencies = problem.Dependencies.Select(dependency => dependency.Description).ToList();
            var current = CreateCombination(problem, verifier, currentSelection, currentSelection, null, 0);
            var points = CreatePoints(problem, verifier, pointSelections, currentSelection);
            var compared = best.Prepend(current).Select(IReadOnlyDictionary<string, double> (combination) => combination.Totals.ToDictionary(total => total.Attribute, total => total.Total)).ToList();
            var axes = TradeOffAxes.Choose([.. attributes.Select(attribute => attribute.Name)], [.. problem.Objectives.Select(objective => objective.Attribute)], compared, [.. points.Select(point => point.Totals)]);
            var requirementChecks = verifier.Verify(currentSelection, true);
            var requirements = requirementChecks
                .Where(check => check.DependsOnChoices || !check.IsMet || check.Requirement.IsIgnored)
                .Select(check => CreateRequirement(problem, check))
                .ToList();

            return new TradeOffStudy(request.Title, root, solution.Status, settings, attributes, choices, problem.Objectives, constraints, dependencies, current, best,
                points, pointsComplete, conflicts)
            {
                Requirements = requirements,
                OtherRequirementCount = requirementChecks.Count - requirements.Count,
                Axes = axes
            };
        }

        /// <summary>
        /// Gets the combinations listed when there are too many of them to list them all: the best ones, the current state,
        /// then those that are one choice away from the current state and from the best one, which show what a single change
        /// does.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="best">The selections of the best combinations.</param>
        /// <returns>The selections, without duplicate, at most <see cref="MaximumPoints"/>.</returns>
        private static List<IReadOnlyList<int>> GetNearSelections(TradeOffProblem problem, IReadOnlyList<IReadOnlyList<int>> best)
        {
            IReadOnlyList<int>[] origins = best.Count == 0 ? [problem.GetCurrentSelection()] : [problem.GetCurrentSelection(), best[0]];

            var near = origins.SelectMany(origin => problem.Decisions.SelectMany((decision, decisionIndex) => Enumerable.Range(0, decision.Options.Count)
                .Where(optionIndex => optionIndex != origin[decisionIndex])
                .Select(optionIndex => (IReadOnlyList<int>)[.. origin.Take(decisionIndex), optionIndex, .. origin.Skip(decisionIndex + 1)])));

            return
            [
                .. best.Concat(origins).Concat(near)
                    .Where(selection => selection.Select((optionIndex, decisionIndex) => problem.IsAllowed(decisionIndex, optionIndex)).All(isAllowed => isAllowed))
                    .DistinctBy(selection => string.Join(',', selection))
                    .Take(MaximumPoints)
            ];
        }

        /// <summary>
        /// Describes a requirement of the model, as it is in the current state.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="check">The check of the requirement on the current state.</param>
        /// <returns>The <see cref="TradeOffStudyRequirement"/>.</returns>
        private static TradeOffStudyRequirement CreateRequirement(TradeOffProblem problem, TradeOffRequirementCheck check)
        {
            var limit = check.Requirement.LimitIndex is { } limitIndex ? problem.Limits[limitIndex] : null;

            return new TradeOffStudyRequirement(check.Check.Id, check.Requirement.Name, check.Check.Text, check.Check.Constraint, check.Check.SatisfiedBy, check.Check.Status,
                check.Check.Explanation, check.DependsOnChoices, check.Requirement.IsIgnored, limit != null, limit?.Attribute, limit?.Symbol, limit?.Threshold);
        }

        /// <summary>
        /// Lists what a combination does to the requirements of the model that depend on the choices, those that it does not
        /// meet first.
        /// </summary>
        /// <param name="verifier">The <see cref="TradeOffVerifier"/>.</param>
        /// <param name="selection">The selection of the combination.</param>
        /// <param name="currentSelection">The selection of the current state.</param>
        /// <returns>The <see cref="RequirementOutcome"/>s.</returns>
        private static List<RequirementOutcome> CreateOutcomes(TradeOffVerifier verifier, IReadOnlyList<int> selection, IReadOnlyList<int> currentSelection)
        {
            return
            [
                .. verifier.Verify(selection)
                    .Zip(verifier.Verify(currentSelection))
                    .Where(pair => pair.First.DependsOnChoices || pair.Second.DependsOnChoices)
                    .OrderBy(pair => pair.First.IsMet)
                    .Select(pair => new RequirementOutcome(pair.First.Requirement.Name, pair.First.Check.SatisfiedBy, pair.First.Check.Status, pair.First.Check.Explanation,
                        pair.First.Check.Status != pair.Second.Check.Status))
            ];
        }

        /// <summary>
        /// Describes a constraint of the problem: a numeric limit, or the attribute whose total is the limit.
        /// </summary>
        /// <param name="limit">The <see cref="TradeOffLimit"/> of the constraint.</param>
        /// <returns>The <see cref="TradeOffStudyConstraint"/>.</returns>
        private static TradeOffStudyConstraint CreateConstraint(TradeOffLimit limit)
        {
            var numericLimit = limit.LimitAttribute == null ? limit.Limit : null;

            return new TradeOffStudyConstraint(limit.Name, limit.Description, limit.Attribute, limit.Symbol, numericLimit, limit.LimitAttribute, limit.Margin, limit.Threshold);
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
            var shownAttributes = problem.GetAttributes().ToHashSet();

            var variants = decision.Options
                .GroupBy(option => option.Variant)
                .Select(group =>
                {
                    var smallest = group.MinBy(option => option.Quantity);
                    // The values shown are those of the totals shown: the totals that only a requirement reads stay out.
                    var values = smallest.Values
                        .Where(pair => shownAttributes.Contains(pair.Key))
                        .ToDictionary(pair => pair.Key, pair => Math.Round(smallest.Quantity > 0 ? pair.Value / smallest.Quantity : pair.Value, 3));

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
        /// <param name="verifier">The <see cref="TradeOffVerifier"/>.</param>
        /// <param name="selections">The selections of the combinations.</param>
        /// <param name="currentSelection">The selection of the current state.</param>
        /// <returns>The <see cref="TradeOffPoint"/> of each combination, in the order of <paramref name="selections"/>.</returns>
        private static List<TradeOffPoint> CreatePoints(TradeOffProblem problem, TradeOffVerifier verifier, IReadOnlyList<IReadOnlyList<int>> selections,
            IReadOnlyList<int> currentSelection)
        {
            var broken = selections.Select(selection => GetBroken(problem, verifier, selection)).ToList();

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
        /// Lists the constraints, the requirements of the model and the rules that a combination breaks, with its values.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/>.</param>
        /// <param name="verifier">The <see cref="TradeOffVerifier"/>.</param>
        /// <param name="selection">The selection of the combination.</param>
        /// <returns>The text of each breach, for example <c>REQ-SYS-001: mass * 1.2 = 150.96 kg &gt; 150 kg</c>.</returns>
        private static List<string> GetBroken(TradeOffProblem problem, TradeOffVerifier verifier, IReadOnlyList<int> selection)
        {
            var brokenRequirements = verifier.Verify(selection)
                .Where(check => check.DependsOnChoices && !check.IsMet)
                .Select(check => $"{check.Requirement.Name}: {check.Check.Explanation}");

            var brokenLimits = problem.Limits
                .Select((limit, index) => (limit, index))
                .Where(pair => !pair.limit.IsFromModel && !problem.IsSatisfied(pair.limit, selection))
                .Select(pair =>
                {
                    var (value, limitValue) = GetComparedValues(problem, pair.limit, selection);
                    return $"{pair.limit.Name ?? $"constraint {pair.index + 1}"}: {pair.limit.DescribeBreach(value, limitValue)}";
                });

            var brokenRules = problem.Dependencies
                .Select((dependency, index) => (dependency, index))
                .Where(pair => !problem.IsRespected(pair.dependency, selection))
                .Select(pair => $"rule {pair.index + 1}: {pair.dependency.Description}");

            return [.. brokenLimits.Concat(brokenRequirements).Concat(brokenRules)];
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
        /// <param name="verifier">The <see cref="TradeOffVerifier"/>.</param>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <param name="currentSelection">The selection of the current state, to compute the differences.</param>
        /// <param name="alternative">The compared variant that the selection takes, or <c>null</c>.</param>
        /// <param name="rank">The rank of the selection, 0 for the current state.</param>
        /// <returns>The <see cref="TradeOffCombination"/> of the selection.</returns>
        private static TradeOffCombination CreateCombination(TradeOffProblem problem, TradeOffVerifier verifier, IReadOnlyList<int> selection, IReadOnlyList<int> currentSelection,
            string alternative, int rank)
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
                .Where(limit => !limit.IsFromModel)
                .Select(limit => (limit, values: GetComparedValues(problem, limit, selection)))
                .Select(pair => new ConstraintCheck(pair.limit.Description, problem.IsSatisfied(pair.limit, selection), pair.limit.Name, pair.values.Value, pair.values.Limit))
                .ToList();

            return new TradeOffCombination(alternative, rank, problem.CountChanges(selection), choices, totals, constraints)
            {
                Requirements = CreateOutcomes(verifier, selection, currentSelection)
            };
        }
    }
}
