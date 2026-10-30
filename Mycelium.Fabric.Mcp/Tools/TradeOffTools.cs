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
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using ModelContextProtocol;
    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Resources;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// The MCP tools that let an AI assistant find the best combination of equipment for what matters to the user, with a
    /// constraint solver, instead of searching it itself, and that let the user explore the result in a view.
    /// </summary>
    [McpServerToolType]
    public class TradeOffTools
    {
        /// <summary>
        /// The <see cref="JsonSerializerOptions"/> of the <see cref="TradeOffStudy"/> sent to the view: a key whose value is
        /// <c>null</c> is still written, because the output schema of explore_trade_off requires every key of the study and
        /// hosts check the result against it.
        /// </summary>
        public static readonly JsonSerializerOptions StudySerializerOptions = new(McpJsonUtilities.DefaultOptions) { DefaultIgnoreCondition = JsonIgnoreCondition.Never };

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="ITradeOffSolver"/> that finds the best combinations.
        /// </summary>
        private readonly ITradeOffSolver solver;

        /// <summary>
        /// The <see cref="IAppHostDetector"/> that tells whether the host shows the view of a trade-off.
        /// </summary>
        private readonly IAppHostDetector appHostDetector;

        /// <summary>
        /// Initializes a new instance of the <see cref="TradeOffTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="solver">The <see cref="ITradeOffSolver"/> that finds the best combinations.</param>
        /// <param name="appHostDetector">The <see cref="IAppHostDetector"/> that tells whether the host shows MCP Apps.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/>, <paramref name="solver"/> or <paramref name="appHostDetector"/> is <c>null</c>.
        /// </exception>
        public TradeOffTools(IModelProvider modelProvider, ITradeOffSolver solver, IAppHostDetector appHostDetector)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(solver);
            ArgumentNullException.ThrowIfNull(appHostDetector);

            this.modelProvider = modelProvider;
            this.solver = solver;
            this.appHostDetector = appHostDetector;
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
        /// <param name="title">The short title of the trade-off, shown by its view, or <c>null</c>.</param>
        /// <param name="server">The <see cref="McpServer"/> of the request, which tells what the host can show.</param>
        /// <returns>
        /// The <see cref="CallToolResult"/>: for the AI assistant, the <see cref="TradeOffResult"/> in JSON; for the view, when the
        /// host shows MCP Apps, the <see cref="TradeOffStudy"/> as structured content.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when the request is invalid, with a message that tells the AI assistant what to correct.
        /// </exception>
        [McpServerTool(Name = "solve_trade_off", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
        [McpAppUi(ResourceUri = TradeOffResource.Uri)]
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
            + "The host may also show the result in a view, where the user changes the settings and may send you the combination they choose. "
            + "Nothing is modified: present the combinations, let the user choose, then use apply_changes.")]
        [return: Description("The status, the current state and the best combinations (the compared alternative, the variant of each choice, the totals with their "
            + "difference from the current state, and each constraint with its compared value, its limit and whether it is met), "
            + "and the constraints and rules that conflict when no combination exists.")]
        public CallToolResult SolveTradeOff(
            [Description("The identifier (Id, a GUID) of the element under which the totals are computed, for example the satellite.")] Guid rootId,
            [Description("The choices to make: existing parts or equipment to add, each with its variants.")] IReadOnlyList<TradeOffChoice> choices,
            [Description("Optional: what matters to the user, by order of priority, for example the lowest mass, then the lowest power. "
                + "Without objective: the combination closest to the current state that meets the constraints.")] IReadOnlyList<TradeOffObjective> objectives = null,
            [Description("Optional: the budgets that every combination meets, for example mass <= 150 with a margin of 20 %, or power <= powerOutput.")] IReadOnlyList<TradeOffConstraint> constraints = null,
            [Description("Optional: the dependencies between variants of different choices, for example propulsion.thermal requires fuelTank.small or fuelTank.large.")] IReadOnlyList<TradeOffRule> rules = null,
            [Description("Optional: the name of a choice whose variants are compared, for example 'propulsion': each variant gets its own best combinations, "
                + "or the conflicts that make it impossible.")] string compareBy = null,
            [Description("Optional, 1 by default: the number of combinations to return, from 1 to 5, for each compared variant.")] int count = 1,
            [Description("Optional: a short title for the view of the trade-off, for example 'Camera and X-band transmitter'.")] string title = null,
            McpServer server = null)
        {
            var study = this.CreateStudy(new TradeOffRequest(rootId, choices, objectives, constraints, rules, count, compareBy, null, title));
            var resultText = JsonSerializer.Serialize(new TradeOffResult(study.Status, study.Current, study.Best, study.Conflicts), McpJsonUtilities.DefaultOptions);
            var toolResult = new CallToolResult { Content = [new TextContentBlock { Text = resultText }] };

            if (this.appHostDetector.ShowsApps(server?.ClientCapabilities))
            {
                toolResult.StructuredContent = JsonSerializer.SerializeToElement(study, StudySerializerOptions);
            }

            return toolResult;
        }

        /// <summary>
        /// Runs a trade-off again with the settings that the user changed in its view, without the AI assistant and without
        /// modifying the model. Only the view of a trade-off calls this tool: the host hides it from the AI assistant.
        /// </summary>
        /// <param name="rootId">The <c>Id</c> of the element under which the totals are computed.</param>
        /// <param name="choices">The <see cref="TradeOffChoice"/>s to make.</param>
        /// <param name="objectives">The <see cref="TradeOffObjective"/>s, by order of priority, or <c>null</c> for none.</param>
        /// <param name="constraints">The <see cref="TradeOffConstraint"/>s, or <c>null</c> for none.</param>
        /// <param name="rules">The <see cref="TradeOffRule"/>s, or <c>null</c> for none.</param>
        /// <param name="compareBy">The name of the choice whose variants are compared, or <c>null</c>.</param>
        /// <param name="count">The number of combinations to return, from the best, for each alternative.</param>
        /// <param name="excluded">The variants turned off by the user, written <c>choice.variant</c>, or <c>null</c> for none.</param>
        /// <param name="title">The short title of the trade-off, or <c>null</c>.</param>
        /// <returns>The <see cref="TradeOffStudy"/> with these settings.</returns>
        /// <exception cref="McpException">
        /// Thrown when the settings are invalid, with a message that the view shows to the user.
        /// </exception>
        [McpServerTool(Name = "explore_trade_off", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
        [McpAppUi(ResourceUri = TradeOffResource.Uri, Visibility = [McpUiToolVisibility.App])]
        [Description("Runs solve_trade_off again with the settings that the user changed in the view of a trade-off. Called by the view only.")]
        public TradeOffStudy ExploreTradeOff(
            [Description("The identifier (Id, a GUID) of the element under which the totals are computed.")] Guid rootId,
            [Description("The choices to make, as in solve_trade_off.")] IReadOnlyList<TradeOffChoice> choices,
            [Description("Optional: the objectives, by order of priority.")] IReadOnlyList<TradeOffObjective> objectives = null,
            [Description("Optional: the constraints that are turned on.")] IReadOnlyList<TradeOffConstraint> constraints = null,
            [Description("Optional: the rules.")] IReadOnlyList<TradeOffRule> rules = null,
            [Description("Optional: the name of the compared choice.")] string compareBy = null,
            [Description("Optional, 1 by default: the number of combinations to return, from 1 to 5, for each compared variant.")] int count = 1,
            [Description("Optional: the variants turned off, written choice.variant, for example ['camera.A', 'camera.current'].")] IReadOnlyList<string> excluded = null,
            [Description("Optional: the short title of the trade-off.")] string title = null)
        {
            return this.CreateStudy(new TradeOffRequest(rootId, choices, objectives, constraints, rules, count, compareBy, excluded, title));
        }

        /// <summary>
        /// Checks a request against the model, solves it and describes the result.
        /// </summary>
        /// <param name="request">The <see cref="TradeOffRequest"/> to solve.</param>
        /// <returns>The <see cref="TradeOffStudy"/> of the request.</returns>
        /// <exception cref="McpException">
        /// Thrown when the request is invalid.
        /// </exception>
        private TradeOffStudy CreateStudy(TradeOffRequest request)
        {
            var problem = TradeOffProblemBuilder.Build(this.modelProvider, request);
            var solution = this.solver.Solve(problem);
            var root = this.modelProvider.GetRequiredElementById(request.RootId);

            return TradeOffStudyBuilder.Build(request, new TradeOffRoot(root.Id, root.qualifiedName), problem, solution);
        }
    }
}
