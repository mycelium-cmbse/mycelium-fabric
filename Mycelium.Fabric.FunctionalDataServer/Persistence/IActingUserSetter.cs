// ------------------------------------------------------------------------------------------------
//  <copyright file="IActingUserSetter.cs" company="Starion Group S.A.">
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
    /// Records the acting user of the current unit of work, which the data access objects of the same scope assign to the managed user references
    /// of the objects they create or update.
    /// </summary>
    public interface IActingUserSetter
    {
        /// <summary>
        /// Records the acting user of the current unit of work, replacing any identifier recorded before.
        /// </summary>
        /// <param name="actingUserId">The identifier of the acting user, a stored <c>User</c>.</param>
        /// <exception cref="ArgumentException"><paramref name="actingUserId"/> is <see cref="Guid.Empty"/>.</exception>
        void SetActingUserId(Guid actingUserId);
    }
}
