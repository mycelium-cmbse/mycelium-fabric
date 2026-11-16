// ------------------------------------------------------------------------------------------------
//  <copyright file="IDatabaseHandler.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    /// <summary>
    /// Manages the one transaction of the current request, independently of the data store behind it.
    /// </summary>
    public interface IDatabaseHandler
    {
        /// <summary>
        /// Gets a value indicating whether a transaction is open for the current request.
        /// </summary>
        bool HasActiveTransaction { get; }

        /// <summary>
        /// Opens the transaction of the current request, or joins it when it is already open.
        /// </summary>
        /// <param name="mode">The <see cref="TransactionMode"/> the operation requires.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The <see cref="ITransactionScope"/> of the operation, or the errors that prevented opening the transaction.</returns>
        /// <exception cref="InvalidOperationException">A <see cref="TransactionMode.ReadWrite"/> scope is requested inside a read-only transaction.</exception>
        Task<ErrorOr<ITransactionScope>> BeginTransactionAsync(TransactionMode mode, CancellationToken cancellationToken);
    }
}
