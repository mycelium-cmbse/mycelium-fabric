// ------------------------------------------------------------------------------------------------
//  <copyright file="TransactionMode.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    /// <summary>
    /// Specifies whether a transaction may write to the data store.
    /// </summary>
    public enum TransactionMode
    {
        /// <summary>
        /// The transaction only reads.
        /// </summary>
        ReadOnly,

        /// <summary>
        /// The transaction reads and writes.
        /// </summary>
        ReadWrite
    }
}
