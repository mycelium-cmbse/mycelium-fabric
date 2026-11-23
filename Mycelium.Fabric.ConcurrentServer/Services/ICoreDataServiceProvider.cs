// ------------------------------------------------------------------------------------------------
//  <copyright file="ICoreDataServiceProvider.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;

    /// <summary>
    /// Provides the core-data service of a concrete SysML v2 or KerML data transfer object type.
    /// </summary>
    public interface ICoreDataServiceProvider
    {
        /// <summary>
        /// Resolves the core-data service of an exact data transfer object type.
        /// </summary>
        /// <param name="dataType">The exact runtime type of the element.</param>
        /// <returns>The core-data service of <paramref name="dataType"/>.</returns>
        ICoreDataService Resolve(Type dataType);
    }
}
