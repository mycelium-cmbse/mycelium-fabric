// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementProposalStore.cs" company="Starion Group S.A.">
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
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    using Mycelium.Fabric.Mcp.Extraction;

    /// <summary>
    /// An <see cref="IRequirementProposalStore"/> that keeps the last proposals in memory: they are lost when the server stops.
    /// </summary>
    public class RequirementProposalStore : IRequirementProposalStore
    {
        /// <summary>
        /// The number of proposals kept: one per section of a long document, the oldest forgotten first.
        /// </summary>
        public const int MaximumCount = 20;

        /// <summary>
        /// The number of hexadecimal characters of a token: 128 random bits.
        /// </summary>
        private const int TokenLength = 32;

        /// <summary>
        /// The lock that prevents two calls from changing the proposals at the same time.
        /// </summary>
        private readonly Lock proposalLock = new();

        /// <summary>
        /// The kept proposals, the oldest first. Guarded by <see cref="proposalLock"/>.
        /// </summary>
        private readonly List<RequirementProposal> proposals = [];

        /// <summary>
        /// The saved rows of each section, the oldest first. Guarded by <see cref="proposalLock"/>.
        /// </summary>
        private readonly List<(string FileName, int SectionNumber, IReadOnlyList<SavedRow> Rows)> savedLists = [];

        /// <summary>
        /// Keeps a proposal, with a new identifier and a new secret token drawn by a cryptographic random number generator, so
        /// that it cannot be guessed, and with the rows saved for its section; the oldest proposal is forgotten when
        /// <see cref="MaximumCount"/> are kept.
        /// </summary>
        /// <param name="proposal">The <see cref="RequirementProposal"/> to keep.</param>
        /// <returns>The kept <see cref="RequirementProposal"/>, with its identifier, its token and its saved rows.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="proposal"/> is <c>null</c>.</exception>
        public RequirementProposal Add(RequirementProposal proposal)
        {
            ArgumentNullException.ThrowIfNull(proposal);

            lock (this.proposalLock)
            {
                var keptProposal = proposal with
                {
                    Id = Guid.NewGuid(),
                    Token = RandomNumberGenerator.GetHexString(TokenLength, true),
                    SavedRows = this.savedLists.Find(saved => IsSameSection(saved.FileName, saved.SectionNumber, proposal)).Rows
                };

                if (this.proposals.Count == MaximumCount)
                {
                    this.proposals.RemoveAt(0);
                }

                this.proposals.Add(keptProposal);

                return keptProposal;
            }
        }

        /// <summary>
        /// Gets a kept proposal, when the identifier and the token are its own. The tokens are compared in constant time.
        /// </summary>
        /// <param name="proposalId">The identifier of the proposal.</param>
        /// <param name="token">The secret token of the proposal.</param>
        /// <returns>The <see cref="RequirementProposal"/>, or <c>null</c> when no proposal has this identifier and this token.</returns>
        public RequirementProposal TryGet(Guid proposalId, string token)
        {
            if (token == null)
            {
                return null;
            }

            lock (this.proposalLock)
            {
                var proposal = this.proposals.Find(candidate => candidate.Id == proposalId);

                return proposal != null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(proposal.Token)) ? proposal : null;
            }
        }

        /// <summary>
        /// Saves the rows of a section of a document as the user has left them, in place of the rows saved earlier for this
        /// section; the oldest saved section is forgotten when <see cref="MaximumCount"/> are kept.
        /// </summary>
        /// <param name="proposal">The <see cref="RequirementProposal"/> whose rows are saved.</param>
        /// <param name="rows">The <see cref="SavedRow"/>s.</param>
        /// <exception cref="ArgumentNullException">Thrown when a parameter is <c>null</c>.</exception>
        public void SaveRows(RequirementProposal proposal, IReadOnlyList<SavedRow> rows)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            ArgumentNullException.ThrowIfNull(rows);

            lock (this.proposalLock)
            {
                this.savedLists.RemoveAll(saved => IsSameSection(saved.FileName, saved.SectionNumber, proposal));

                if (this.savedLists.Count == MaximumCount)
                {
                    this.savedLists.RemoveAt(0);
                }

                this.savedLists.Add((proposal.FileName, proposal.SectionNumber, rows));
            }
        }

        /// <summary>
        /// Forgets a proposal, once imported, and the rows saved for its section; nothing happens when no proposal has this
        /// identifier.
        /// </summary>
        /// <param name="proposalId">The identifier of the proposal.</param>
        public void Remove(Guid proposalId)
        {
            lock (this.proposalLock)
            {
                var proposal = this.proposals.Find(candidate => candidate.Id == proposalId);

                if (proposal != null)
                {
                    this.proposals.Remove(proposal);
                    this.savedLists.RemoveAll(saved => IsSameSection(saved.FileName, saved.SectionNumber, proposal));
                }
            }
        }

        /// <summary>
        /// Tells whether saved rows belong to the section of a proposal.
        /// </summary>
        /// <param name="fileName">The name of the document of the saved rows.</param>
        /// <param name="sectionNumber">The number of the section of the saved rows.</param>
        /// <param name="proposal">The <see cref="RequirementProposal"/>.</param>
        /// <returns><c>true</c> when the saved rows belong to the section of the proposal.</returns>
        private static bool IsSameSection(string fileName, int sectionNumber, RequirementProposal proposal)
        {
            return fileName == proposal.FileName && sectionNumber == proposal.SectionNumber;
        }
    }
}
