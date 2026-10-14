// ------------------------------------------------------------------------------------------------
//  <copyright file="SectionSummary.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// A section of a document, as listed in the outline given by the <c>read_document</c> tool.
    /// </summary>
    /// <param name="Number">The number of the section, from 1, which designates it in the <c>read_document</c> tool.</param>
    /// <param name="Level">The level of its heading, from 1 to 6, or 0 for the text before the first heading.</param>
    /// <param name="Title">The text of its heading.</param>
    /// <param name="LineCount">The number of lines of its text, without its subsections.</param>
    /// <param name="RequirementCount">
    /// The number of requirement identifiers found in the section, or <c>null</c> when no identifier pattern is given.
    /// </param>
    /// <param name="ModelledCount">
    /// The number of these identifiers that a requirement of the model has as <c>reqId</c>, or <c>null</c> when no
    /// identifier pattern is given.
    /// </param>
    public sealed record SectionSummary(int Number, int Level, string Title, int LineCount, int? RequirementCount, int? ModelledCount);
}
