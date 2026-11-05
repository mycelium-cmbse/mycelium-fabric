// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffPoint.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// One combination of a <see cref="TradeOffStudy"/>, as the view draws it on its chart.
    /// </summary>
    /// <param name="Choices">The <see cref="TradeOffPointChoice"/> of each choice, in the order of the request.</param>
    /// <param name="Totals">The total of each attribute, by attribute name, in the unit of the attribute.</param>
    /// <param name="Feasible">Whether the combination respects every constraint and rule.</param>
    /// <param name="Rank">
    /// The rank of a feasible combination, from 1, ordered like the solver orders them: by the objectives, then by the number
    /// of changes; <c>null</c> when the combination is not feasible.
    /// </param>
    /// <param name="IsCurrent">Whether the combination is the current state.</param>
    /// <param name="Broken">Why the combination is not feasible: each constraint or rule it breaks, with the values.</param>
    public sealed record TradeOffPoint(IReadOnlyList<TradeOffPointChoice> Choices, IReadOnlyDictionary<string, double> Totals, bool Feasible, int? Rank, bool IsCurrent,
        IReadOnlyList<string> Broken);
}
