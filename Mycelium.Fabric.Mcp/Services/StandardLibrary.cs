// ------------------------------------------------------------------------------------------------
//  <copyright file="StandardLibrary.cs" company="Starion Group S.A.">
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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Hosting;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Enumerations;
    using SysML2.NET.Serializer.Xmi;

    /// <summary>
    /// The SysML v2 standard library, loaded once from the XMI files of a folder by the <see cref="IModelLibraryLoader"/> of
    /// SysML2.NET, which resolves the references between the files. As an <see cref="IHostedService"/>, it starts loading
    /// in the background when the host starts, so that the server answers at once and the library is ready, or nearly, when
    /// a tool first needs it.
    /// </summary>
    public class StandardLibrary : IStandardLibrary, IHostedService
    {
        /// <summary>
        /// The separator of the segments of a qualified name.
        /// </summary>
        private const string NameSeparator = "::";

        /// <summary>
        /// The name of the package of the library that declares <see cref="MeasurementUnitName"/>.
        /// </summary>
        private const string MeasurementReferencesName = "MeasurementReferences";

        /// <summary>
        /// The name of the attribute definition that types every unit of the library, such as <c>MassUnit</c> does.
        /// </summary>
        private const string MeasurementUnitName = "MeasurementUnit";

        /// <summary>
        /// The <see cref="IModelLibraryLoader"/> that reads the XMI files of the library.
        /// </summary>
        private readonly IModelLibraryLoader libraryLoader;

        /// <summary>
        /// The loaded library. A thread that reads it while it is being loaded waits for the end of the loading.
        /// </summary>
        private readonly Lazy<LoadedLibrary> loadedLibrary;

        /// <summary>
        /// Initializes a new instance of the <see cref="StandardLibrary"/> class. The library is loaded by
        /// <see cref="StartAsync"/>, or by the first read of its elements.
        /// </summary>
        /// <param name="libraryLoader">The <see cref="IModelLibraryLoader"/> that reads the XMI files of the library.</param>
        /// <param name="libraryDirectory">The <see cref="Uri"/> of the folder that holds the XMI files of the library.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="libraryLoader"/> or <paramref name="libraryDirectory"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="libraryDirectory"/> is not an absolute file <see cref="Uri"/>.
        /// </exception>
        public StandardLibrary(IModelLibraryLoader libraryLoader, Uri libraryDirectory)
        {
            ArgumentNullException.ThrowIfNull(libraryLoader);
            ArgumentNullException.ThrowIfNull(libraryDirectory);

            if (!libraryDirectory.IsAbsoluteUri || !libraryDirectory.IsFile)
            {
                throw new ArgumentException("The library directory must be an absolute file URI.", nameof(libraryDirectory));
            }

            this.libraryLoader = libraryLoader;
            this.LibraryDirectory = libraryDirectory;
            this.loadedLibrary = new Lazy<LoadedLibrary>(this.Load);
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the folder that holds the XMI files of the library.
        /// </summary>
        public Uri LibraryDirectory { get; }

        /// <summary>
        /// Gets the elements of the library, indexed by their <c>Id</c>, which is the same in every copy of the library. It
        /// is empty when the library could not be loaded. Waits for the end of the loading.
        /// </summary>
        public IReadOnlyDictionary<Guid, IElement> Elements => this.loadedLibrary.Value.Elements;

        /// <summary>
        /// Gets the elements of the library that have a name or a short name, indexed by their <c>Id</c>: the only ones that a
        /// model can reference, by their name in the textual notation or by their <c>Id</c>, such as a definition, a feature
        /// or a unit. They are about one element of the library in twenty. Waits for the end of the loading.
        /// </summary>
        public IReadOnlyDictionary<Guid, IElement> NamedElements => this.loadedLibrary.Value.NamedElements;

        /// <summary>
        /// Gets the problems met while loading the library, such as a missing folder, or while reading its units. Waits for
        /// the end of the loading.
        /// </summary>
        public IReadOnlyList<string> Problems => this.loadedLibrary.Value.Problems;

        /// <summary>
        /// Gets the units that the library declares, such as <c>hp</c> or <c>lbf</c> of <c>USCustomaryUnits</c>: the
        /// attributes typed by a <c>MeasurementUnit</c>. The units that <see cref="UnitCatalog"/> knows keep the definition of
        /// the catalog. It holds no unit when the library or its units could not be read. Waits for the end of the loading.
        /// </summary>
        public LibraryUnits Units => this.loadedLibrary.Value.Units;

        /// <summary>
        /// Starts loading the library on a thread of the thread pool, unless it has already started, without waiting for the
        /// end of the loading: the host starts the next services, such as the transport of the MCP server, at once.
        /// </summary>
        /// <param name="cancellationToken">Signals that the start of the host has been aborted: the loading is then not started.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => this.loadedLibrary.Value, cancellationToken);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Does nothing: the library stays in memory until the server stops, and a loading that has started cannot be stopped.
        /// </summary>
        /// <param name="cancellationToken">Not used.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the element of the library that a qualified name designates, such as <c>ISQ::MassValue</c> or <c>SI::kg</c>.
        /// The first segment designates a package of the library, which resolves the rest of the name with the name
        /// resolution of SysML2.NET, through short names and public imports: <c>ISQ::MassValue</c> designates
        /// <c>ISQBase::MassValue</c>, which <c>ISQ</c> imports. Waits for the end of the loading.
        /// </summary>
        /// <param name="qualifiedName">The qualified name, which starts with the name of a package of the library.</param>
        /// <returns>The designated element, or <c>null</c> when the library has no such element.</returns>
        public IElement Resolve(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName))
            {
                return null;
            }

            var separatorIndex = qualifiedName.IndexOf(NameSeparator, StringComparison.Ordinal);
            var packageName = separatorIndex < 0 ? qualifiedName : qualifiedName[..separatorIndex];

            if (!this.loadedLibrary.Value.Packages.TryGetValue(packageName, out var package))
            {
                return null;
            }

            if (separatorIndex < 0)
            {
                return package;
            }

            var memberName = qualifiedName[(separatorIndex + NameSeparator.Length)..];

            return string.IsNullOrWhiteSpace(memberName) ? null : package.Resolve(memberName)?.MemberElement;
        }

        /// <summary>
        /// Gets an element and all the elements that it owns, directly or not, with the relationships through which it owns
        /// them, indexed by their <c>Id</c>.
        /// </summary>
        /// <param name="roots">The root namespaces of the library.</param>
        /// <returns>The elements of the library.</returns>
        private static Dictionary<Guid, IElement> IndexElements(IEnumerable<INamespace> roots)
        {
            var elementsById = new Dictionary<Guid, IElement>();
            var elementsToVisit = new Stack<IElement>(roots);

            while (elementsToVisit.Count > 0)
            {
                var element = elementsToVisit.Pop();

                if (!elementsById.TryAdd(element.Id, element))
                {
                    continue;
                }

                foreach (var ownedRelationship in element.OwnedRelationship)
                {
                    elementsToVisit.Push(ownedRelationship);
                }

                if (element is IRelationship relationship)
                {
                    foreach (var ownedRelatedElement in relationship.OwnedRelatedElement)
                    {
                        elementsToVisit.Push(ownedRelatedElement);
                    }
                }
            }

            return elementsById;
        }

        /// <summary>
        /// Loads the library. A library that cannot be read is left empty, with the reason in its problems, so that the
        /// server still works on a model that does not use the library. A library whose units cannot be read keeps its
        /// elements, without unit.
        /// </summary>
        /// <returns>The <see cref="LoadedLibrary"/>.</returns>
        private LoadedLibrary Load()
        {
            var directory = this.LibraryDirectory.LocalPath;
            Dictionary<Guid, IElement> elements;
            Dictionary<Guid, IElement> namedElements;
            Dictionary<string, INamespace> packages;

            try
            {
                var roots = this.libraryLoader.Load(directory);
                elements = IndexElements(roots);
                namedElements = elements.Values.Where(element => element.DeclaredName != null || element.DeclaredShortName != null).ToDictionary(element => element.Id);

                // The packages of the library are the named members of the root namespaces, one per file.
                packages = roots
                    .SelectMany(root => root.ownedMember.OfType<INamespace>())
                    .Where(package => package.DeclaredName != null)
                    .DistinctBy(package => package.DeclaredName)
                    .ToDictionary(package => package.DeclaredName);
            }
            catch (Exception exception)
            {
                // Any failure of the reader, whatever its type, leaves the server running without the library.
                return new LoadedLibrary(new Dictionary<Guid, IElement>(), new Dictionary<Guid, IElement>(), new Dictionary<string, INamespace>(), LibraryUnits.None,
                    [$"The standard library could not be loaded from '{directory}': {exception.Message}"]);
            }

            try
            {
                return new LoadedLibrary(elements, namedElements, packages, ReadUnits(namedElements.Values, packages), []);
            }
            catch (Exception exception)
            {
                // The derived properties that SysML2.NET computes on the library can fail: the library stays usable without its units.
                return new LoadedLibrary(elements, namedElements, packages, LibraryUnits.None, [$"The units of the standard library could not be read: {exception.Message}"]);
            }
        }

        /// <summary>
        /// Reads the units that the library declares: the named attributes of its packages that are typed by a
        /// <c>MeasurementUnit</c> of <c>MeasurementReferences</c>, which leaves out the scales, such as <c>'°C_abs'</c>, and
        /// the quantities. The enumeration values are left out first: SysML2.NET cannot compute the type of some of them.
        /// </summary>
        /// <param name="namedElements">The elements of the library that have a name or a short name.</param>
        /// <param name="packages">The packages of the library, indexed by their name.</param>
        /// <returns>The <see cref="LibraryUnits"/>, without unit when the library has no <c>MeasurementUnit</c>.</returns>
        private static LibraryUnits ReadUnits(IEnumerable<IElement> namedElements, IReadOnlyDictionary<string, INamespace> packages)
        {
            if (packages.GetValueOrDefault(MeasurementReferencesName)?.Resolve(MeasurementUnitName)?.MemberElement is not IType measurementUnit)
            {
                return LibraryUnits.None;
            }

            var unitDeclarations = namedElements
                .OfType<IAttributeUsage>()
                .Where(attribute => attribute is not IEnumerationUsage && attribute.owningNamespace is ILibraryPackage)
                .Where(attribute => (attribute.type ?? []).Any(type => type.Specializes(measurementUnit)));

            return LibraryUnits.Read(unitDeclarations);
        }

        /// <summary>
        /// The elements of a loaded library and the problems met while loading it.
        /// </summary>
        /// <param name="Elements">The elements of the library, indexed by their <c>Id</c>.</param>
        /// <param name="NamedElements">The elements of the library that have a name or a short name, indexed by their <c>Id</c>.</param>
        /// <param name="Packages">The packages of the library, such as <c>ISQ</c> or <c>ModelingMetadata</c>, indexed by their name.</param>
        /// <param name="Units">The units that the library declares.</param>
        /// <param name="Problems">The problems met while loading the library or reading its units.</param>
        private sealed record LoadedLibrary(IReadOnlyDictionary<Guid, IElement> Elements, IReadOnlyDictionary<Guid, IElement> NamedElements, IReadOnlyDictionary<string, INamespace> Packages,
            LibraryUnits Units, IReadOnlyList<string> Problems);
    }
}
