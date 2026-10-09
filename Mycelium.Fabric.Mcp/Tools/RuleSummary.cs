// ------------------------------------------------------------------------------------------------
//  <copyright file="RuleSummary.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using Mycelium.Fabric.Mcp.Validation;

    /// <summary>
    /// A rule checked by the <c>validate_model</c> tool, with the number of issues it has found.
    /// </summary>
    /// <param name="Name">The name of the rule, for example <c>broken-reference</c>.</param>
    /// <param name="Severity">The <see cref="ValidationSeverity"/> of the issues found by the rule.</param>
    /// <param name="Description">What the rule checks.</param>
    /// <param name="IssueCount">The number of issues that the rule has found in the checked elements.</param>
    public sealed record RuleSummary(string Name, ValidationSeverity Severity, string Description, int IssueCount);
}
