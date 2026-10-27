// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintCheck.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// Whether a <see cref="TradeOffCombination"/> respects one constraint of the request.
    /// </summary>
    /// <param name="Constraint">The text of the constraint, with its margin, for example <c>mass * 1.2 &lt;= 150</c>.</param>
    /// <param name="Satisfied">Whether the combination respects the constraint.</param>
    /// <param name="Name">The short name of the constraint, for example <c>REQ-SYS-001</c>, or <c>null</c>.</param>
    /// <param name="Value">
    /// The compared value: the total times the margin factor for an upper bound (<c>150.96</c> for <c>mass * 1.2</c>), the
    /// total for a lower bound.
    /// </param>
    /// <param name="Limit">The limit that <paramref name="Value"/> is compared with, times the margin factor for a lower bound.</param>
    public sealed record ConstraintCheck(string Constraint, bool Satisfied, string Name = null, double Value = 0, double Limit = 0);
}
