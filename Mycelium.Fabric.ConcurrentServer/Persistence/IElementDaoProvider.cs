// ------------------------------------------------------------------------------------------------
//  <copyright file="IElementDaoProvider.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;

    using SysML2.NET.Core;
    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Provides the <see cref="IElementDao"/> of a metaclass, by its stored kind or by its data transfer object type.
    /// </summary>
    public interface IElementDaoProvider
    {
        /// <summary>
        /// Resolves the data access object of the metaclass a <c>class_kind</c> stands for.
        /// </summary>
        /// <param name="classKind">The <see cref="SysML2.NET.Core.ClassKind"/> of the metaclass.</param>
        /// <returns>The <see cref="IElementDao"/> of the metaclass.</returns>
        /// <exception cref="ArgumentException"><paramref name="classKind"/> is not a concrete SysML v2 or KerML metaclass.</exception>
        IElementDao Resolve(ClassKind classKind);

        /// <summary>
        /// Resolves the data access object of the metaclass of an exact data transfer object type.
        /// </summary>
        /// <param name="dataType">The exact runtime type of the element.</param>
        /// <returns>The <see cref="IElementDao"/> of the metaclass.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="dataType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="dataType"/> is not a concrete SysML v2 or KerML data transfer object type.</exception>
        IElementDao Resolve(Type dataType);

        /// <summary>
        /// Resolves the data access object of the metaclass of an exact data transfer object type, typed by it.
        /// </summary>
        /// <typeparam name="TElement">The exact data transfer object type of the metaclass.</typeparam>
        /// <returns>The <see cref="IElementDao{TElement}"/> of the metaclass.</returns>
        /// <exception cref="ArgumentException"><typeparamref name="TElement"/> is not a concrete SysML v2 or KerML data transfer object type.</exception>
        IElementDao<TElement> Resolve<TElement>() where TElement : class, IElement;
    }
}
