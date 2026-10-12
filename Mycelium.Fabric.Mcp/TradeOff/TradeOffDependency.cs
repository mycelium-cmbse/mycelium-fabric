// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffDependency.cs" company="Starion Group S.A.">
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
    /// A checked <see cref="TradeOffRule"/> of a <see cref="TradeOffProblem"/>, whose variants are resolved.
    /// </summary>
    /// <param name="Kind">The <see cref="TradeOffRuleKind"/> of the rule.</param>
    /// <param name="If">The variant that the rule starts from.</param>
    /// <param name="Then">The variants of other decisions that the rule links to.</param>
    /// <param name="Description">The text of the rule, for example <c>propulsion.thermal requires fuelTank.small or fuelTank.large</c>.</param>
    public sealed record TradeOffDependency(TradeOffRuleKind Kind, TradeOffVariantReference If, IReadOnlyList<TradeOffVariantReference> Then, string Description);
}
