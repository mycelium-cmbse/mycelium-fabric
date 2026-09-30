// ------------------------------------------------------------------------------------------------
//  <copyright file="InMemoryModelProvider.cs" company="Starion Group S.A.">
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
    using System.IO;
    using System.Linq;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// An <see cref="IModelProvider"/> that loads a SysML v2 model from a JSON file of the Systems Modeling
    /// API and keeps it in memory.
    /// </summary>
    public class InMemoryModelProvider : IModelProvider
    {
        /// <summary>
        /// All the elements of the loaded model.
        /// </summary>
        private readonly List<IElement> elements;

        /// <summary>
        /// The elements of the loaded model that have no owner.
        /// </summary>
        private readonly List<IElement> rootElements;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryModelProvider"/> class by loading the model
        /// stored in the given JSON file.
        /// </summary>
        /// <param name="filePath">The path of the JSON file that contains the model.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="filePath"/> is <c>null</c>, empty or white space.
        /// </exception>
        public InMemoryModelProvider(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            using var stream = File.OpenRead(filePath);

            var dtos = new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();

            var assembler = new Assembler();
            assembler.Synchronize(dtos);

            this.elements = assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .ToList();

            this.rootElements = this.elements
                .Where(element => element.OwningRelationship == null && element is not IRelationship { OwningRelatedElement: not null })
                .ToList();
        }

        /// <inheritdoc />
        public IReadOnlyList<IElement> GetElements()
        {
            return this.elements;
        }

        /// <inheritdoc />
        public IReadOnlyList<IElement> GetRootElements()
        {
            return this.rootElements;
        }
    }
}