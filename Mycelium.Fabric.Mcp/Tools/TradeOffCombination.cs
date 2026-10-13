// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffCombination.cs" company="Starion Group S.A.">
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
    /// One combination returned by the <c>solve_trade_off</c> tool, or the current state.
    /// </summary>
    /// <param name="Alternative">
    /// The compared variant that the combination takes, written <c>choice.variant</c>, or <c>null</c> when no choice is compared
    /// and for the current state.
    /// </param>
    /// <param name="Rank">
    /// The rank of the combination, from 1 for the best (among those of its alternative when a choice is compared), or 0 for the
    /// current state.
    /// </param>
    /// <param name="ChangeCount">The number of choices whose variant differs from the current state.</param>
    /// <param name="Choices">The <see cref="ChosenVariant"/> of each choice, in the order of the request.</param>
    /// <param name="Totals">The <see cref="AttributeTotal"/> of each attribute of the objectives and constraints.</param>
    /// <param name="Constraints">The <see cref="ConstraintCheck"/> of each constraint, in the order of the request.</param>
    public sealed record TradeOffCombination(string Alternative, int Rank, int ChangeCount, IReadOnlyList<ChosenVariant> Choices, IReadOnlyList<AttributeTotal> Totals,
        IReadOnlyList<ConstraintCheck> Constraints);
}
