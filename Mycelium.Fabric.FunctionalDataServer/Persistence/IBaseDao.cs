// ------------------------------------------------------------------------------------------------
//  <copyright file="IBaseDao.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Mycelium.SDK.DTO;

    using Npgsql;

    /// <summary>
    /// The non-generic base of the Functional Data access objects: it identifies them as one family for registration and reads their objects as
    /// <see cref="IThing"/>, so that a caller can read without naming the concrete class.
    /// </summary>
    public interface IBaseDao
    {
        /// <summary>
        /// Reads the stored object of the exact concrete class of this data access object that has the specified identifier.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the query.</param>
        /// <param name="id">The identifier of the object to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>The stored object, or <see langword="null"/> when no object of the class has <paramref name="id"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<IThing> ReadThingByIdAsync(NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the stored objects of the exact concrete class of this data access object among the specified identifiers.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the query.</param>
        /// <param name="ids">The identifiers of the objects to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>
        /// The stored objects found, in no guaranteed order; identifiers with no object of the class are skipped, and an empty list of identifiers
        /// gives an empty list.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="ids"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<IReadOnlyList<IThing>> ReadThingsByIdsAsync(NpgsqlTransaction transaction, IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    }
}
