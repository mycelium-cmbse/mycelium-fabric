// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementNavigationService.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM;
    using SysML2.NET.PSM;

    /// <summary>
    /// The ElementNavigationService of Systems Modeling API and Services 1.0 §7.2.2, confined to the project of the request.
    /// </summary>
    public class ElementNavigationService : VersionedDataServiceBase, IElementNavigationService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ElementNavigationService"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> that reads the commit the data is read at.</param>
        /// <param name="dataRepository">The <see cref="IDataRepository"/> the elements are read from.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/> that verifies the commit belongs to the project.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public ElementNavigationService(IDatabaseHandler databaseHandler, IProjectScope projectScope, IProjectRepository projectRepository, ICommitRepository commitRepository,
            IDataRepository dataRepository,
            IProjectDataVersioningService versioningService, ILogger<ElementNavigationService> logger)
            : base(databaseHandler, projectScope, projectRepository, commitRepository, dataRepository, versioningService, logger)
        {
        }

        /// <summary>
        /// Gets an element as it stands at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="elementId">The identifier of the element.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The element, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<IElement>> GetElementByIdAsync(Guid projectId, Guid commitId, Guid elementId, CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId,
                transaction => this.DataRepository.ReadByIdAsync<IElement>(transaction, commitId, elementId, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets one page of the elements as they stand at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of elements, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<IElement>>> GetElementsAsync(Guid projectId, Guid commitId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId,
                transaction => this.DataRepository.ReadAsync<IElement>(transaction, commitId, queryParameters, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets one page of the relationships that relate to an element at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="elementId">The identifier of the related element.</param>
        /// <param name="direction">The end the element is related by.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of relationships, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<IRelationship>>> GetRelationshipsByRelatedElementAsync(Guid projectId, Guid commitId, Guid elementId, Direction direction,
            QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId, async transaction =>
            {
                var element = await this.DataRepository.ReadByIdAsync<IElement>(transaction, commitId, elementId, cancellationToken);

                return element.IsError
                    ? element.Errors
                    : await this.DataRepository.ReadRelationshipsAsync(transaction, commitId, elementId, direction, queryParameters, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets one page of the elements no other element owns at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of root elements, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<IElement>>> GetRootElementsAsync(Guid projectId, Guid commitId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId,
                transaction => this.DataRepository.ReadRootElementsAsync(transaction, commitId, queryParameters, cancellationToken), cancellationToken);
        }
    }
}
