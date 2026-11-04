// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectScope.cs" company="Starion Group S.A.">
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
    /// The <see cref="IProjectScope"/> of one request, to be registered with a scoped lifetime.
    /// </summary>
    public sealed class ProjectScope : IProjectScope
    {
        /// <summary>
        /// The identifier of the project in scope, or <c>null</c> when none is.
        /// </summary>
        private Guid? enteredProjectId;

        /// <summary>
        /// Gets the identifier of the project in scope.
        /// </summary>
        /// <exception cref="InvalidOperationException">No project is in scope.</exception>
        public Guid ProjectId => this.enteredProjectId ?? throw new InvalidOperationException("No project is in scope for the current request.");

        /// <summary>
        /// Confines the current request to the given project until the returned handle is disposed.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <returns>The <see cref="IDisposable"/> that leaves the scope when disposed.</returns>
        /// <exception cref="ArgumentException"><paramref name="projectId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="InvalidOperationException">A project is already in scope.</exception>
        public IDisposable Enter(Guid projectId)
        {
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("The project identifier of a scope cannot be empty.", nameof(projectId));
            }

            if (this.enteredProjectId.HasValue)
            {
                throw new InvalidOperationException($"The current request is already scoped to project {this.enteredProjectId.Value}.");
            }

            this.enteredProjectId = projectId;

            return new ScopeExit(this);
        }

        /// <summary>
        /// Leaves the scope that a <see cref="ScopeExit"/> handle was issued for.
        /// </summary>
        private void Leave() => this.enteredProjectId = null;

        /// <summary>
        /// The handle returned by <see cref="Enter"/>; disposing it more than once leaves the scope only once.
        /// </summary>
        private sealed class ScopeExit : IDisposable
        {
            /// <summary>
            /// The <see cref="ProjectScope"/> to leave, or <c>null</c> once it has been left.
            /// </summary>
            private ProjectScope scope;

            /// <summary>
            /// Initializes a new instance of the <see cref="ScopeExit"/> class.
            /// </summary>
            /// <param name="scope">The <see cref="ProjectScope"/> to leave on disposal.</param>
            public ScopeExit(ProjectScope scope)
            {
                this.scope = scope;
            }

            /// <summary>
            /// Leaves the scope, unless this handle has already done so.
            /// </summary>
            public void Dispose()
            {
                this.scope?.Leave();
                this.scope = null;
            }
        }
    }
}
