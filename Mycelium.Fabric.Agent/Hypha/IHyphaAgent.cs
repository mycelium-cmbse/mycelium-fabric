// ------------------------------------------------------------------------------------------------
//  <copyright file="IHyphaAgent.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Agent.Hypha
{
    using System.Collections.Generic;
    using System.Threading;

    using Microsoft.Extensions.AI;

    /// <summary>
    /// Hypha, the assistant of Bloom: answers the messages of a conversation with the LLM and the tools of Fabric, and keeps
    /// the conversations in memory.
    /// </summary>
    public interface IHyphaAgent
    {
        /// <summary>
        /// Answers a message of a conversation, once the previous answer of the same conversation is over.
        /// </summary>
        /// <param name="threadId">The identifier of the conversation, one per user and project.</param>
        /// <param name="message">The message of the user.</param>
        /// <param name="context">What the user works on, such as the project and the selected elements, for this answer only; <c>null</c> when none.</param>
        /// <param name="cancellationToken">Cancels the answer, as when the user closes the stream or sends a message at once.</param>
        /// <returns>The parts of the answer as the LLM writes them, with the calls of tools and their results.</returns>
        IAsyncEnumerable<ChatResponseUpdate> RunAsync(string threadId, ChatMessage message, string context, CancellationToken cancellationToken);

        /// <summary>
        /// Gets every message of a conversation, with the calls of tools and their results, even those already summarized for the LLM.
        /// </summary>
        /// <param name="threadId">The identifier of the conversation.</param>
        /// <returns>The messages in their order, or an empty list for a conversation that has none.</returns>
        IReadOnlyList<ChatMessage> GetHistory(string threadId);
    }
}
