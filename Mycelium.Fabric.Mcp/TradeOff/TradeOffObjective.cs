// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffObjective.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.ComponentModel;

    /// <summary>
    /// What matters to the user in the <c>solve_trade_off</c> tool: the total of an attribute under the root, to make as small
    /// or as large as possible.
    /// </summary>
    public sealed record TradeOffObjective
    {
        /// <summary>
        /// Gets the name of the attribute whose total is optimized.
        /// </summary>
        [Description("The attribute whose total under the root is optimized, for example 'mass'.")]
        public string Attribute { get; init; }

        /// <summary>
        /// Gets whether the smallest or the largest total is the best.
        /// </summary>
        [Description("Optional, Minimize by default: Minimize or Maximize.")]
        public TradeOffDirection Direction { get; init; }
    }
}
