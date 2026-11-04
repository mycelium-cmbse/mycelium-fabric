// ------------------------------------------------------------------------------------------------
//  <copyright file="BatchPreview.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Validation;

    /// <summary>
    /// What a batch that waits for the decision of the user would do to the model, shown to the user by the view of the batch.
    /// </summary>
    /// <param name="BatchId">The identifier of the waiting batch, sent back with the decision.</param>
    /// <param name="Token">
    /// The secret token of the waiting batch, sent back with the decision. It travels in the structured content because some
    /// hosts, as VS Code, do not pass <c>_meta</c> to the view; the host hides decide_batch from the AI assistant.
    /// </param>
    /// <param name="CreatedElements">
    /// The elements that the batch would create; their identifiers are provisional, since the batch is built again when the user
    /// applies it.
    /// </param>
    /// <param name="ModifiedElements">The existing elements that the batch would update.</param>
    /// <param name="DeletedElements">The existing elements that the batch would delete.</param>
    /// <param name="BudgetImpacts">The totals of mass and power of the top-level parts that the batch would change.</param>
    /// <param name="RequirementChanges">The requirements whose status or constrained value the batch would change.</param>
    /// <param name="NewIssues">The errors and warnings that the batch would introduce, at most 20, the most serious first.</param>
    /// <param name="NewIssueCount">The number of errors and warnings that the batch would introduce.</param>
    /// <param name="ResolvedIssueCount">The number of errors and warnings of the model that the batch would resolve.</param>
    public sealed record BatchPreview(Guid BatchId, string Token, IReadOnlyList<PreviewElement> CreatedElements, IReadOnlyList<PreviewElement> ModifiedElements,
        IReadOnlyList<PreviewElement> DeletedElements, IReadOnlyList<BudgetImpact> BudgetImpacts, IReadOnlyList<RequirementChange> RequirementChanges,
        IReadOnlyList<ValidationIssue> NewIssues, int NewIssueCount, int ResolvedIssueCount);
}
