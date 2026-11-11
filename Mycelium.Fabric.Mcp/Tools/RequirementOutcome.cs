// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementOutcome.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// What a combination of a trade-off does to a requirement of the model: the check of <c>check_requirements</c> on the
    /// model with this combination, in short.
    /// </summary>
    /// <param name="Name">The name of the requirement, for example <c>REQ-SYS-001</c>.</param>
    /// <param name="SatisfiedBy">The qualified name of the part that satisfies the requirement.</param>
    /// <param name="Status">
    /// The <see cref="RequirementStatus"/> on the combination: <c>Satisfied</c>, <c>NotSatisfied</c>, or <c>NotEvaluated</c>
    /// when the server cannot tell, which never counts as met.
    /// </param>
    /// <param name="Explanation">The values compared, for example <c>152.4 [kg] &gt; 150 [kg]</c>, or why the server cannot tell.</param>
    /// <param name="Changed">Whether the status differs from the one of the current state.</param>
    public sealed record RequirementOutcome(string Name, string SatisfiedBy, RequirementStatus Status, string Explanation, bool Changed);
}
