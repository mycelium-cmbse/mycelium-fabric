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
    using System.Collections.Generic;
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
    /// Systems Modeling API and Services 1.0 §7.1.2 (pp. 32–36) makes <c>Data</c> the entity a
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
        /// Reads, in one query, the payload with any of the given identifiers as it stands at a commit.
        /// </summary>
        /// <typeparam name="TData">The type of payload that is read.</typeparam>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the payload is read at.</param>
        /// <param name="dataIds">The identifiers of the payload that is read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The payload that exists at the commit, none for an identifier that does not, or the errors that prevented reading it.</returns>
        Task<ErrorOr<IReadOnlyList<TData>>> ReadByIdsAsync<TData>(IDataStoreTransaction transaction, Guid commitId, IReadOnlyCollection<Guid> dataIds,
            CancellationToken cancellationToken) where TData : class, IData;

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
        /// Reads the identifiers of the data that cannot outlive the given elements at a commit.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit the data is read at.</param>
        /// <param name="elementIds">The identifiers of the elements the closure starts from.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        /// <remarks>
        /// §7.2.3 <c>createCommit</c> deletes the incoming relationships of a deleted element, transitively since a relationship is itself an
        /// element, and the external relationships whose element end is deleted (§7.1.3). References held in the stored properties of other
        /// elements are not part of the closure.
        /// </remarks>
        Task<ErrorOr<IReadOnlyList<Guid>>> ReadDependentDataClosureAsync(IDataStoreTransaction transaction, Guid commitId, IReadOnlyList<Guid> elementIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// Reads the identifier of the project that owns a data identity.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="identityId">The identifier of the data identity.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The identifier of the owning project, or <see cref="ErrorType.NotFound"/> when no data identity has the identifier.</returns>
        /// <remarks>
        /// The model gives a data identity no project of its own while the store keeps one; a creation reuses only an identity of its own project.
        /// </remarks>
        Task<ErrorOr<Guid>> ReadIdentityOwnerAsync(IDataStoreTransaction transaction, Guid identityId, CancellationToken cancellationToken);

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
        /// <see cref="Query"/> becomes the filter and, as this server reads the <c>scope</c> §7.1.4 leaves open, the scope becomes a
        /// condition on the identifier. Evaluating it is the store's work, since it holds the index the condition selects
        /// on and a service filtering in memory would have to read the whole commit first.
        /// </remarks>
        Task<ErrorOr<PagedResult<IData>>> ReadMatchingAsync(IDataStoreTransaction transaction, Guid commitId, SearchFilter filter, QueryParameters queryParameters, CancellationToken cancellationToken);
    }
}
