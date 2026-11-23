// ------------------------------------------------------------------------------------------------
//  <copyright file="IBaseService.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Mycelium.SDK.DTO;
    using Mycelium.SDK.Http;

    /// <summary>
    /// Defines the Functional Data service operations of the objects of one concrete model class, from which every generated
    /// <c>I&lt;ConcreteClass&gt;Service</c> derives.
    /// </summary>
    /// <typeparam name="TThing">The DTO interface of the concrete model class.</typeparam>
    public interface IBaseService<TThing> where TThing : class, IThing
    {
        /// <summary>
        /// Reads the object of the concrete class that has the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the object to read.</param>
        /// <param name="queryParameter">The <see cref="QueryParameter"/> of the read; must not be null.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The object, or the errors of the read.</returns>
        Task<ErrorOr<TThing>> ReadByIdAsync(Guid id, QueryParameter queryParameter, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the objects of the concrete class that have the specified identifiers.
        /// </summary>
        /// <param name="ids">The identifiers of the objects to read.</param>
        /// <param name="queryParameter">The <see cref="QueryParameter"/> of the read; must not be null.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The objects, or the errors of the read.</returns>
        Task<ErrorOr<IReadOnlyList<TThing>>> ReadByIdsAsync(IReadOnlyList<Guid> ids, QueryParameter queryParameter, CancellationToken cancellationToken);

        /// <summary>
        /// Reads every object of the concrete class.
        /// </summary>
        /// <param name="queryParameter">The <see cref="QueryParameter"/> of the read; must not be null.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The objects, or the errors of the read.</returns>
        Task<ErrorOr<IReadOnlyList<TThing>>> ReadAllAsync(QueryParameter queryParameter, CancellationToken cancellationToken);

        /// <summary>
        /// Reads every object of the concrete class whose searchable index has the specified value.
        /// </summary>
        /// <param name="indexName">The name of the searchable index.</param>
        /// <param name="value">The searched value.</param>
        /// <param name="queryParameter">The <see cref="QueryParameter"/> of the read; must not be null.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The matching objects, or the errors of the read.</returns>
        Task<ErrorOr<IReadOnlyList<TThing>>> ReadByIndexAsync(string indexName, string value, QueryParameter queryParameter, CancellationToken cancellationToken);

        /// <summary>
        /// Creates an object of the concrete class.
        /// </summary>
        /// <param name="thing">The object to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns><see cref="Created"/>, or the errors of the creation.</returns>
        Task<ErrorOr<Created>> CreateAsync(TThing thing, CancellationToken cancellationToken);

        /// <summary>
        /// Updates an object of the concrete class.
        /// </summary>
        /// <param name="thing">The new state of the object, identified by its <see cref="IThing.Id"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns><see cref="Updated"/>, or the errors of the update.</returns>
        Task<ErrorOr<Updated>> UpdateAsync(TThing thing, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes the object of the concrete class that has the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the object to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns><see cref="Deleted"/>, or the errors of the deletion.</returns>
        Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken cancellationToken);
    }
}
