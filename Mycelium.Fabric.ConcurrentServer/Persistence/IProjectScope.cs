// ------------------------------------------------------------------------------------------------
//  <copyright file="IProjectScope.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;

    /// <summary>
    /// Carries the project that the reads and writes of the current request are confined to.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §8.1.3 (pp. 50–56) nests project-owned records under the
    /// <c>projectId</c> of the route, while several PIM operations of §7.2.3 and §7.2.4 take no project argument.
    /// </remarks>
    public interface IProjectScope
    {
        /// <summary>
        /// Gets the identifier of the project in scope.
        /// </summary>
        /// <exception cref="InvalidOperationException">No project is in scope.</exception>
        Guid ProjectId { get; }

        /// <summary>
        /// Confines the current request to the given project until the returned handle is disposed.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <returns>The <see cref="IDisposable"/> that leaves the scope when disposed.</returns>
        /// <exception cref="ArgumentException"><paramref name="projectId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="InvalidOperationException">A project is already in scope.</exception>
        IDisposable Enter(Guid projectId);
    }
}
