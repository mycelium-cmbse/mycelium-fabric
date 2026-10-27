// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffConstraint.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.ComponentModel;

    /// <summary>
    /// A budget that every combination returned by the <c>solve_trade_off</c> tool respects: the total of an attribute under the
    /// root compared with a limit, or with the total of another attribute, with an optional margin.
    /// </summary>
    public sealed record TradeOffConstraint
    {
        /// <summary>
        /// Gets the optional short name of the constraint, shown with it.
        /// </summary>
        [Description("Optional: a short name shown with the constraint, for example the requirement it comes from ('REQ-SYS-001').")]
        public string Name { get; init; }

        /// <summary>
        /// Gets the name of the attribute whose total is constrained.
        /// </summary>
        [Description("The attribute whose total under the root is constrained, for example 'mass' or 'power'.")]
        public string Attribute { get; init; }

        /// <summary>
        /// Gets the comparison operator of the constraint.
        /// </summary>
        [Description("How the total compares with the limit: '<', '<=', '>' or '>='.")]
        public string Operator { get; init; }

        /// <summary>
        /// Gets the limit of the constraint.
        /// </summary>
        [Description("The limit, in the unit of the attribute unless unit is given, for example 150 for 150 kg. Give either limit or limitAttribute.")]
        public double? Limit { get; init; }

        /// <summary>
        /// Gets the unit of the limit.
        /// </summary>
        [Description("Optional, with a numeric limit: its unit, as a symbol (kg, g, W) or a name (kilogram). The limit is then 150 [kg], "
            + "and the server converts it to the unit of the attribute under the root.")]
        public string Unit { get; init; }

        /// <summary>
        /// Gets the attribute whose total is the limit, instead of a number.
        /// </summary>
        [Description("Instead of a limit: another attribute whose total under the root is the limit, for example 'powerOutput' for power <= powerOutput "
            + "(the consumed power stays under the generated power).")]
        public string LimitAttribute { get; init; }

        /// <summary>
        /// Gets the margin of the constraint, in percent.
        /// </summary>
        [Description("Optional: a margin in percent, for example 20, that always makes the constraint harder to meet. With '<' or '<=', the total plus the margin "
            + "must stay under the limit (mass * 1.2 <= 150); with '>' or '>=', the total must reach the limit plus the margin (capacity >= 300 * 1.2). "
            + "Not allowed with a limit of 0 or less.")]
        public double? Margin { get; init; }
    }
}
