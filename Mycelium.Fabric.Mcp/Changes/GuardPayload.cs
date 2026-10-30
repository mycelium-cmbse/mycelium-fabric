// ------------------------------------------------------------------------------------------------
//  <copyright file="GuardPayload.cs" company="Starion Group S.A.">
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
    /// The guard of a transition in an <see cref="ElementPayload"/>: an attribute compared with a limit or with another
    /// attribute, as in the constraint of a requirement. The server builds from it, for example,
    /// <c>transition first safe if batteryCharge &gt;= 80 then nominal;</c>.
    /// </summary>
    public sealed record GuardPayload
    {
        /// <summary>
        /// Gets the name of the attribute that the guard compares, or a path to an attribute of a feature.
        /// </summary>
        [Description("The compared attribute, found from the owner of the transition up through its owners, for example 'batteryCharge' (an attribute of the part whose states these are), or a path such as 'eps.battery.charge'.")]
        public string Attribute { get; init; }

        /// <summary>
        /// Gets the comparison operator of the guard.
        /// </summary>
        [Description("How the attribute compares with the limit: '<', '<=', '>', '>=', '==' or '!='.")]
        public string Operator { get; init; }

        /// <summary>
        /// Gets the limit of the guard.
        /// </summary>
        [Description("The limit: a number (80), a Boolean (true), or a text or enumeration value name ('eclipse'), the last two with '==' or '!=' only. Give either limit or limitAttribute.")]
        public PayloadValue? Limit { get; init; }

        /// <summary>
        /// Gets the attribute that the compared attribute is compared with, instead of a limit.
        /// </summary>
        [Description("Instead of a limit: another attribute, or a path, to compare with, found like attribute.")]
        public string LimitAttribute { get; init; }

        /// <summary>
        /// Gets the unit of the numeric limit.
        /// </summary>
        [Description("Optional, with a numeric limit: its unit, as a symbol (%, V, °C) or a name.")]
        public string Unit { get; init; }
    }
}
