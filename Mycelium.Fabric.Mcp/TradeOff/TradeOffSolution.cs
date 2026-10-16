// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffSolution.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Collections.Generic;

    /// <summary>
    /// The solution of a <see cref="TradeOffProblem"/> found by an <see cref="ITradeOffSolver"/>.
    /// </summary>
    /// <param name="Status">
    /// The overall <see cref="TradeOffStatus"/>: <see cref="TradeOffStatus.Infeasible"/> or <see cref="TradeOffStatus.Unknown"/>
    /// when no alternative has a combination, otherwise <see cref="TradeOffStatus.Feasible"/> when one search was stopped by the
    /// time limit, and <see cref="TradeOffStatus.Optimal"/> when every combination is proven to be the best.
    /// </param>
    /// <param name="Alternatives">
    /// One <see cref="TradeOffAlternative"/> for each variant of the compared decision, or a single one when no decision is
    /// compared.
    /// </param>
    public sealed record TradeOffSolution(TradeOffStatus Status, IReadOnlyList<TradeOffAlternative> Alternatives);
}
