// ------------------------------------------------------------------------------------------------
//  <copyright file="IDaoProvider.cs" company="Starion Group S.A.">
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
    /// Provides the data access object of a concrete class from its class kind, the modeled class name stored in the <c>class_kind</c> column.
    /// </summary>
    public interface IDaoProvider
    {
        /// <summary>
        /// Resolves the data access object that stores the objects of a class kind.
        /// </summary>
        /// <param name="classKind">The class kind read from the <c>class_kind</c> column: the modeled name of a concrete class.</param>
        /// <returns>The data access object of <paramref name="classKind"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="classKind"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="classKind"/> is not the name of a concrete class.</exception>
        IBaseDao Resolve(string classKind);
    }
}
