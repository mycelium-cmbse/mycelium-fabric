// ------------------------------------------------------------------------------------------------
//  <copyright file="IRecordRepository.cs" company="Starion Group S.A.">
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

    using SysML2.NET.PIM;
    using SysML2.NET.PSM;

    /// <summary>
    /// Defines the persistence contract shared by every record of the OMG Systems Modeling API and
    /// Services API model.
    /// </summary>
    /// <typeparam name="TRecord">
    /// The type of record that is read and written.
    /// </typeparam>
    /// <remarks>
    /// The generated interfaces under <c>Persistence/AutoGenRepositories/</c> bind this contract to one
    /// record each, so a store implements these six members per record and nothing else. The surface is
    /// deliberately uniform rather than narrowed to the operations the API exposes.
    /// </remarks>
    public interface IRecordRepository<TRecord> : IBaseRepository where TRecord : Record
    {
        /// <summary>
        /// Creates one record.
        /// </summary>
        /// <param name="transaction">
        /// The <see cref="IDataStoreTransaction"/> the operation takes part in.
        /// </param>
        /// <param name="record">
        /// The record that is created.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        Task<ErrorOr<TRecord>> CreateAsync(IDataStoreTransaction transaction, TRecord record, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the record with the given identifier.
        /// </summary>
        /// <param name="transaction">
        /// The <see cref="IDataStoreTransaction"/> the operation takes part in.
        /// </param>
        /// <param name="recordId">
        /// The identifier of the record that is read.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        /// <remarks>
        /// A record that does not exist is an <see cref="ErrorType.NotFound"/> error rather than a null
        /// value, so a caller never has to tell absence from failure.
        /// </remarks>
        Task<ErrorOr<TRecord>> ReadByIdAsync(IDataStoreTransaction transaction, Guid recordId, CancellationToken cancellationToken);

        /// <summary>
        /// Reads one page of records.
        /// </summary>
        /// <param name="transaction">
        /// The <see cref="IDataStoreTransaction"/> the operation takes part in.
        /// </param>
        /// <param name="queryParameters">
        /// The paging parameters applied to the result.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        Task<ErrorOr<PagedResult<TRecord>>> ReadAsync(IDataStoreTransaction transaction, QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Reads one page of the records that satisfy a filter.
        /// </summary>
        /// <param name="transaction">
        /// The <see cref="IDataStoreTransaction"/> the operation takes part in.
        /// </param>
        /// <param name="filter">
        /// The <see cref="SearchFilter"/> the records are narrowed by.
        /// </param>
        /// <param name="queryParameters">
        /// The paging parameters applied to the result.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        /// <remarks>
        /// The filter names a property the store holds an index on, so the condition is applied where the
        /// records are rather than after they have been read.
        /// </remarks>
        Task<ErrorOr<PagedResult<TRecord>>> ReadByFilterAsync(IDataStoreTransaction transaction, SearchFilter filter, QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Updates one record.
        /// </summary>
        /// <param name="transaction">
        /// The <see cref="IDataStoreTransaction"/> the operation takes part in.
        /// </param>
        /// <param name="record">
        /// The record that is updated.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        Task<ErrorOr<TRecord>> UpdateAsync(IDataStoreTransaction transaction, TRecord record, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the record with the given identifier.
        /// </summary>
        /// <param name="transaction">
        /// The <see cref="IDataStoreTransaction"/> the operation takes part in.
        /// </param>
        /// <param name="recordId">
        /// The identifier of the record that is deleted.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> that cancels the operation.
        /// </param>
        /// <returns>
        /// The outcome of the operation, or the errors that prevented it.
        /// </returns>
        Task<ErrorOr<Deleted>> DeleteAsync(IDataStoreTransaction transaction, Guid recordId, CancellationToken cancellationToken);
    }
}
