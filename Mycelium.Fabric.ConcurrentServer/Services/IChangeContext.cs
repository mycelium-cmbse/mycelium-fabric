// ------------------------------------------------------------------------------------------------
//  <copyright file="IChangeContext.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Common;
    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Gives the effects and the validation of a change access to the change and to the data it can reference once it is committed.
    /// </summary>
    public interface IChangeContext
    {
        /// <summary>
        /// Gets the <see cref="DataVersion"/>s the commit writes, a version without payload being a deletion.
        /// </summary>
        IReadOnlyList<DataVersion> Change { get; }

        /// <summary>
        /// Gets the state at the previous commits, by data identity.
        /// </summary>
        IReadOnlyDictionary<Guid, DataVersion> PreviousState { get; }

        /// <summary>
        /// Gets, in one call, the data with any of the given identities that a change can reference once it is committed.
        /// </summary>
        /// <param name="identityIds">The identifiers of the data identities.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The data found, by identity, none for an identity that cannot be referenced, or the errors that prevented reading it.</returns>
        Task<ErrorOr<IReadOnlyDictionary<Guid, IData>>> GetDataAsync(IReadOnlyCollection<Guid> identityIds, CancellationToken cancellationToken);
    }
}
