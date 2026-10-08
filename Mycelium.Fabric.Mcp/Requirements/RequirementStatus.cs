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
        /// The constraint of the requirement holds for the part that satisfies it.
        /// </summary>
        Satisfied,

        /// <summary>
        /// The constraint of the requirement does not hold for the part that satisfies it.
        /// </summary>
        NotSatisfied,

        /// <summary>
        /// The requirement cannot be checked: it has no constraint, or no part satisfies it.
        /// </summary>
        NotVerifiable,

        /// <summary>
        /// The requirement has a constraint and a satisfying part, but the server cannot evaluate it: the constraint has
        /// another form than <c>attribute operator limit</c>, or the part has no value for the attribute.
        /// </summary>
        NotEvaluated
    }
}
