// ------------------------------------------------------------------------------------------------
//  <copyright file="IElementDao´.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Defines the <see cref="IElementDao"/> of one metaclass, which reads its elements as their exact data transfer object type.
    /// </summary>
    /// <typeparam name="TElement">The exact data transfer object type of the metaclass.</typeparam>
    public interface IElementDao<TElement> : IElementDao where TElement : class, IElement
    {
        /// <summary>
        /// Reads, in one query, the live versions of elements of the metaclass with any of the given identifiers.
        /// </summary>
        /// <param name="transaction">The <see cref="ISqlTransaction"/> the operation takes part in.</param>
        /// <param name="projectId">The identifier of the project the versions belong to.</param>
        /// <param name="versionIds">The identifiers of the versions that are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>
        /// The elements, none for an identifier that names a deletion, another metaclass or no version, or the errors the store raised.
        /// </returns>
        new Task<ErrorOr<IReadOnlyList<TElement>>> ReadAsync(ISqlTransaction transaction, Guid projectId, IReadOnlyCollection<Guid> versionIds,
            CancellationToken cancellationToken);
    }
}
