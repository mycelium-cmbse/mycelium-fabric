// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementProposal.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The requirements that the AI assistant has found in a section of a document, checked by the server: what the view of the
    /// extraction shows, and what the user imports from it.
    /// </summary>
    /// <param name="FileName">The name of the document.</param>
    /// <param name="SectionNumber">The number of the section, as the outline gives it.</param>
    /// <param name="SectionTitle">The title of the section.</param>
    /// <param name="OwnerId">
    /// The <c>Id</c> of the package that owns the requirements, or under which <paramref name="NewPackage"/> is created; <c>null</c>
    /// to create <paramref name="NewPackage"/> at the top level.
    /// </param>
    /// <param name="NewPackage">The name of the package that the import creates to own the requirements, or <c>null</c>.</param>
    /// <param name="Requirements">The <see cref="ExtractedRequirement"/>s, in the order given by the AI assistant.</param>
    /// <param name="MissedIds">
    /// The identifiers that the section and its subsections define, that the proposal does not hold and that the model does not
    /// have; empty when no identifier pattern is given.
    /// </param>
    public sealed record RequirementProposal(string FileName, int SectionNumber, string SectionTitle, Guid? OwnerId, string NewPackage,
        IReadOnlyList<ExtractedRequirement> Requirements, IReadOnlyList<string> MissedIds)
    {
        /// <summary>
        /// Gets the qualified name of the package designated by <see cref="OwnerId"/>, shown by the view, or <c>null</c>.
        /// </summary>
        public string OwnerName { get; init; }

        /// <summary>
        /// Gets the identifier of the proposal, given by the server when it keeps the proposal.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Gets the secret that the view sends with the import, so that only the view can import the proposal: it is sent to the
        /// view only, never to the AI assistant; <c>null</c> until the server keeps the proposal.
        /// </summary>
        public string Token { get; init; }
    }
}
