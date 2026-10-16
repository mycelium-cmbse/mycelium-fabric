// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffRule.cs" company="Starion Group S.A.">
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
    /// A dependency between the variants of two or more <see cref="TradeOffChoice"/>s, for example "a thermal engine requires a
    /// fuel tank". Variants are written <c>choice.variant</c>.
    /// </summary>
    public sealed record TradeOffRule
    {
        /// <summary>
        /// Gets the variant that the rule starts from.
        /// </summary>
        [Description("The variant that the rule starts from, written choice.variant, for example 'propulsion.thermal'.")]
        public string If { get; init; }

        /// <summary>
        /// Gets how the variant is linked to the other variants.
        /// </summary>
        [Description("Optional, Requires by default: Requires (when 'if' is taken, at least one variant of 'then' is taken) or Excludes (never taken together with a variant of 'then').")]
        public TradeOffRuleKind Kind { get; init; }

        /// <summary>
        /// Gets the variants of other choices that the rule links to.
        /// </summary>
        [Description("The variants of other choices, written choice.variant, for example ['fuelTank.small', 'fuelTank.large'].")]
        public IReadOnlyList<string> Then { get; init; }
    }
}
