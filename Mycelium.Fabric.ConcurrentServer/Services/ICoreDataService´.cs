// ------------------------------------------------------------------------------------------------
//  <copyright file="ICoreDataService´.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Identifies the <see cref="ICoreDataService"/> of one concrete SysML v2 or KerML metaclass, as the <see cref="ICoreDataServiceProvider"/> resolves it.
    /// </summary>
    /// <typeparam name="TElement">The type of element the service handles.</typeparam>
    public interface ICoreDataService<TElement> : ICoreDataService where TElement : class, IElement
    {
    }
}
