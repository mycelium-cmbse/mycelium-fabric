// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffStudyRequirement.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;

    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// A requirement of the model in a trade-off: one that the choices can change, one that does not hold today, or one that
    /// the user turned off.
    /// </summary>
    /// <param name="Id">The <c>Id</c> of the requirement.</param>
    /// <param name="Name">The name shown for the requirement, for example <c>REQ-SYS-001</c>: what turns it off.</param>
    /// <param name="Text">The text of the requirement, or <c>null</c>.</param>
    /// <param name="Constraint">The constraint that is checked, for example <c>subj.mass * 1.2 &lt;= 150 [kg]</c>.</param>
    /// <param name="SatisfiedBy">The qualified name of the part that satisfies the requirement.</param>
    /// <param name="Status">The <see cref="RequirementStatus"/> in the current state.</param>
    /// <param name="Explanation">The values compared in the current state, or why the server cannot tell.</param>
    /// <param name="DependsOnChoices">
    /// Whether a choice of the trade-off can change the check. When it is <c>false</c> and the requirement does not hold, no
    /// combination repairs it.
    /// </param>
    /// <param name="Ignored">Whether the user turned the requirement off: the combinations are not checked against it.</param>
    /// <param name="Searched">
    /// Whether the solver searches with the requirement, which compares sums. Otherwise the requirement is checked on each
    /// combination that the solver proposes, and a combination that breaks it is ranked after the others.
    /// </param>
    /// <param name="Attribute">The total that the solver compares with a number for this requirement, or <c>null</c>.</param>
    /// <param name="Operator">The operator of this comparison, or <c>null</c>.</param>
    /// <param name="Threshold">The number that the total is compared with, in the unit of the total, or <c>null</c>.</param>
    public sealed record TradeOffStudyRequirement(Guid Id, string Name, string Text, string Constraint, string SatisfiedBy, RequirementStatus Status, string Explanation,
        bool DependsOnChoices, bool Ignored, bool Searched, string Attribute = null, string Operator = null, double? Threshold = null);
}
