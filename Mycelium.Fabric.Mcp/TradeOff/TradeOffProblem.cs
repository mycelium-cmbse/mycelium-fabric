// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffProblem.cs" company="Starion Group S.A.">
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
    using System.Linq;

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// A trade-off problem read from the model by the <see cref="TradeOffProblemBuilder"/>, that an
    /// <see cref="ITradeOffSolver"/> solves without knowing SysML: the decisions and their options, the part of each total that
    /// no decision changes, the objectives, the limits and the dependencies between variants.
    /// </summary>
    /// <param name="Decisions">The <see cref="TradeOffDecision"/>s, in the order of the choices.</param>
    /// <param name="FixedTotals">
    /// The part of the total of each attribute that comes from the parts that no decision changes, by attribute name.
    /// </param>
    /// <param name="Objectives">The <see cref="TradeOffObjective"/>s, by order of priority.</param>
    /// <param name="Limits">
    /// The <see cref="TradeOffLimit"/>s: the constraints given with the request, then the limits read from the requirements of
    /// the model. A combination may break a limit when no combination meets them all: the fewer, the better.
    /// </param>
    /// <param name="Dependencies">The <see cref="TradeOffDependency"/>s that every combination respects.</param>
    /// <param name="Count">The number of combinations to find, from the best, for each alternative.</param>
    /// <param name="CompareDecisionIndex">
    /// The index of the decision whose variants are compared, each with its own best combinations, or <c>null</c> to rank all
    /// the combinations together.
    /// </param>
    /// <param name="AttributeUnits">
    /// The unit in which the values and the totals of each attribute are expressed, by attribute name, or <c>null</c> when no
    /// attribute has a unit.
    /// </param>
    /// <param name="Excluded">The variants that no combination takes, or <c>null</c> for none.</param>
    public sealed record TradeOffProblem(IReadOnlyList<TradeOffDecision> Decisions, IReadOnlyDictionary<string, double> FixedTotals, IReadOnlyList<TradeOffObjective> Objectives,
        IReadOnlyList<TradeOffLimit> Limits, IReadOnlyList<TradeOffDependency> Dependencies, int Count, int? CompareDecisionIndex,
        IReadOnlyDictionary<string, Unit> AttributeUnits = null, IReadOnlyList<TradeOffVariantReference> Excluded = null)
    {
        /// <summary>
        /// The largest number of totals read by the requirements of the model that are shown with each combination.
        /// </summary>
        public const int MaximumModelTotals = 12;

        /// <summary>
        /// Gets the requirements of the model that are checked on every combination.
        /// </summary>
        public IReadOnlyList<TradeOffRequirement> Requirements { get; init; } = [];

        /// <summary>
        /// Gets what the checks of the requirements have taught so far about the combinations.
        /// </summary>
        public IReadOnlyList<TradeOffPenalty> Penalties { get; init; } = [];

        /// <summary>
        /// Gets the <see cref="TradeOffDesignSpace"/> that tells what the parts hold in a combination, or <c>null</c> for a
        /// problem that is not read from a model.
        /// </summary>
        public TradeOffDesignSpace Space { get; init; }

        /// <summary>
        /// Gets the smallest and the largest value that the linear form of a limit takes over the combinations.
        /// </summary>
        /// <param name="limit">The <see cref="TradeOffLimit"/>.</param>
        /// <returns>The range, in units of the linear form.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="limit"/> is <c>null</c>.
        /// </exception>
        public (long Minimum, long Maximum) GetRange(TradeOffLimit limit)
        {
            ArgumentNullException.ThrowIfNull(limit);

            var form = limit.GetLinearForm();
            var (minimum, maximum) = Scale(this.GetTotalRange(limit.Attribute), form.TotalCoefficient);

            if (limit.LimitAttribute == null)
            {
                return (minimum, maximum);
            }

            var (limitMinimum, limitMaximum) = Scale(this.GetTotalRange(limit.LimitAttribute), form.LimitCoefficient);

            return (minimum + limitMinimum, maximum + limitMaximum);
        }

        /// <summary>
        /// Gets the size to which the excess of a broken limit is compared, in units of its linear form: the limit itself,
        /// or the current total that stands for it when the limit is the total of another attribute or is 0.
        /// </summary>
        /// <param name="limit">The <see cref="TradeOffLimit"/>.</param>
        /// <returns>The reference, at least 1.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="limit"/> is <c>null</c>.
        /// </exception>
        public long GetReference(TradeOffLimit limit)
        {
            ArgumentNullException.ThrowIfNull(limit);

            var form = limit.GetLinearForm();
            var current = this.GetCurrentSelection();

            long[] candidates =
            [
                limit.LimitAttribute == null ? form.Bound : form.LimitCoefficient * this.GetTotalUnits(limit.LimitAttribute, current),
                form.TotalCoefficient * this.GetTotalUnits(limit.Attribute, current),
                TradeOffScale.UnitsPerFactor * TradeOffScale.UnitsPerValue
            ];

            return candidates.Select(Math.Abs).First(candidate => candidate > 0);
        }

        /// <summary>
        /// Counts the limits that a combination breaks, with the penalties that apply to it: the number of requirements and
        /// constraints that it does not meet, as far as the checks done so far tell.
        /// </summary>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <returns>The number of broken limits.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <c>null</c>.
        /// </exception>
        public int CountBroken(IReadOnlyList<int> selection)
        {
            ArgumentNullException.ThrowIfNull(selection);

            return this.Limits.Count(limit => !this.IsSatisfied(limit, selection)) + this.Penalties.Where(penalty => penalty.AppliesTo(selection)).Sum(penalty => penalty.Weight);
        }

        /// <summary>
        /// Gets the smallest and the largest total of an attribute over the combinations, in units of
        /// <see cref="TradeOffScale"/>.
        /// </summary>
        /// <param name="attribute">The name of the attribute.</param>
        /// <returns>The range.</returns>
        private (long Minimum, long Maximum) GetTotalRange(string attribute)
        {
            var fixedUnits = TradeOffScale.ToUnits(this.FixedTotals.GetValueOrDefault(attribute));
            var optionUnits = this.Decisions.Select(decision => decision.Options.Select(option => TradeOffScale.ToUnits(option.Values.GetValueOrDefault(attribute))).ToList()).ToList();

            return (fixedUnits + optionUnits.Sum(units => units.Min()), fixedUnits + optionUnits.Sum(units => units.Max()));
        }

        /// <summary>
        /// Multiplies a range by a coefficient, which reverses it when it is negative.
        /// </summary>
        /// <param name="range">The range.</param>
        /// <param name="coefficient">The coefficient.</param>
        /// <returns>The multiplied range.</returns>
        private static (long Minimum, long Maximum) Scale((long Minimum, long Maximum) range, long coefficient)
        {
            return coefficient >= 0 ? (range.Minimum * coefficient, range.Maximum * coefficient) : (range.Maximum * coefficient, range.Minimum * coefficient);
        }

        /// <summary>
        /// Tells whether a combination may take an option: its variant is not turned off, and it is not the absence of a
        /// required equipment.
        /// </summary>
        /// <param name="decisionIndex">The index of the decision.</param>
        /// <param name="optionIndex">The index of the option in the decision.</param>
        /// <returns><c>true</c> when the option may be taken.</returns>
        public bool IsAllowed(int decisionIndex, int optionIndex)
        {
            var decision = this.Decisions[decisionIndex];
            var option = decision.Options[optionIndex];

            return !(decision.Required && option.IsCurrent) && !this.IsExcluded(new TradeOffVariantReference(decisionIndex, option.Variant));
        }

        /// <summary>
        /// Tells whether a variant is turned off.
        /// </summary>
        /// <param name="reference">The <see cref="TradeOffVariantReference"/> of the variant.</param>
        /// <returns><c>true</c> when no combination may take the variant.</returns>
        public bool IsExcluded(TradeOffVariantReference reference)
        {
            return this.Excluded?.Contains(reference) == true;
        }

        /// <summary>
        /// Lists every combination that takes allowed options only, in the order of the decisions and of their options, when
        /// there are not more than <paramref name="maximum"/> of them.
        /// </summary>
        /// <param name="maximum">The largest number of combinations to list.</param>
        /// <param name="selections">
        /// The selection of each combination, or an empty list when there are more than <paramref name="maximum"/>.
        /// </param>
        /// <returns><c>true</c> when there are not more than <paramref name="maximum"/> combinations.</returns>
        public bool TryEnumerateSelections(int maximum, out IReadOnlyList<IReadOnlyList<int>> selections)
        {
            var allowedOptions = this.Decisions
                .Select((decision, decisionIndex) => Enumerable.Range(0, decision.Options.Count).Where(optionIndex => this.IsAllowed(decisionIndex, optionIndex)).ToList())
                .ToList();

            long count = 1;

            foreach (var options in allowedOptions)
            {
                count *= options.Count;

                if (count > maximum)
                {
                    selections = [];
                    return false;
                }
            }

            IEnumerable<IReadOnlyList<int>> combinations = [[]];

            foreach (var options in allowedOptions)
            {
                combinations = combinations.SelectMany(selection => options.Select(optionIndex => (IReadOnlyList<int>)[.. selection, optionIndex])).ToList();
            }

            selections = [.. combinations];
            return true;
        }

        /// <summary>
        /// Tells whether a selection respects a dependency.
        /// </summary>
        /// <param name="dependency">The <see cref="TradeOffDependency"/> to check.</param>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <returns><c>true</c> when the dependency is respected.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="dependency"/> or <paramref name="selection"/> is <c>null</c>.
        /// </exception>
        public bool IsRespected(TradeOffDependency dependency, IReadOnlyList<int> selection)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            ArgumentNullException.ThrowIfNull(selection);

            if (!this.Takes(dependency.If, selection))
            {
                return true;
            }

            return dependency.Kind == TradeOffRuleKind.Requires
                ? dependency.Then.Any(reference => this.Takes(reference, selection))
                : !dependency.Then.Any(reference => this.Takes(reference, selection));
        }

        /// <summary>
        /// Tells whether a selection takes a variant.
        /// </summary>
        /// <param name="reference">The <see cref="TradeOffVariantReference"/> of the variant.</param>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <returns><c>true</c> when the selected option of the decision is of this variant.</returns>
        private bool Takes(TradeOffVariantReference reference, IReadOnlyList<int> selection)
        {
            return this.Decisions[reference.DecisionIndex].Options[selection[reference.DecisionIndex]].Variant == reference.Variant;
        }

        /// <summary>
        /// Gets the unit in which the totals and the values of an attribute are expressed.
        /// </summary>
        /// <param name="attribute">The name of the attribute.</param>
        /// <returns>The unit of the attribute, or <c>null</c> when it has none.</returns>
        public Unit GetUnit(string attribute)
        {
            return this.AttributeUnits?.GetValueOrDefault(attribute);
        }

        /// <summary>
        /// Gets the alternatives to search: each variant of the compared decision that may be taken, or a single
        /// <c>null</c> alternative when no decision is compared.
        /// </summary>
        /// <returns>The <see cref="TradeOffVariantReference"/> of each alternative, or a single <c>null</c>.</returns>
        public IReadOnlyList<TradeOffVariantReference> GetAlternatives()
        {
            if (this.CompareDecisionIndex is not { } decisionIndex)
            {
                return [null];
            }

            var decision = this.Decisions[decisionIndex];

            return
            [
                .. decision.Options
                    .Where(option => !(decision.Required && option.IsCurrent))
                    .Select(option => option.Variant)
                    .Distinct()
                    .Select(variant => new TradeOffVariantReference(decisionIndex, variant))
                    .Where(reference => !this.IsExcluded(reference))
            ];
        }

        /// <summary>
        /// Gets the names of the attributes of the problem, without duplicates: those of the objectives, then those of the
        /// limits, then the limit attributes.
        /// </summary>
        /// <returns>The names of the attributes.</returns>
        public IReadOnlyList<string> GetAttributes()
        {
            var givenLimits = this.Limits.Where(limit => !limit.IsFromModel).ToList();
            var modelLimits = this.Limits.Where(limit => limit.IsFromModel).ToList();

            // When the request names no attribute, the totals that the requirements of the model compare are shown instead,
            // unless there are too many of them.
            var modelTotals = modelLimits.Select(limit => limit.Attribute).Concat(modelLimits.Select(limit => limit.LimitAttribute)).Where(total => total != null).Distinct().ToList();
            var showsModelTotals = this.Objectives.Count == 0 && givenLimits.Count == 0 && modelTotals.Count <= MaximumModelTotals;

            return
            [
                .. this.Objectives.Select(objective => objective.Attribute)
                    .Concat(givenLimits.Select(limit => limit.Attribute))
                    .Concat(givenLimits.Select(limit => limit.LimitAttribute))
                    .Concat(showsModelTotals ? modelTotals : [])
                    .Where(attribute => attribute != null)
                    .Distinct()
            ];
        }

        /// <summary>
        /// Gets the selection of the current state: the index of the current option of each decision.
        /// </summary>
        /// <returns>The index of the current option of each decision.</returns>
        public IReadOnlyList<int> GetCurrentSelection()
        {
            return [.. this.Decisions.Select(decision => decision.CurrentOptionIndex)];
        }

        /// <summary>
        /// Computes the total of an attribute for a selection, in units of <see cref="TradeOffScale"/>: the fixed part plus the
        /// value of the selected option of each decision.
        /// </summary>
        /// <param name="attribute">The name of the attribute.</param>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <returns>The total in units.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <c>null</c>.
        /// </exception>
        public long GetTotalUnits(string attribute, IReadOnlyList<int> selection)
        {
            ArgumentNullException.ThrowIfNull(selection);

            return TradeOffScale.ToUnits(this.FixedTotals.GetValueOrDefault(attribute))
                + this.Decisions.Select((decision, index) => TradeOffScale.ToUnits(decision.Options[selection[index]].Values.GetValueOrDefault(attribute))).Sum();
        }

        /// <summary>
        /// Tells whether a selection respects a limit, with the arithmetic of the solver.
        /// </summary>
        /// <param name="limit">The <see cref="TradeOffLimit"/> to check.</param>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <returns><c>true</c> when the limit is respected.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="limit"/> or <paramref name="selection"/> is <c>null</c>.
        /// </exception>
        public bool IsSatisfied(TradeOffLimit limit, IReadOnlyList<int> selection)
        {
            ArgumentNullException.ThrowIfNull(limit);

            var limitTotal = limit.LimitAttribute == null ? 0 : this.GetTotalUnits(limit.LimitAttribute, selection);

            return limit.GetLinearForm().IsSatisfiedBy(this.GetTotalUnits(limit.Attribute, selection), limitTotal);
        }

        /// <summary>
        /// Counts the decisions whose selected option is not the current state.
        /// </summary>
        /// <param name="selection">The index of the selected option of each decision.</param>
        /// <returns>The number of changes.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <c>null</c>.
        /// </exception>
        public int CountChanges(IReadOnlyList<int> selection)
        {
            ArgumentNullException.ThrowIfNull(selection);

            return this.Decisions.Where((decision, index) => !decision.Options[selection[index]].IsCurrent).Count();
        }
    }
}
