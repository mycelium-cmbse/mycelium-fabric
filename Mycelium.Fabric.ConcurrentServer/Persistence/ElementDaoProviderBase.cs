// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementDaoProviderBase.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Extensions.DependencyInjection;

    using SysML2.NET.Core;
    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Implements the <see cref="IElementDaoProvider"/> over the scoped data access objects that a generated subclass maps, one per metaclass.
    /// </summary>
    /// <remarks>
    /// The data access objects are registered with a scoped lifetime, so the provider is scoped as well and resolves them from its own scope.
    /// </remarks>
    public abstract class ElementDaoProviderBase : IElementDaoProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ElementDaoProviderBase"/> class.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> of the current scope, from which the data access objects are resolved.</param>
        /// <param name="daoResolutions">The resolution of the data access object of each concrete data transfer object type.</param>
        /// <param name="dataTypes">The data transfer object type of each concrete metaclass kind.</param>
        /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/>, <paramref name="daoResolutions"/> or <paramref name="dataTypes"/> is <c>null</c>.</exception>
        protected ElementDaoProviderBase(IServiceProvider serviceProvider, IReadOnlyDictionary<Type, Func<IServiceProvider, IElementDao>> daoResolutions,
            IReadOnlyDictionary<ClassKind, Type> dataTypes)
        {
            this.ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.DaoResolutions = daoResolutions ?? throw new ArgumentNullException(nameof(daoResolutions));
            this.DataTypes = dataTypes ?? throw new ArgumentNullException(nameof(dataTypes));
        }

        /// <summary>
        /// Gets the resolution of the data access object of each concrete data transfer object type.
        /// </summary>
        private IReadOnlyDictionary<Type, Func<IServiceProvider, IElementDao>> DaoResolutions { get; }

        /// <summary>
        /// Gets the data transfer object type of each concrete metaclass kind.
        /// </summary>
        private IReadOnlyDictionary<ClassKind, Type> DataTypes { get; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> of the current scope.
        /// </summary>
        private IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Resolves the data access object of the metaclass a <c>class_kind</c> stands for.
        /// </summary>
        /// <param name="classKind">The <see cref="SysML2.NET.Core.ClassKind"/> of the metaclass.</param>
        /// <returns>The <see cref="IElementDao"/> of the metaclass.</returns>
        /// <exception cref="ArgumentException"><paramref name="classKind"/> is not a concrete SysML v2 or KerML metaclass.</exception>
        public IElementDao Resolve(ClassKind classKind) =>
            this.DataTypes.TryGetValue(classKind, out var dataType)
                ? this.Resolve(dataType)
                : throw new ArgumentException($"No element DAO stores the class kind {classKind}.", nameof(classKind));

        /// <summary>
        /// Resolves the data access object of the metaclass of an exact data transfer object type.
        /// </summary>
        /// <param name="dataType">The exact runtime type of the element.</param>
        /// <returns>The <see cref="IElementDao"/> of the metaclass.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="dataType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="dataType"/> is not a concrete SysML v2 or KerML data transfer object type.</exception>
        public IElementDao Resolve(Type dataType)
        {
            ArgumentNullException.ThrowIfNull(dataType);

            return this.DaoResolutions.TryGetValue(dataType, out var resolve)
                ? resolve(this.ServiceProvider)
                : throw new ArgumentException($"No element DAO stores {dataType.FullName}.", nameof(dataType));
        }

        /// <summary>
        /// Resolves the data access object of the metaclass of an exact data transfer object type, typed by it.
        /// </summary>
        /// <typeparam name="TElement">The exact data transfer object type of the metaclass.</typeparam>
        /// <returns>The <see cref="IElementDao{TElement}"/> of the metaclass.</returns>
        /// <exception cref="ArgumentException"><typeparamref name="TElement"/> is not a concrete SysML v2 or KerML data transfer object type.</exception>
        public IElementDao<TElement> Resolve<TElement>() where TElement : class, IElement =>
            this.ServiceProvider.GetService<IElementDao<TElement>>()
            ?? throw new ArgumentException($"No element DAO stores {typeof(TElement).FullName}.", nameof(TElement));
    }
}
