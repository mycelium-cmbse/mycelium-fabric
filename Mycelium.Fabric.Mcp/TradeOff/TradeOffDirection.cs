// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffDirection.cs" company="Starion Group S.A.">
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
    /// Whether a <see cref="TradeOffObjective"/> looks for the smallest or the largest total. It is written as text in JSON (for
    /// example <c>"Minimize"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TradeOffDirection>))]
    public enum TradeOffDirection
    {
        /// <summary>
        /// The smallest total is the best, for example the mass.
        /// </summary>
        Minimize,

        /// <summary>
        /// The largest total is the best, for example the generated power.
        /// </summary>
        Maximize
    }
}
