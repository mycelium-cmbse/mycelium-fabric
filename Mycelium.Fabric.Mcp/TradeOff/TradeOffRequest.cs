// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffRequest.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// The parameters of the <c>solve_trade_off</c> tool, as the AI assistant sends them, before the
    /// <see cref="TradeOffProblemBuilder"/> checks them against the model.
    /// </summary>
    /// <param name="RootId">The <c>Id</c> of the element under which the totals are computed, for example the satellite.</param>
    /// <param name="Choices">The <see cref="TradeOffChoice"/>s to make.</param>
    /// <param name="Objectives">The <see cref="TradeOffObjective"/>s, by order of priority, or <c>null</c> for none.</param>
    /// <param name="Constraints">The <see cref="TradeOffConstraint"/>s, or <c>null</c> for none.</param>
    /// <param name="Rules">The <see cref="TradeOffRule"/>s, or <c>null</c> for none.</param>
    /// <param name="Count">The number of combinations to return, from the best, for each alternative.</param>
    /// <param name="CompareBy">
    /// The name of the choice whose variants are compared, each with its own best combinations, or <c>null</c>.
    /// </param>
    public sealed record TradeOffRequest(
        Guid RootId,
        IReadOnlyList<TradeOffChoice> Choices,
        IReadOnlyList<TradeOffObjective> Objectives,
        IReadOnlyList<TradeOffConstraint> Constraints,
        IReadOnlyList<TradeOffRule> Rules,
        int Count,
        string CompareBy);
}
