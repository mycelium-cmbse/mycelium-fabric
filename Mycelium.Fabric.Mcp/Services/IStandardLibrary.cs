// ------------------------------------------------------------------------------------------------
//  <copyright file="IStandardLibrary.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// The SysML v2 standard library, read-only, which the elements of a model can use, such as the <c>Refinement</c> metadata
    /// of <c>ModelingMetadata</c> or the quantities of <c>ISQ</c>. It is not part of the model: the tools neither list nor
    /// export it.
    /// </summary>
    /// <remarks>
    /// Loading the library takes several seconds: the <see cref="StandardLibrary"/> starts it in the background when the host
    /// starts, and the members that read the library wait for it to end.
    /// </remarks>
    public interface IStandardLibrary
    {
        /// <summary>
        /// Gets the elements of the library, indexed by their <c>Id</c>, which is the same in every copy of the library. It
        /// is empty when the library could not be loaded. Waits for the end of the loading.
        /// </summary>
        IReadOnlyDictionary<Guid, IElement> Elements { get; }

        /// <summary>
        /// Gets the elements of the library that have a name or a short name, indexed by their <c>Id</c>: the only ones that a
        /// model can reference, by their name in the textual notation or by their <c>Id</c>, such as a definition, a feature
        /// or a unit. They are about one element of the library in twenty. Waits for the end of the loading.
        /// </summary>
        IReadOnlyDictionary<Guid, IElement> NamedElements { get; }

        /// <summary>
        /// Gets the problems met while loading the library, such as a missing folder, or while reading its units. Waits for
        /// the end of the loading.
        /// </summary>
        IReadOnlyList<string> Problems { get; }

        /// <summary>
        /// Gets the units that the library declares, such as <c>hp</c> or <c>lbf</c> of <c>USCustomaryUnits</c>: the
        /// attributes typed by a <c>MeasurementUnit</c>. The units that <see cref="UnitCatalog"/> knows keep the definition of
        /// the catalog. It holds no unit when the library or its units could not be read. Waits for the end of the loading.
        /// </summary>
        LibraryUnits Units { get; }

        /// <summary>
        /// Gets the element of the library that a qualified name designates, such as <c>ISQ::MassValue</c> or <c>SI::kg</c>.
        /// The name is resolved as SysML resolves it, through short names and public imports: <c>ISQ::MassValue</c> designates
        /// <c>ISQBase::MassValue</c>, which <c>ISQ</c> imports. Waits for the end of the loading.
        /// </summary>
        /// <param name="qualifiedName">The qualified name, which starts with the name of a package of the library.</param>
        /// <returns>The designated element, or <c>null</c> when the library has no such element.</returns>
        IElement Resolve(string qualifiedName);
    }
}
