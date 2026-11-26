// ------------------------------------------------------------------------------------------------
//  <copyright file="IManagedValueProvider.cs" company="Starion Group S.A.">
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
    /// Provides the values the data access objects assign to the managed properties of the objects they store. It supplies no time: the database
    /// assigns the creation and update times.
    /// </summary>
    public interface IManagedValueProvider
    {
        /// <summary>
        /// Queries the identifier of the acting user of the current unit of work, assigned to the managed user references of a created or updated
        /// object.
        /// </summary>
        /// <returns>The identifier of the acting user, a stored <c>User</c>.</returns>
        /// <exception cref="InvalidOperationException">No acting user is known for the current unit of work.</exception>
        Guid QueryActingUserId();
    }
}
