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
    /// API and keeps it in memory. It holds no element until <see cref="LoadModel"/> is called.
    /// </summary>
    public class InMemoryModelProvider : IModelProvider
    {
        /// <summary>
        /// All the elements of the loaded model.
        /// </summary>
        private List<IElement> elements = [];

        /// <summary>
        /// The elements of the loaded model that have no owner.
        /// </summary>
        private List<IElement> rootElements = [];

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
        public void LoadModel(Uri modelPath)
        {
            ArgumentNullException.ThrowIfNull(modelPath);

            if (!modelPath.IsAbsoluteUri || !modelPath.IsFile)
            {
                throw new ArgumentException("The model path must be an absolute file URI.", nameof(modelPath));
            }

            using var stream = File.OpenRead(modelPath.LocalPath);

            var dtos = new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();

            var assembler = new Assembler();
            assembler.Synchronize(dtos);

            var loadedElements = assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .ToList();

            this.rootElements = loadedElements
                .Where(element => element.OwningRelationship == null && element is not IRelationship { OwningRelatedElement: not null })
                .ToList();

            this.elements = loadedElements;
        }

        /// <summary>
        /// Gets all the elements of the model.
        /// </summary>
        /// <returns>A read-only list of every <see cref="IElement"/> in the model.</returns>
        public IReadOnlyList<IElement> GetElements()
        {
            return this.elements;
        }

        /// <summary>
        /// Gets the root elements of the model, that is the elements that have no owner.
        /// </summary>
        /// <returns>A read-only list of the root <see cref="IElement"/>s of the model.</returns>
        public IReadOnlyList<IElement> GetRootElements()
        {
            return this.rootElements;
        }
    }
}