// ------------------------------------------------------------------------------------------------
//  <copyright file="ManagedValueProvider.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Persistence
{
    using System;

    /// <summary>
    /// Holds the acting user of one scope, recorded by the caller of the unit of work before its first create or update, and provides it to the data
    /// access objects of that scope.
    /// </summary>
    public sealed class ManagedValueProvider : IManagedValueProvider
    {
        /// <summary>
        /// Gets or sets the identifier of the recorded acting user, or <see langword="null"/> while none has been recorded.
        /// </summary>
        private Guid? ActingUserId { get; set; }

        /// <summary>
        /// Records the acting user of the current unit of work, replacing any identifier recorded before.
        /// </summary>
        /// <param name="actingUserId">The identifier of the acting user, a stored <c>User</c>.</param>
        /// <exception cref="ArgumentException"><paramref name="actingUserId"/> is <see cref="Guid.Empty"/>.</exception>
        public void SetActingUserId(Guid actingUserId)
        {
            if (actingUserId == Guid.Empty)
            {
                throw new ArgumentException("The acting user identifier must not be empty.", nameof(actingUserId));
            }

            this.ActingUserId = actingUserId;
        }

        /// <summary>
        /// Queries the identifier of the acting user recorded for the current unit of work.
        /// </summary>
        /// <returns>The identifier recorded through <see cref="SetActingUserId"/>.</returns>
        /// <exception cref="InvalidOperationException">No acting user has been recorded for the current unit of work.</exception>
        public Guid QueryActingUserId()
        {
            return this.ActingUserId ?? throw new InvalidOperationException("No acting user has been recorded for the current unit of work.");
        }
    }
}
