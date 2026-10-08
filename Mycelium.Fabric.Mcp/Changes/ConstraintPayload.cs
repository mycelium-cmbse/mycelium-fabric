// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintPayload.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.ComponentModel;

    /// <summary>
    /// The constraint of a requirement in an <see cref="ElementPayload"/>: an attribute of the subject of the requirement
    /// compared with a limit, with an optional margin. The server builds from it
    /// <c>subject subj { attribute mass; } require constraint { subj.mass * 1.2 &lt;= 150 }</c>.
    /// </summary>
    public sealed record ConstraintPayload
    {
        /// <summary>
        /// Gets the name of the attribute of the subject that the constraint compares with the limit.
        /// </summary>
        [Description("The name of the constrained attribute of the subject, for example 'mass' or 'power'. check_requirements computes its value on the satisfying part as sum_attribute does.")]
        public string Attribute { get; init; }

        /// <summary>
        /// Gets the comparison operator of the constraint.
        /// </summary>
        [Description("How the attribute compares with the limit: '<', '<=', '>', '>=' or '=='.")]
        public string Operator { get; init; }

        /// <summary>
        /// Gets the limit of the constraint.
        /// </summary>
        [Description("The limit, in the unit of the attribute, for example 150 for 150 kg.")]
        public double? Limit { get; init; }

        /// <summary>
        /// Gets the margin of the constraint, in percent.
        /// </summary>
        [Description("Optional: a margin in percent, for example 20, that always makes the constraint harder to meet. With '<' or '<=', the value plus the margin must stay under the limit (subj.mass * 1.2 <= 150); with '>' or '>=', the value must reach the limit plus the margin (subj.capacity >= 300 * 1.2). Not allowed with '=='.")]
        public double? Margin { get; init; }
    }
}
