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
    public sealed record ConstraintCheck(string Constraint, bool Satisfied);
}
