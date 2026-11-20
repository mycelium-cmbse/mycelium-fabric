// ------------------------------------------------------------------------------------------------
//  <copyright file="IElementRule´.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Defines a validation rule of the elements of a change that are of a given type, including its specializations.
    /// </summary>
    /// <typeparam name="TElement">The type of element the rule validates.</typeparam>
    /// <remarks>
    /// The type parameter is contravariant, so a rule written once for an abstract metaclass applies to the service of every concrete
    /// metaclass that specializes it.
    /// </remarks>
    public interface IElementRule<in TElement> : IElementRule where TElement : class, IElement
    {
        /// <summary>
        /// Validates an element that a change creates or updates.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="element">The element.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or every error the rule finds on the element.</returns>
        Task<ErrorOr<Success>> ValidateAsync(IChangeContext context, TElement element, CancellationToken cancellationToken);
    }
}
