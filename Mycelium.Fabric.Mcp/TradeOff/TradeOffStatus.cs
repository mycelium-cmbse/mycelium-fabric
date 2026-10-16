// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffStatus.cs" company="Starion Group S.A.">
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
    /// The outcome of an <see cref="ITradeOffSolver"/>. It is written as text in JSON (for example <c>"Optimal"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TradeOffStatus>))]
    public enum TradeOffStatus
    {
        /// <summary>
        /// The combinations are proven to be the best ones.
        /// </summary>
        Optimal,

        /// <summary>
        /// The combinations respect every constraint and rule, but the time limit stopped the search before proving that they
        /// are the best ones.
        /// </summary>
        Feasible,

        /// <summary>
        /// No combination respects every constraint and rule together.
        /// </summary>
        Infeasible,

        /// <summary>
        /// The time limit stopped the search before any combination was found.
        /// </summary>
        Unknown
    }
}
