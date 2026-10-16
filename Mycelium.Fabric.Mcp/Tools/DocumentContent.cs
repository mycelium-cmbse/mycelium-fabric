// ------------------------------------------------------------------------------------------------
//  <copyright file="DocumentContent.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// The result of the <c>read_document</c> tool: one page of the outline of a document, or one page of the text of one of
    /// its sections.
    /// </summary>
    /// <param name="FileName">The name of the file of the document.</param>
    /// <param name="SectionCount">The number of sections of the document.</param>
    /// <param name="Sections">One page of the outline, or <c>null</c> when a section is read.</param>
    /// <param name="Section">The <see cref="SectionSummary"/> of the read section, or <c>null</c> for the outline.</param>
    /// <param name="Text">One page of the text of the read section, made of whole lines, or <c>null</c> for the outline.</param>
    /// <param name="Requirements">
    /// The <see cref="RequirementCoverage"/> of the whole document for the outline, or of the read section, or <c>null</c>
    /// when no identifier pattern is given.
    /// </param>
    /// <param name="NextOffset">The offset that gives the next page, or <c>null</c> when this page is the last one.</param>
    public sealed record DocumentContent(string FileName, int SectionCount, IReadOnlyList<SectionSummary> Sections, SectionSummary Section, string Text,
        RequirementCoverage Requirements, int? NextOffset);
}
