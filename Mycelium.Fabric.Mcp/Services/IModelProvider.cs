// ------------------------------------------------------------------------------------------------
//  <copyright file="IModelProvider.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System;
    using System.Collections.Generic;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Provides read access to the SysML v2 model that the MCP tools work on.
    /// </summary>
    /// <remarks>
    /// The read members mirror the <c>ElementNavigationService</c> of the Systems Modeling API and Services 1.0
    /// (§7.2.2): <see cref="Elements"/> for <c>getElements</c>, <see cref="RootElements"/> for
    /// <c>getRootElements</c> and <see cref="GetElementById"/> for <c>getElementById</c>. The project and commit
    /// scoping of the specification is left out for now, since a provider serves a single model at a time.
    /// </remarks>
    public interface IModelProvider
    {
        /// <summary>
        /// Gets all the elements of the loaded model.
        /// </summary>
        IReadOnlyList<IElement> Elements { get; }

        /// <summary>
        /// Gets the root elements of the loaded model, that is the elements that have no owner.
        /// </summary>
        IReadOnlyList<IElement> RootElements { get; }

        /// <summary>
        /// Loads the model stored at the given location, replacing the model loaded before.
        /// </summary>
        /// <param name="modelPath">The <see cref="Uri"/> of the JSON file that contains the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelPath"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="modelPath"/> is not an absolute file <see cref="Uri"/>.
        /// </exception>
        void LoadModel(Uri modelPath);

        /// <summary>
        /// Gets the element of the loaded model that has the given identifier.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>
        /// The <see cref="IElement"/> that has the given identifier, or <c>null</c> when the loaded model contains no
        /// such element.
        /// </returns>
        IElement GetElementById(Guid elementId);
    }
}