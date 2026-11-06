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
        /// Keeps a proposal, with a new identifier and a new secret token drawn by a cryptographic random number generator, so
        /// that it cannot be guessed; the oldest proposal is forgotten when <see cref="MaximumCount"/> are kept.
        /// </summary>
        /// <param name="proposal">The <see cref="RequirementProposal"/> to keep.</param>
        /// <returns>The kept <see cref="RequirementProposal"/>, with its identifier and its token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="proposal"/> is <c>null</c>.</exception>
        public RequirementProposal Add(RequirementProposal proposal)
        {
            ArgumentNullException.ThrowIfNull(proposal);

            var keptProposal = proposal with { Id = Guid.NewGuid(), Token = RandomNumberGenerator.GetHexString(TokenLength, true) };

            lock (this.proposalLock)
            {
                if (this.proposals.Count == MaximumCount)
                {
                    this.proposals.RemoveAt(0);
                }

                this.proposals.Add(keptProposal);
            }

            return keptProposal;
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
        /// Forgets a proposal, once imported; nothing happens when no proposal has this identifier.
        /// </summary>
        /// <param name="proposalId">The identifier of the proposal.</param>
        public void Remove(Guid proposalId)
        {
            lock (this.proposalLock)
            {
                this.proposals.RemoveAll(proposal => proposal.Id == proposalId);
            }
        }
    }
}
