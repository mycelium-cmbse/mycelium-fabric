// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffRuleKind.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// How a <see cref="TradeOffRule"/> links a variant to other variants. It is written as text in JSON (for example
    /// <c>"Requires"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TradeOffRuleKind>))]
    public enum TradeOffRuleKind
    {
        /// <summary>
        /// When the variant is taken, one of the other variants must be taken too.
        /// </summary>
        Requires,

        /// <summary>
        /// The variant is never taken together with any of the other variants.
        /// </summary>
        Excludes
    }
}
