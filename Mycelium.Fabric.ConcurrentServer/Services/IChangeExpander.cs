// ------------------------------------------------------------------------------------------------
//  <copyright file="IChangeExpander.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Expands a change with the versions its effects entail, before it is validated and committed.
    /// </summary>
    public interface IChangeExpander
    {
        /// <summary>
        /// Computes the versions that the given versions of a change entail, with the <see cref="ICoreDataService"/> of the metaclass of each element.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change, which holds the given versions.</param>
        /// <param name="versions">The versions whose effects are computed.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The versions to add to the change, or the errors of every element together.</returns>
        Task<ErrorOr<IReadOnlyList<DataVersion>>> ComputeEffectsAsync(IChangeContext context, IReadOnlyList<DataVersion> versions, CancellationToken cancellationToken);
    }
}
