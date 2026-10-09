// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementStatus.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The outcome of the check of a requirement. It is written as text in JSON (for example <c>"NotSatisfied"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RequirementStatus>))]
    public enum RequirementStatus
    {
        /// <summary>
        /// The requirement holds for the part that satisfies it: its required constraints hold, or one of its assumptions
        /// does not, as in the <c>RequirementCheck</c> of the SysML v2 library.
        /// </summary>
        Satisfied,

        /// <summary>
        /// The requirement does not hold for the part that satisfies it: its assumptions hold, and one of its required
        /// constraints does not.
        /// </summary>
        NotSatisfied,

        /// <summary>
        /// The requirement cannot be checked: it has no required constraint, or no part satisfies it.
        /// </summary>
        NotVerifiable,

        /// <summary>
        /// The requirement has a required constraint and a satisfying part, but the server cannot tell whether it holds: a
        /// constraint is not a comparison that it can evaluate, or the part has no value for an attribute.
        /// </summary>
        NotEvaluated
    }
}
