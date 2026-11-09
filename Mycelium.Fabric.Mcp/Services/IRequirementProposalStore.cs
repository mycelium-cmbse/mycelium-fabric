// ------------------------------------------------------------------------------------------------
//  <copyright file="IRequirementProposalStore.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System;
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Extraction;

    /// <summary>
    /// Keeps the proposals of requirements shown to the user, until the user imports them from the view of the extraction.
    /// </summary>
    public interface IRequirementProposalStore
    {
        /// <summary>
        /// Keeps a proposal, with a new identifier and a new secret token, and with the rows saved for the same section of the
        /// same document, if any.
        /// </summary>
        /// <param name="proposal">The <see cref="RequirementProposal"/> to keep.</param>
        /// <returns>The kept <see cref="RequirementProposal"/>, with its identifier, its token and its saved rows.</returns>
        RequirementProposal Add(RequirementProposal proposal);

        /// <summary>
        /// Gets a kept proposal, when the identifier and the token are its own.
        /// </summary>
        /// <param name="proposalId">The identifier of the proposal.</param>
        /// <param name="token">The secret token of the proposal.</param>
        /// <returns>The <see cref="RequirementProposal"/>, or <c>null</c> when no proposal has this identifier and this token.</returns>
        RequirementProposal TryGet(Guid proposalId, string token);

        /// <summary>
        /// Saves the rows of a section of a document as the user has left them, in place of the rows saved earlier for this section.
        /// </summary>
        /// <param name="proposal">The <see cref="RequirementProposal"/> whose rows are saved.</param>
        /// <param name="rows">The <see cref="SavedRow"/>s.</param>
        void SaveRows(RequirementProposal proposal, IReadOnlyList<SavedRow> rows);

        /// <summary>
        /// Forgets a proposal, once imported, and the rows saved for its section.
        /// </summary>
        /// <param name="proposalId">The identifier of the proposal.</param>
        void Remove(Guid proposalId);
    }
}
