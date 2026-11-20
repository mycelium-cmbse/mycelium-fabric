// ------------------------------------------------------------------------------------------------
//  <copyright file="IChangeValidator.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Validates a change before it is committed.
    /// </summary>
    public interface IChangeValidator
    {
        /// <summary>
        /// Validates every element a change creates or updates, with the <see cref="ICoreDataService"/> of its metaclass.
        /// </summary>
        /// <param name="context">The <see cref="IChangeContext"/> of the change.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or the errors of every element together.</returns>
        Task<ErrorOr<Success>> ValidateAsync(IChangeContext context, CancellationToken cancellationToken);
    }
}
