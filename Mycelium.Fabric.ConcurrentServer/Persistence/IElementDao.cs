// ------------------------------------------------------------------------------------------------
//  <copyright file="IElementDao.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Defines the data access object that writes and reads the versions of the elements of one metaclass in the SysML v2 schema.
    /// </summary>
    /// <remarks>
    /// A version is append-only (Systems Modeling API and Services 1.0 §7.1.2), so writing a new version and reading one back are the only
    /// operations; a deletion is a version without payload.
    /// </remarks>
    public interface IElementDao
    {
        /// <summary>
        /// Gets the <see cref="SysML2.NET.Core.ClassKind"/> of the metaclass whose versions are stored.
        /// </summary>
        ClassKind ClassKind { get; }

        /// <summary>
        /// Gets the exact data transfer object type of the metaclass whose versions are stored.
        /// </summary>
        Type DataType { get; }

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
        Task<ErrorOr<IReadOnlyList<IElement>>> ReadAsync(ISqlTransaction transaction, Guid projectId, IReadOnlyCollection<Guid> versionIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// Writes, in at most two statements, versions of elements of the metaclass.
        /// </summary>
        /// <param name="transaction">The <see cref="ISqlTransaction"/> the operation takes part in.</param>
        /// <param name="projectId">The identifier of the project the versions belong to.</param>
        /// <param name="versions">The versions that are written, each with its identifier, identity and commit; one without payload is a deletion.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors the store raised.</returns>
        Task<ErrorOr<Success>> WriteAsync(ISqlTransaction transaction, Guid projectId, IReadOnlyCollection<DataVersion> versions, CancellationToken cancellationToken);
    }
}
