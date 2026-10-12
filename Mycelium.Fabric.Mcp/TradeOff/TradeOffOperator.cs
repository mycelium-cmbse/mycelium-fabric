// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffOperator.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    /// <summary>
    /// The comparison operator of a <see cref="TradeOffLimit"/>, read from the text of a <see cref="TradeOffConstraint"/>.
    /// </summary>
    public enum TradeOffOperator
    {
        /// <summary>
        /// The total stays strictly under the limit (<c>&lt;</c>).
        /// </summary>
        LessThan,

        /// <summary>
        /// The total stays under the limit or equal to it (<c>&lt;=</c>).
        /// </summary>
        LessThanOrEqual,

        /// <summary>
        /// The total stays strictly over the limit (<c>&gt;</c>).
        /// </summary>
        GreaterThan,

        /// <summary>
        /// The total stays over the limit or equal to it (<c>&gt;=</c>).
        /// </summary>
        GreaterThanOrEqual
    }
}
