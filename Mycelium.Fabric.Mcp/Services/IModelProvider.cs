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
    using System.Collections.Generic;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Provides read access to the SysML v2 model that the MCP tools work on.
    /// </summary>
    /// <remarks>
    /// The operations mirror the <c>ElementNavigationService</c> of the Systems Modeling API and Services 1.0
    /// (§7.2.2): <c>getElements</c> and <c>getRootElements</c>. The project and commit scoping of the
    /// specification is left out for now, since a provider serves a single model.
    /// </remarks>
    public interface IModelProvider
    {
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