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
    /// <param name="Limits">The <see cref="TradeOffLimit"/>s that every combination respects.</param>
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
    public sealed record TradeOffProblem(IReadOnlyList<TradeOffDecision> Decisions, IReadOnlyDictionary<string, double> FixedTotals, IReadOnlyList<TradeOffObjective> Objectives,
        IReadOnlyList<TradeOffLimit> Limits, IReadOnlyList<TradeOffDependency> Dependencies, int Count, int? CompareDecisionIndex,
        IReadOnlyDictionary<string, Unit> AttributeUnits = null)
    {
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
            ];
        }

        /// <summary>
        /// Gets the names of the attributes of the problem, without duplicates: those of the objectives, then those of the
        /// limits, then the limit attributes.
        /// </summary>
        /// <returns>The names of the attributes.</returns>
        public IReadOnlyList<string> GetAttributes()
        {
            return
            [
                .. this.Objectives.Select(objective => objective.Attribute)
                    .Concat(this.Limits.Select(limit => limit.Attribute))
                    .Concat(this.Limits.Select(limit => limit.LimitAttribute))
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
