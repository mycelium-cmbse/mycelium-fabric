// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffVariant.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// One candidate of a <see cref="TradeOffChoice"/> sent to the <c>solve_trade_off</c> tool, for example another camera:
    /// its name, the values of its attributes for one unit, and how many units may be taken.
    /// </summary>
    public sealed record TradeOffVariant
    {
        /// <summary>
        /// The name of the variant that keeps an existing part as it is in the model.
        /// </summary>
        public const string CurrentName = "current";

        /// <summary>
        /// The name of the variant that means that the part is removed, or that the equipment to add is not added.
        /// </summary>
        public const string NoneName = "none";

        /// <summary>
        /// Gets the name of the variant, unique in its choice.
        /// </summary>
        [Description("The name of the variant, unique in its choice, for example 'electric' or 'SA-120'. Two names are reserved: "
            + "'current' (the values of the part in the model, always added by the server; give it only to complete values that the model lacks) "
            + "and 'none' (the part is removed or the equipment is not added; all its values are 0, so give no values).")]
        public string Name { get; init; }

        /// <summary>
        /// Gets the values of the attributes of one unit of the variant, by attribute name.
        /// </summary>
        [Description("The values of the attributes for one unit, by attribute name, for example { \"mass\": 30, \"power\": 60 }. "
            + "For an existing part, an attribute that is not given keeps the current value of the part; for equipment to add, it is 0.")]
        public IReadOnlyDictionary<string, double> Values { get; init; }

        /// <summary>
        /// Gets the smallest number of units of the variant that may be taken.
        /// </summary>
        [Description("Optional, 1 by default: the smallest number of units, for example 2 solar panels. The values are multiplied by the number chosen.")]
        public int MinQuantity { get; init; } = 1;

        /// <summary>
        /// Gets the largest number of units of the variant that may be taken.
        /// </summary>
        [Description("Optional, 1 by default: the largest number of units, at most 20, for example 6 solar panels. The solver chooses the number between minQuantity and maxQuantity.")]
        public int MaxQuantity { get; init; } = 1;
    }
}
