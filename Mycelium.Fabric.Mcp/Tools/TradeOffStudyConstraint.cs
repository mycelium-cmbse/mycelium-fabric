// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffStudyConstraint.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// A constraint of a <see cref="TradeOffStudy"/>, as the server understood it.
    /// </summary>
    /// <param name="Name">The short name of the constraint, for example <c>REQ-SYS-001</c>, or <c>null</c>.</param>
    /// <param name="Description">The text of the constraint, for example <c>mass * 1.2 &lt;= 150 kg</c>.</param>
    /// <param name="Attribute">The name of the constrained attribute.</param>
    /// <param name="Operator">The operator: <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> or <c>&gt;=</c>.</param>
    /// <param name="Limit">The limit, in the unit of the attribute, or <c>null</c> when it is the total of another attribute.</param>
    /// <param name="LimitAttribute">The attribute whose total is the limit, or <c>null</c> for a numeric limit.</param>
    /// <param name="Margin">The margin in percent, 0 for none.</param>
    /// <param name="Threshold">
    /// The total at which a numeric limit is reached, margin included (<c>125</c> for <c>mass * 1.2 &lt;= 150</c>), or
    /// <c>null</c> when the limit is the total of another attribute.
    /// </param>
    public sealed record TradeOffStudyConstraint(string Name, string Description, string Attribute, string Operator, double? Limit, string LimitAttribute, double Margin, double? Threshold);
}
