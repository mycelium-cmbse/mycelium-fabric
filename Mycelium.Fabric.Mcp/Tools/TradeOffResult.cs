// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffResult.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.TradeOff;

    /// <summary>
    /// The result of the <c>solve_trade_off</c> tool: the current state and the best combinations, or why none exists.
    /// </summary>
    /// <param name="Status">The <see cref="TradeOffStatus"/> of the search.</param>
    /// <param name="Current">The current state, as a <see cref="TradeOffCombination"/> of rank 0.</param>
    /// <param name="Combinations">
    /// The best <see cref="TradeOffCombination"/>s, from the best (for each alternative when a choice is compared), empty when
    /// none exists.
    /// </param>
    /// <param name="Conflicts">
    /// For the problem, or for each compared alternative, that has no combination: the constraints and rules that cannot be
    /// respected together, each written after its alternative when a choice is compared.
    /// </param>
    public sealed record TradeOffResult(TradeOffStatus Status, TradeOffCombination Current, IReadOnlyList<TradeOffCombination> Combinations, IReadOnlyList<string> Conflicts)
    {
        /// <summary>
        /// Gets the requirements of the model that the server checks on every combination, as they are in the current state:
        /// those that the choices can change, those that do not hold today, and those that the user turned off.
        /// </summary>
        public IReadOnlyList<TradeOffStudyRequirement> Requirements { get; init; } = [];
    }
}
