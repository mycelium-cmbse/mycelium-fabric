// ------------------------------------------------------------------------------------------------
//  <copyright file="ValidationReport.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Validation;

    /// <summary>
    /// The result of the <c>validate_model</c> tool: the rules checked and the issues found in the checked elements.
    /// </summary>
    /// <param name="ElementCount">The number of checked elements: the whole model, or an element and what it owns.</param>
    /// <param name="ErrorCount">The number of errors in the checked elements.</param>
    /// <param name="WarningCount">The number of warnings in the checked elements.</param>
    /// <param name="InformationCount">The number of information issues in the checked elements.</param>
    /// <param name="Rules">Each rule checked, with its number of issues, the most serious first.</param>
    /// <param name="TotalFound">The number of issues that match the requested severity and rule.</param>
    /// <param name="Issues">The matching issues of the requested page, the most serious first.</param>
    /// <param name="NextOffset">The offset that gives the next page, or <c>null</c> when this page is the last one.</param>
    public sealed record ValidationReport(int ElementCount, int ErrorCount, int WarningCount, int InformationCount, IReadOnlyList<RuleSummary> Rules,
        int TotalFound, IReadOnlyList<ValidationIssue> Issues, int? NextOffset);
}
