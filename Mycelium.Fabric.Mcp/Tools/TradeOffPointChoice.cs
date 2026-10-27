// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffPointChoice.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// The variant that a <see cref="TradeOffPoint"/> takes for one choice.
    /// </summary>
    /// <param name="Choice">The name of the choice.</param>
    /// <param name="Variant">The name of the variant.</param>
    /// <param name="Quantity">The number of units, 0 when the part is removed or the equipment not added.</param>
    public sealed record TradeOffPointChoice(string Choice, string Variant, int Quantity);
}
