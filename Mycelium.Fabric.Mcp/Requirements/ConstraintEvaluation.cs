// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstraintEvaluation.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    /// <summary>
    /// The result of the evaluation of an <see cref="AttributeConstraint"/> for a value of its attribute.
    /// </summary>
    /// <param name="IsSatisfied">Whether the constraint holds.</param>
    /// <param name="Gap">
    /// How far the value is from the limit, margin included: positive or zero when the constraint holds, negative by the
    /// excess when it does not. Rounded to 3 decimals.
    /// </param>
    /// <param name="Comparison">
    /// The comparison that holds between the compared value and limit, margin included, for example <c>150.96 &gt; 150</c>.
    /// </param>
    public sealed record ConstraintEvaluation(bool IsSatisfied, double Gap, string Comparison);
}
