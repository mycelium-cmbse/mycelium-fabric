// ------------------------------------------------------------------------------------------------
//  <copyright file="AttributeTotal.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// The total of an attribute under the root for a <see cref="TradeOffCombination"/>.
    /// </summary>
    /// <param name="Attribute">The name of the attribute.</param>
    /// <param name="Total">The total, rounded to 3 decimals.</param>
    /// <param name="Difference">The difference with the total of the current state, rounded to 3 decimals.</param>
    /// <param name="Unit">The unit of the total and of the difference, or <c>null</c> when the attribute has none.</param>
    public sealed record AttributeTotal(string Attribute, double Total, double Difference, Unit Unit = null);
}
