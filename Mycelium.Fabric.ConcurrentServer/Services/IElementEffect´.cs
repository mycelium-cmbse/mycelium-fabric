// ------------------------------------------------------------------------------------------------
//  <copyright file="IElementEffect´.cs" company="Starion Group S.A.">
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
    /// Defines an effect that the change of an element of a given type, including its specializations, has on the rest of the data.
    /// </summary>
    /// <typeparam name="TElement">The type of element whose change has the effect.</typeparam>
    /// <remarks>
    /// The type parameter is contravariant, so an effect written once for an abstract metaclass applies to the service of every concrete
    /// metaclass that specializes it.
    /// </remarks>
    public interface IElementEffect<in TElement> : IElementEffect where TElement : class, IElement
    {
        /// <summary>
        /// Computes the versions that a change of an element entails.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="element">The element, as it stands before the change when the change deletes it.</param>
        /// <param name="changeType">The <see cref="ChangeType"/> of the element in the change.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The versions to add to the change, none for data the change already versions, or the errors the effect found.</returns>
        Task<ErrorOr<IReadOnlyList<DataVersion>>> ComputeAsync(IChangeContext context, TElement element, ChangeType changeType, CancellationToken cancellationToken);
    }
}
