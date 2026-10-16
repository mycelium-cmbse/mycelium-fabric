// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffOption.cs" company="Starion Group S.A.">
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
    /// One option of a <see cref="TradeOffDecision"/>: a variant with a given number of units. A variant that may be taken from 2
    /// to 6 times gives 5 options.
    /// </summary>
    /// <param name="Variant">The name of the variant.</param>
    /// <param name="Quantity">The number of units, 0 for the variant <c>none</c>.</param>
    /// <param name="IsCurrent">
    /// Whether the option is the current state: the current variant of an existing part, or <c>none</c> for equipment to add.
    /// </param>
    /// <param name="Values">The value that the option adds to the total of each attribute of the problem, by attribute name.</param>
    public sealed record TradeOffOption(string Variant, int Quantity, bool IsCurrent, IReadOnlyDictionary<string, double> Values);
}
