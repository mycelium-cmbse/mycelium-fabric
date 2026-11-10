// ------------------------------------------------------------------------------------------------
//  <copyright file="ICoreDataService.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM.POCO;
    using SysML2.NET.PSM;

    /// <summary>
    /// Defines the operations shared by the services of every concrete SysML v2 and KerML metaclass.
    /// </summary>
    /// <typeparam name="TElement">The type of element the service reads and changes.</typeparam>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.2.3 (pp. 42–45) makes <c>createCommit</c> the only write of
    /// element data, so a write here stages <see cref="DataVersion" /> records in <see cref="Commit.Change" />
    /// of an unsaved commit and persists nothing.
    /// </remarks>
    public interface ICoreDataService<TElement> : IBaseService where TElement : class, IElement
    {
        /// <summary>
        /// Gets the element with the given identifier as it stands at a commit.
        /// </summary>
        /// <param name="projectId">The identifier of the project the commit belongs to.</param>
        /// <param name="commitId">The identifier of the commit the element is read at.</param>
        /// <param name="elementId">The identifier of the element that is read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> that cancels the operation.</param>
        /// <returns>The element, or the errors that prevented reading it.</returns>
        Task<ErrorOr<TElement>> GetByIdAsync(Guid projectId, Guid commitId, Guid elementId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the elements with the given identifiers as they stand at a commit.
        /// </summary>
        /// <param name="projectId">The identifier of the project the commit belongs to.</param>
        /// <param name="commitId">The identifier of the commit the elements are read at.</param>
        /// <param name="elementIds">The identifiers of the elements that are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> that cancels the operation.</param>
        /// <returns>The elements, or the errors that prevented reading them.</returns>
        Task<ErrorOr<IReadOnlyList<TElement>>> GetManyAsync(Guid projectId, Guid commitId, IReadOnlyList<Guid> elementIds, CancellationToken cancellationToken);

        /// <summary>
        /// Gets one page of the elements as they stand at a commit.
        /// </summary>
        /// <param name="projectId">The identifier of the project the commit belongs to.</param>
        /// <param name="commitId">The identifier of the commit the elements are read at.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> that cancels the operation.</param>
        /// <returns>The page of elements, or the errors that prevented reading it.</returns>
        Task<ErrorOr<PagedResult<TElement>>> GetAsync(Guid projectId, Guid commitId, QueryParameters queryParameters, CancellationToken cancellationToken);

        /// <summary>
        /// Validates the creation of an element and stages it in a commit.
        /// </summary>
        /// <param name="commit">The unsaved <see cref="Commit" /> whose change receives the creation.</param>
        /// <param name="element">The element that is created.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented staging the creation.</returns>
        Task<ErrorOr<Success>> CreateAsync(Commit commit, TElement element, CancellationToken cancellationToken);

        /// <summary>
        /// Validates the update of an element and stages it in a commit.
        /// </summary>
        /// <param name="commit">The unsaved <see cref="Commit" /> whose change receives the update.</param>
        /// <param name="element">The element in its updated state.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented staging the update.</returns>
        Task<ErrorOr<Success>> UpdateAsync(Commit commit, TElement element, CancellationToken cancellationToken);

        /// <summary>
        /// Validates the deletion of an element and stages it, with the deletions it entails, in a commit.
        /// </summary>
        /// <param name="commit">The unsaved <see cref="Commit" /> whose change receives the deletion.</param>
        /// <param name="elementId">The identifier of the element that is deleted.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented staging the deletion.</returns>
        /// <remarks>
        /// §7.2.3 requires the deletion of an element to delete its incoming relationships as well.
        /// </remarks>
        Task<ErrorOr<Success>> DeleteAsync(Commit commit, Guid elementId, CancellationToken cancellationToken);
    }
}
