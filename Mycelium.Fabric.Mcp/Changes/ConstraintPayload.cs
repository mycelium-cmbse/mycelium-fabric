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
    using System.Text.Json.Serialization;

    using SysML2.NET.Core.Systems.Requirements;

    /// <summary>
    /// A constraint of a requirement in an <see cref="ElementPayload"/>: an attribute of the subject of the requirement
    /// compared with a limit or with another attribute, with an optional margin. The server builds from it, for example,
    /// <c>subject subj { attribute mass; } require constraint { subj.mass * 1.2 &lt;= 150 [kg] }</c>, or
    /// <c>assume constraint { subj.fuelLevel &gt;= subj.fuelTankCapacity }</c> for an assumption.
    /// </summary>
    public sealed record ConstraintPayload
    {
        /// <summary>
        /// Gets the kind of the constraint: a required constraint or an assumption.
        /// </summary>
        [Description("Optional: Requirement (the default) for a required constraint (require constraint), or Assumption for an assumption (assume constraint): the requirement then only has to hold when its assumptions hold. It replaces the current constraints of the same kind.")]
        [JsonConverter(typeof(JsonStringEnumConverter<RequirementConstraintKind>))]
        public RequirementConstraintKind? Kind { get; init; }

        /// <summary>
        /// Gets the name of the attribute of the subject that the constraint compares, or a path to an attribute of a
        /// sub-part.
        /// </summary>
        [Description("The constrained attribute of the subject, for example 'mass', or a path to an attribute of a sub-part of the subject, for example 'camera.mass'. check_requirements computes its value on the satisfying part as sum_attribute does.")]
        public string Attribute { get; init; }

        /// <summary>
        /// Gets the comparison operator of the constraint.
        /// </summary>
        [Description("How the attribute compares with the limit: '<', '<=', '>', '>=', '==' or '!='.")]
        public string Operator { get; init; }

        /// <summary>
        /// Gets the limit of the constraint.
        /// </summary>
        [Description("The limit: a number (150 for 150 kg, in the unit of the attribute unless unit is given), a Boolean (true), or a text or enumeration value name ('sunSynchronous'), the last two with '==' or '!=' only. Give either limit or limitAttribute.")]
        public PayloadValue? Limit { get; init; }

        /// <summary>
        /// Gets the attribute of the subject that the constrained attribute is compared with, instead of a limit.
        /// </summary>
        [Description("Instead of a limit: another attribute of the subject, or a path, to compare with, for example 'fuelTankCapacity' for subj.fuelLevel >= subj.fuelTankCapacity.")]
        public string LimitAttribute { get; init; }

        /// <summary>
        /// Gets the unit of the numeric limit.
        /// </summary>
        [Description("Optional, with a numeric limit: its unit, as a symbol (kg, W, km/h, arcsec) or a name (kilogram). The limit is then 150 [kg], and the value of the attribute is converted to compare them.")]
        public string Unit { get; init; }

        /// <summary>
        /// Gets the margin of the constraint, in percent.
        /// </summary>
        [Description("Optional: a margin in percent, for example 20, that always makes the constraint harder to meet. With '<' or '<=', the value plus the margin must stay under the limit (subj.mass * 1.2 <= 150); with '>' or '>=', the value must reach the limit plus the margin (subj.capacity >= 300 * 1.2). Not allowed with '==' or '!=', with a limit of 0 or less, or with a unit that has an offset such as °C: write the margin into the limit instead.")]
        public double? Margin { get; init; }
    }
}
