// ------------------------------------------------------------------------------------------------
//  <copyright file="StandardLibraryHelper.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using IModelLibraryLoader = SysML2.NET.Serializer.Xmi.IModelLibraryLoader;

    /// <summary>
    /// Gives the tests a standard library: an empty one, for the tests of a model that does not use it, or a part of the real
    /// one, which the <see cref="StandardLibrary"/> reads through a mock of its <see cref="IModelLibraryLoader"/>: the
    /// elements of the library that the tests use, read once from <c>Data/StandardLibrary/StandardLibrarySubset.json</c>.
    /// </summary>
    internal static class StandardLibraryHelper
    {
        /// <summary>
        /// The <c>Id</c> of the <c>Refinement</c> metadata definition of <c>ModelingMetadata</c>.
        /// </summary>
        public static readonly Guid RefinementId = Guid.Parse("58f9c6ee-0dbe-530f-b7ee-800c71866e29");

        /// <summary>
        /// The <c>Id</c> of the <c>MassValue</c> attribute definition of <c>ISQBase</c>.
        /// </summary>
        public static readonly Guid MassValueId = Guid.Parse("9cd0e404-efee-50e5-a59b-681065bd188c");

        /// <summary>
        /// The part of the real standard library, loaded the first time it is used.
        /// </summary>
        private static readonly Lazy<IStandardLibrary> LoadedLibrary = new(LoadLibrary);

        /// <summary>
        /// The DTOs of the part of the real standard library, read the first time they are used.
        /// </summary>
        private static readonly Lazy<List<DtoElement>> LibraryDtos = new(ReadLibraryDtos);

        /// <summary>
        /// Gets a standard library that holds no element and has no problem.
        /// </summary>
        public static IStandardLibrary Empty
        {
            get
            {
                var library = new Mock<IStandardLibrary>();
                library.Setup(standardLibrary => standardLibrary.Elements).Returns(new Dictionary<Guid, IElement>());
                library.Setup(standardLibrary => standardLibrary.NamedElements).Returns(new Dictionary<Guid, IElement>());
                library.Setup(standardLibrary => standardLibrary.Problems).Returns([]);
                library.Setup(standardLibrary => standardLibrary.Units).Returns(LibraryUnits.None);

                return library.Object;
            }
        }

        /// <summary>
        /// Gets a standard library that could not be loaded: it holds no element and has one problem.
        /// </summary>
        public static IStandardLibrary Missing
        {
            get
            {
                var library = new Mock<IStandardLibrary>();
                library.Setup(standardLibrary => standardLibrary.Elements).Returns(new Dictionary<Guid, IElement>());
                library.Setup(standardLibrary => standardLibrary.NamedElements).Returns(new Dictionary<Guid, IElement>());
                library.Setup(standardLibrary => standardLibrary.Problems).Returns(["The standard library could not be loaded from 'StandardLibrary': the folder is missing."]);
                library.Setup(standardLibrary => standardLibrary.Units).Returns(LibraryUnits.None);

                return library.Object;
            }
        }

        /// <summary>
        /// Gets the part of the real standard library, shared by the tests, which must not modify it.
        /// </summary>
        public static IStandardLibrary Loaded => LoadedLibrary.Value;

        /// <summary>
        /// Gets the number of elements of the part of the real standard library.
        /// </summary>
        public static int LoadedElementCount => LibraryDtos.Value.Count;

        /// <summary>
        /// Gets the folder that holds the part of the real standard library.
        /// </summary>
        public static Uri LibraryDirectory => new(Path.Combine(AppContext.BaseDirectory, "Data", "StandardLibrary"));

        /// <summary>
        /// Creates the factory of the loggers of the reader of the library, as a mock that gives mock loggers.
        /// </summary>
        /// <returns>The mock of the <see cref="ILoggerFactory"/>.</returns>
        public static Mock<ILoggerFactory> CreateLoggerFactory()
        {
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

            return loggerFactory;
        }

        /// <summary>
        /// Loads the part of the real standard library: the reader of the library gives its root namespaces, built by
        /// SysML2.NET from the DTOs of the JSON file.
        /// </summary>
        /// <returns>The loaded library.</returns>
        private static IStandardLibrary LoadLibrary()
        {
            List<INamespace> roots = [.. ModelDtoHelper.Assemble(LibraryDtos.Value).OfType<INamespace>().Where(element => element.OwningRelationship == null)];

            var libraryLoader = new Mock<IModelLibraryLoader>();
            libraryLoader.Setup(loader => loader.Load(LibraryDirectory.LocalPath)).Returns(roots);

            var library = new StandardLibrary(libraryLoader.Object, LibraryDirectory);
            _ = library.Elements;

            libraryLoader.Verify(loader => loader.Load(LibraryDirectory.LocalPath), Times.Once);

            return library;
        }

        /// <summary>
        /// Reads the DTOs of the part of the real standard library.
        /// </summary>
        /// <returns>The DTOs.</returns>
        private static List<DtoElement> ReadLibraryDtos()
        {
            using var stream = File.OpenRead(Path.Combine(LibraryDirectory.LocalPath, "StandardLibrarySubset.json"));

            return new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();
        }
    }
}
