// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffAlternative.cs" company="Starion Group S.A.">
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
    /// The best combinations of a <see cref="TradeOffProblem"/> for one variant of the compared decision, or for the whole
    /// problem when no decision is compared.
    /// </summary>
    /// <param name="Variant">
    /// The variant of the compared decision that every combination takes, or <c>null</c> when no decision is compared.
    /// </param>
    /// <param name="Status">The <see cref="TradeOffStatus"/> of the search.</param>
    /// <param name="Selections">
    /// The combinations found, from the best: for each one, the index of the selected option of each decision.
    /// </param>
    /// <param name="Conflicts">
    /// When the search is <see cref="TradeOffStatus.Infeasible"/>, the descriptions of a smallest set of limits and dependencies
    /// that cannot be respected together; otherwise empty.
    /// </param>
    public sealed record TradeOffAlternative(string Variant, TradeOffStatus Status, IReadOnlyList<IReadOnlyList<int>> Selections, IReadOnlyList<string> Conflicts);
}
