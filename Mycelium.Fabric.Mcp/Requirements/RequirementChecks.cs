// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementChecks.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    using System.Collections.Generic;

    /// <summary>
    /// The result of the <c>check_requirements</c> tool: the number of checks of each status, and one page of the checks.
    /// </summary>
    /// <param name="SatisfiedCount">The number of checks of the whole model whose status is <see cref="RequirementStatus.Satisfied"/>.</param>
    /// <param name="NotSatisfiedCount">The number of checks of the whole model whose status is <see cref="RequirementStatus.NotSatisfied"/>.</param>
    /// <param name="NotVerifiableCount">The number of checks of the whole model whose status is <see cref="RequirementStatus.NotVerifiable"/>.</param>
    /// <param name="NotEvaluatedCount">The number of checks of the whole model whose status is <see cref="RequirementStatus.NotEvaluated"/>.</param>
    /// <param name="TotalFound">The number of checks that match the requested status, which can be greater than the number of returned checks.</param>
    /// <param name="Requirements">The checks of the requested page, sorted by requirement identifier.</param>
    /// <param name="NextOffset">The offset that gives the next page, or <c>null</c> when this page is the last one.</param>
    public sealed record RequirementChecks(int SatisfiedCount, int NotSatisfiedCount, int NotVerifiableCount, int NotEvaluatedCount, int TotalFound,
        IReadOnlyList<RequirementCheck> Requirements, int? NextOffset);
}
