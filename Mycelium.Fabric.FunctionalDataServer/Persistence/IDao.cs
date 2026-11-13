// ------------------------------------------------------------------------------------------------
//  <copyright file="IDao.cs" company="Starion Group S.A.">
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
    /// Stores the objects of one concrete model class in the Functional Data database, inside the caller's transaction.
    /// </summary>
    /// <typeparam name="TDto">The DTO interface of the concrete model class.</typeparam>
    public interface IDao<TDto> where TDto : class, IThing
    {
        /// <summary>
        /// Reads every stored object of the exact concrete class, ordered by identifier.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the query.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>The stored objects, or an empty list when none exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<IReadOnlyList<TDto>> ReadAllAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the stored object of the exact concrete class that has the specified identifier.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the query.</param>
        /// <param name="id">The identifier of the object to read.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>The stored object, or <see langword="null"/> when no object of the class has <paramref name="id"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the query.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<TDto> ReadByIdAsync(NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Stores a new object: its JSON document, one row per class of its inheritance chain and its link-table rows.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the statements.</param>
        /// <param name="dto">The object to store, identified by its application-supplied <see cref="IThing.Id"/>.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>A task that completes when every statement has been executed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="dto"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a statement.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task CreateAsync(NpgsqlTransaction transaction, TDto dto, CancellationToken cancellationToken);

        /// <summary>
        /// Replaces a stored object's JSON document, scalar reference columns and link-table rows with the values of <paramref name="dto"/>.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the statements.</param>
        /// <param name="dto">The new state of the object, identified by its <see cref="IThing.Id"/>.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>
        /// <see langword="true"/> when the object was updated; <see langword="false"/> when no object of the class has the identifier, in which case
        /// nothing is written.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> or <paramref name="dto"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails a statement.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<bool> UpdateAsync(NpgsqlTransaction transaction, TDto dto, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a stored object; the database removes its inheritance rows, link rows and contained objects.
        /// </summary>
        /// <param name="transaction">The caller's open transaction, whose connection executes the statement.</param>
        /// <param name="id">The identifier of the object to delete.</param>
        /// <param name="cancellationToken">The token canceling the operation.</param>
        /// <returns>
        /// <see langword="true"/> when the object was deleted; <see langword="false"/> when no object of the class has <paramref name="id"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="transaction"/> is null.</exception>
        /// <exception cref="NpgsqlException">The database rejects or fails the statement.</exception>
        /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
        Task<bool> DeleteAsync(NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken);
    }
}
