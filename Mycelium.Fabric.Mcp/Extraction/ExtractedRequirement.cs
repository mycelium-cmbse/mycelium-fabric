// ------------------------------------------------------------------------------------------------
//  <copyright file="ExtractedRequirement.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A requirement proposed by the AI assistant, once checked by the server: one row of the view of the extraction.
    /// </summary>
    /// <param name="Requirement">The <see cref="ProposedRequirement"/>, as the AI assistant gave it.</param>
    /// <param name="SourceTitle">The title of the section or subsection where the statement is written, or <c>null</c> when it is not found.</param>
    /// <param name="Excerpt">The passage of the document that holds the statement, or <c>null</c> when it is not found.</param>
    /// <param name="Findings">The <see cref="ExtractionFinding"/>s, empty when the requirement is ready to import.</param>
    public sealed record ExtractedRequirement(ProposedRequirement Requirement, string SourceTitle, string Excerpt, IReadOnlyList<ExtractionFinding> Findings)
    {
        /// <summary>
        /// Gets a value indicating whether the requirement can be imported as written: it has no finding.
        /// </summary>
        public bool IsReady => this.Findings.Count == 0;

        /// <summary>
        /// Gets a value indicating whether the requirement cannot be imported at all: the model, or an earlier row, already has
        /// its identifier.
        /// </summary>
        public bool IsBlocked => this.Findings.Any(finding => finding.Kind is ExtractionFindingKind.AlreadyModelled or ExtractionFindingKind.Repeated);
    }
}
