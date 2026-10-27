// ------------------------------------------------------------------------------------------------
//  <copyright file="BatchReview.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// Where a batch of changes stands, sent to the view of the batch by apply_changes and decide_batch.
    /// </summary>
    /// <param name="Status">The <see cref="BatchReviewStatus"/> of the batch.</param>
    /// <param name="Message">
    /// The message for the AI assistant: the view writes it in the conversation after a decision of the user, so that the
    /// assistant goes on.
    /// </param>
    public sealed record BatchReview(BatchReviewStatus Status, string Message)
    {
        /// <summary>
        /// Gets the <see cref="BatchPreview"/> of a batch that waits for the decision of the user, otherwise <c>null</c>.
        /// </summary>
        public BatchPreview Preview { get; init; }

        /// <summary>
        /// Gets the <see cref="ApplyChangesResult"/> of an applied or refused batch, otherwise <c>null</c>.
        /// </summary>
        public ApplyChangesResult Result { get; init; }
    }
}
