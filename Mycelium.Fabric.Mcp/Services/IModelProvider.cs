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
    /// The read operations mirror the <c>ElementNavigationService</c> of the Systems Modeling API and Services 1.0
    /// (§7.2.2): <c>getElements</c> and <c>getRootElements</c>. The project and commit scoping of the
    /// specification is left out for now, since a provider serves a single model at a time.
    /// </remarks>
    public interface IModelProvider
    {
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
        /// Gets all the elements of the model.
        /// </summary>
        /// <returns>A read-only list of every <see cref="IElement"/> in the model.</returns>
        IReadOnlyList<IElement> GetElements();

        /// <summary>
        /// Gets the root elements of the model, that is the elements that have no owner.
        /// </summary>
        /// <returns>A read-only list of the root <see cref="IElement"/>s of the model.</returns>
        IReadOnlyList<IElement> GetRootElements();
    }
}