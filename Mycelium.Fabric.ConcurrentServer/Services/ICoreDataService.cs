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
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Defines the service of a metaclass for an element whose type is only known at runtime, as the <see cref="ICoreDataServiceProvider"/> resolves it.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.2.3 makes <c>createCommit</c> the only write of element data, so the service computes the
    /// effects of a change and validates its elements inside the transaction of the commit, and writes nothing.
    /// </remarks>
    public interface ICoreDataService
    {
        /// <summary>
        /// Computes the versions that a change of an element entails.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="element">The element, as it stands before the change when the change deletes it.</param>
        /// <param name="changeType">The <see cref="ChangeType"/> of the element in the change.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The versions to add to the change, or every error the effects found.</returns>
        Task<ErrorOr<IReadOnlyList<DataVersion>>> ComputeEffectsAsync(IChangeContext context, IElement element, ChangeType changeType, CancellationToken cancellationToken);

        /// <summary>
        /// Validates an element that a change creates or updates.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="element">The element, of the data type the service was resolved for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or every error the element carries.</returns>
        Task<ErrorOr<Success>> ValidateAsync(IChangeContext context, IElement element, CancellationToken cancellationToken);
    }
}
