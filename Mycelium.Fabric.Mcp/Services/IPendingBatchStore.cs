// ------------------------------------------------------------------------------------------------
//  <copyright file="IPendingBatchStore.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// Keeps, in memory, the batch of changes that waits for the decision of the user: at most one at a time.
    /// </summary>
    public interface IPendingBatchStore
    {
        /// <summary>
        /// Gets a value indicating whether a batch waits for the decision of the user.
        /// </summary>
        bool HasPendingBatch { get; }

        /// <summary>
        /// Gets the batch that waits for the decision of the user, or <c>null</c>.
        /// </summary>
        PendingBatch PendingBatch { get; }

        /// <summary>
        /// Keeps a batch until the user decides, with a new identifier and a new secret token.
        /// </summary>
        /// <param name="changes">The changes of the batch.</param>
        /// <returns>The kept <see cref="PendingBatch"/>, or <c>null</c> when another batch is already waiting.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="changes"/> is <c>null</c>.
        /// </exception>
        PendingBatch TryAdd(IReadOnlyList<ModelChange> changes);

        /// <summary>
        /// Removes the waiting batch and returns it, when the identifier and the token are its own.
        /// </summary>
        /// <param name="batchId">The identifier of the batch.</param>
        /// <param name="token">The secret token of the batch.</param>
        /// <returns>
        /// The removed <see cref="PendingBatch"/>, or <c>null</c> when no batch waits with this identifier and this token;
        /// the waiting batch is then kept.
        /// </returns>
        PendingBatch TryTake(Guid batchId, string token);
    }
}
