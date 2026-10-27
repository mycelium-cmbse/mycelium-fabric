// ------------------------------------------------------------------------------------------------
//  <copyright file="PendingBatchStore.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// An <see cref="IPendingBatchStore"/> that keeps the waiting batch in memory: it is lost when the server stops.
    /// </summary>
    public class PendingBatchStore : IPendingBatchStore
    {
        /// <summary>
        /// The number of hexadecimal characters of a token: 128 random bits.
        /// </summary>
        private const int TokenLength = 32;

        /// <summary>
        /// The lock that prevents two calls from adding or taking a batch at the same time.
        /// </summary>
        private readonly Lock batchLock = new();

        /// <summary>
        /// The batch that waits for the decision of the user, or <c>null</c>. Guarded by <see cref="batchLock"/>.
        /// </summary>
        private PendingBatch pendingBatch;

        /// <summary>
        /// Gets a value indicating whether a batch waits for the decision of the user.
        /// </summary>
        public bool HasPendingBatch
        {
            get
            {
                lock (this.batchLock)
                {
                    return this.pendingBatch != null;
                }
            }
        }

        /// <summary>
        /// Gets the batch that waits for the decision of the user, or <c>null</c>.
        /// </summary>
        public PendingBatch PendingBatch
        {
            get
            {
                lock (this.batchLock)
                {
                    return this.pendingBatch;
                }
            }
        }

        /// <summary>
        /// Keeps a batch until the user decides, with a new identifier and a new secret token drawn by a cryptographic random
        /// number generator, so that it cannot be guessed.
        /// </summary>
        /// <param name="changes">The changes of the batch.</param>
        /// <returns>The kept <see cref="PendingBatch"/>, or <c>null</c> when another batch is already waiting.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="changes"/> is <c>null</c>.
        /// </exception>
        public PendingBatch TryAdd(IReadOnlyList<ModelChange> changes)
        {
            ArgumentNullException.ThrowIfNull(changes);

            lock (this.batchLock)
            {
                if (this.pendingBatch != null)
                {
                    return null;
                }

                this.pendingBatch = new PendingBatch(Guid.NewGuid(), RandomNumberGenerator.GetHexString(TokenLength, true), changes);
                return this.pendingBatch;
            }
        }

        /// <summary>
        /// Removes the waiting batch and returns it, when the identifier and the token are its own. The tokens are compared in
        /// constant time.
        /// </summary>
        /// <param name="batchId">The identifier of the batch.</param>
        /// <param name="token">The secret token of the batch.</param>
        /// <returns>
        /// The removed <see cref="PendingBatch"/>, or <c>null</c> when no batch waits with this identifier and this token;
        /// the waiting batch is then kept.
        /// </returns>
        public PendingBatch TryTake(Guid batchId, string token)
        {
            lock (this.batchLock)
            {
                if (this.pendingBatch == null || this.pendingBatch.Id != batchId || token == null
                    || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(this.pendingBatch.Token)))
                {
                    return null;
                }

                var takenBatch = this.pendingBatch;
                this.pendingBatch = null;
                return takenBatch;
            }
        }
    }
}
