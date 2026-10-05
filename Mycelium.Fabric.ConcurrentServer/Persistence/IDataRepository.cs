// ------------------------------------------------------------------------------------------------
//  <copyright file="IDataRepository.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Common;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    /// <summary>
    /// Defines the persistence contract of the versioned payload that a commit carries.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.1.1 (pp. 29–32) makes <c>Data</c> the entity a
    /// <c>DataVersion</c> wraps rather than a record of its own, so it has no class in the API model and
    /// no generated repository. The reads below are the ones §7.2.2, §7.2.4, §7.2.5 and §7.2.6 issue
    /// against the payload of a single commit.
    /// </remarks>
    public interface IDataRepository : IBaseRepository
    {
        /// <summary>
        /// Reads the payload with the given identifier as it stands at a commit.
        /// </summary>
        /// <typeparam name="TData">The type of payload that is read.</typeparam>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the payload is read at.</param>
        /// <param name="dataId">The identifier of the payload that is read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<TData>> ReadByIdAsync<TData>(IDataStoreTransaction transaction, Guid commitId, Guid dataId, CancellationToken cancellationToken) where TData : class, IData;

        /// <summary>
        /// Reads one page of the payload of a given type as it stands at a commit.
        /// </summary>
        /// <typeparam name="TData">The type of payload that is read.</typeparam>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the payload is read at.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<PagedResult<TData>>> ReadAsync<TData>(IDataStoreTransaction transaction, Guid commitId, QueryParameters queryParameters, CancellationToken cancellationToken) where TData : class, IData;

        /// <summary>
        /// Reads one page of the elements that no other element owns at a commit.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the elements are read at.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<PagedResult<IElement>>> ReadRootElementsAsync(IDataStoreTransaction transaction, Guid commitId, QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Reads one page of the relationships that relate to an element at a commit.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the relationships are read at.</param>
        /// <param name="relatedElementId">The identifier of the element the relationships relate to.</param>
        /// <param name="direction">The end the element is related by.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<PagedResult<IRelationship>>> ReadRelationshipsAsync(IDataStoreTransaction transaction, Guid commitId, Guid relatedElementId, Direction direction, QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Reads one page of the external relationships that an element is an end of at a commit.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the external relationships are read at.</param>
        /// <param name="elementId">The identifier of the element that is an end of the external relationships.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<PagedResult<ExternalRelationship>>> ReadExternalRelationshipsByElementAsync(IDataStoreTransaction transaction, Guid commitId, Guid elementId, QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Reads one page of the payload that satisfies a filter at a commit.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the filter is evaluated at.</param>
        /// <param name="filter">The <see cref="SearchFilter"/> the payload is narrowed by.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        /// <remarks>
        /// This is how §7.2.4 <c>executeQuery</c> reaches the store: the <c>where</c> of a
        /// <see cref="Query"/> becomes the filter and its <c>scope</c> becomes a condition on the
        /// identifier. Evaluating it is the store's work, since it holds the index the condition selects
        /// on and a service filtering in memory would have to read the whole commit first.
        /// </remarks>
        Task<ErrorOr<PagedResult<IData>>> ReadMatchingAsync(IDataStoreTransaction transaction, Guid commitId, SearchFilter filter, QueryParameters queryParameters, CancellationToken cancellationToken);
    }
}
