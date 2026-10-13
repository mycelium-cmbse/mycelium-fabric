// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffTools.cs" company="Starion Group S.A.">
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
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// The MCP tools that let an AI assistant find the best combination of equipment for what matters to the user, with a
    /// constraint solver, instead of searching it itself.
    /// </summary>
    [McpServerToolType]
    public class TradeOffTools
    {
        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="ITradeOffSolver"/> that finds the best combinations.
        /// </summary>
        private readonly ITradeOffSolver solver;

        /// <summary>
        /// Initializes a new instance of the <see cref="TradeOffTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="solver">The <see cref="ITradeOffSolver"/> that finds the best combinations.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> or <paramref name="solver"/> is <c>null</c>.
        /// </exception>
        public TradeOffTools(IModelProvider modelProvider, ITradeOffSolver solver)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(solver);

            this.modelProvider = modelProvider;
            this.solver = solver;
        }

        /// <summary>
        /// Finds the best combinations of variants for the given choices, without modifying the model.
        /// </summary>
        /// <param name="rootId">The <c>Id</c> of the element under which the totals are computed.</param>
        /// <param name="choices">The <see cref="TradeOffChoice"/>s to make.</param>
        /// <param name="objectives">The <see cref="TradeOffObjective"/>s, by order of priority, or <c>null</c> for none.</param>
        /// <param name="constraints">The <see cref="TradeOffConstraint"/>s, or <c>null</c> for none.</param>
        /// <param name="rules">The <see cref="TradeOffRule"/>s, or <c>null</c> for none.</param>
        /// <param name="compareBy">The name of the choice whose variants are compared, or <c>null</c>.</param>
        /// <param name="count">The number of combinations to return, from the best, for each alternative.</param>
        /// <returns>The <see cref="TradeOffResult"/> that gives the current state and the best combinations.</returns>
        /// <exception cref="McpException">
        /// Thrown when the request is invalid, with a message that tells the AI assistant what to correct.
        /// </exception>
        [McpServerTool(Name = "solve_trade_off", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
        [Description("Finds, with a constraint solver, the best combinations of equipment for what matters to the user (the lightest, the least power hungry...), "
            + "with or without budgets. Use it to compare options (which engine, which camera, how many solar panels) instead of searching yourself. "
            + "First read the current parts and values (sum_attribute, get_attribute_values) and ask the user for the candidate variants and their values. "
            + "A choice is an existing part (partId; its sub-parts follow it) or equipment to add (name), and takes exactly one variant: "
            + "'current' (an existing part as it is) or 'none' (equipment not added) is added by the server, 'none' on an existing part removes it, "
            + "and a variant may be taken several times (minQuantity, maxQuantity). "
            + "A choice often leads to others: write them as rules (a thermal engine requires a fuel tank) and as constraints between totals "
            + "(power <= powerOutput, so that the solar panels feed the equipment). Totals are sums under the root, as sum_attribute computes them, "
            + "in the unit of the first part that has one; a value or a limit may be given in another unit of the same dimension (units, unit: 500 g, 2 kg), the server converts it. "
            + "Objectives are optimized in their order of priority, then the fewest changes. To compare alternatives (thermal against electric), "
            + "give compareBy: each variant of this choice then gets its own best combination, with all its consequences. "
            + "Nothing is modified: present the combinations, let the user choose, then use apply_changes.")]
        [return: Description("The status, the current state and the best combinations (the compared alternative, the variant of each choice, the totals with their "
            + "difference from the current state, and whether each constraint is met), and the constraints and rules that conflict when no combination exists.")]
        public TradeOffResult SolveTradeOff(
            [Description("The identifier (Id, a GUID) of the element under which the totals are computed, for example the satellite.")] Guid rootId,
            [Description("The choices to make: existing parts or equipment to add, each with its variants.")] IReadOnlyList<TradeOffChoice> choices,
            [Description("Optional: what matters to the user, by order of priority, for example the lowest mass, then the lowest power. "
                + "Without objective: the combination closest to the current state that meets the constraints.")] IReadOnlyList<TradeOffObjective> objectives = null,
            [Description("Optional: the budgets that every combination meets, for example mass <= 150 with a margin of 20 %, or power <= powerOutput.")] IReadOnlyList<TradeOffConstraint> constraints = null,
            [Description("Optional: the dependencies between variants of different choices, for example propulsion.thermal requires fuelTank.small or fuelTank.large.")] IReadOnlyList<TradeOffRule> rules = null,
            [Description("Optional: the name of a choice whose variants are compared, for example 'propulsion': each variant gets its own best combinations, "
                + "or the conflicts that make it impossible.")] string compareBy = null,
            [Description("Optional, 1 by default: the number of combinations to return, from 1 to 5, for each compared variant.")] int count = 1)
        {
            var problem = TradeOffProblemBuilder.Build(this.modelProvider, new TradeOffRequest(rootId, choices, objectives, constraints, rules, count, compareBy));
            var solution = this.solver.Solve(problem);
            var currentSelection = problem.GetCurrentSelection();
            var compareName = problem.CompareDecisionIndex is { } compareIndex ? problem.Decisions[compareIndex].Name : null;

            var combinations = solution.Alternatives
                .SelectMany(alternative => alternative.Selections.Select((selection, index) =>
                    CreateCombination(problem, selection, currentSelection, Describe(compareName, alternative), index + 1)))
                .ToList();

            var conflicts = solution.Alternatives
                .SelectMany(alternative => alternative.Conflicts.Select(conflict => compareName == null ? conflict : $"{Describe(compareName, alternative)}: {conflict}"))
                .ToList();

            return new TradeOffResult(solution.Status, CreateCombination(problem, currentSelection, currentSelection, null, 0), combinations, conflicts);
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
                .Select(limit => new ConstraintCheck(limit.Description, problem.IsSatisfied(limit, selection)))
                .ToList();

            return new TradeOffCombination(alternative, rank, problem.CountChanges(selection), choices, totals, constraints);
        }
    }
}
