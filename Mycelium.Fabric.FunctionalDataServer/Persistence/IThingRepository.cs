// ------------------------------------------------------------------------------------------------
//  <copyright file="IThingRepository.cs" company="Starion Group S.A.">
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
    /// Reads stored objects of any concrete class from their identifiers, inside the caller's transaction, without the caller naming their class.
    /// </summary>
    public interface IThingRepository
    {
        /// <summary>
        /// Reads the stored object that has the specified identifier, whatever its concrete class.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the queries.</param>
        /// <param name="id">The identifier of the object to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>The stored object, or <see langword="null"/> when no object has <paramref name="id"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="ArgumentException">The stored class kind of the object is handled by no data access object.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<IThing> ReadByIdAsync(NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the stored objects that have the specified identifiers, whatever their concrete classes.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the queries.</param>
        /// <param name="ids">The identifiers of the objects to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>
        /// The stored objects found, each once and in no guaranteed order; identifiers with no object are skipped, and an empty list of identifiers
        /// gives an empty list.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="ids"/> is null.</exception>
        /// <exception cref="ArgumentException">The stored class kind of an object is handled by no data access object.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<IReadOnlyList<IThing>> ReadByIdsAsync(NpgsqlTransaction transaction, IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    }
}
