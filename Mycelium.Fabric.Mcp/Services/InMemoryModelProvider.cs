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
    using System.Text.Json;
    using System.Threading;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Changes;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// An <see cref="IModelProvider"/> that loads a SysML v2 model from a JSON file of the Systems Modeling
    /// API and keeps it in memory. It holds no element until <see cref="LoadModel"/> is called.
    /// </summary>
    /// <remarks>
    /// The model is kept twice: as DTOs, which hold identifiers and can be copied and modified, and as the POCOs that
    /// SysML2.NET builds from them, which the tools navigate. A batch of changes is applied to a copy of the DTOs, from which
    /// new POCOs replace the current ones only when the whole batch succeeds.
    /// </remarks>
    public class InMemoryModelProvider : IModelProvider
    {
        /// <summary>
        /// The lock that prevents two batches of changes, or a batch and a load, from modifying the model at the same time.
        /// </summary>
        private readonly Lock modelLock = new();

        /// <summary>
        /// The DTOs of the loaded model, from which its elements are built.
        /// </summary>
        private List<DtoElement> elementDtos = [];

        /// <summary>
        /// The elements of the loaded model, indexed by their <c>Id</c>.
        /// </summary>
        private Dictionary<Guid, IElement> elementsById = [];

        /// <summary>
        /// Gets all the elements of the loaded model.
        /// </summary>
        public IReadOnlyList<IElement> Elements { get; private set; } = [];

        /// <summary>
        /// Gets the root elements of the loaded model, that is the elements that have no owner.
        /// </summary>
        public IReadOnlyList<IElement> RootElements { get; private set; } = [];

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

            var dtos = ReadElementDtos(stream);

            lock (this.modelLock)
            {
                this.SetModel(dtos);
            }
        }

        /// <summary>
        /// Applies a batch of changes to the loaded model, all or nothing: when one change is invalid, the model is left
        /// unchanged and the problems are returned.
        /// </summary>
        /// <param name="changes">The changes to apply, in order.</param>
        /// <returns>The <see cref="ApplyChangesResult"/> that tells whether the batch has been applied.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="changes"/> is <c>null</c>.
        /// </exception>
        public ApplyChangesResult ApplyChanges(IReadOnlyList<ModelChange> changes)
        {
            ArgumentNullException.ThrowIfNull(changes);

            lock (this.modelLock)
            {
                var applier = new ModelChangeApplier(this.CopyElementDtos());
                var result = applier.Apply(changes);

                if (result.Applied)
                {
                    this.SetModel([.. applier.Elements]);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the element of the loaded model that has the given identifier.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>
        /// The <see cref="IElement"/> that has the given identifier, or <c>null</c> when the loaded model contains no
        /// such element.
        /// </returns>
        public IElement GetElementById(Guid elementId)
        {
            return this.elementsById.GetValueOrDefault(elementId);
        }

        /// <summary>
        /// Gets the element of the loaded model that has the given identifier, or fails with a message the AI assistant can
        /// act on.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>The <see cref="IElement"/> that has the given identifier.</returns>
        /// <exception cref="McpException">
        /// Thrown when the loaded model contains no such element. Unlike other exceptions, the message of an
        /// <see cref="McpException"/> is sent back to the AI assistant.
        /// </exception>
        public IElement GetRequiredElementById(Guid elementId)
        {
            return this.GetElementById(elementId)
                ?? throw new McpException($"No element has the identifier '{elementId}'. Use find_elements_by_name or list_children to get a valid identifier.");
        }

        /// <summary>
        /// Reads the DTOs of the elements stored in a JSON stream of the Systems Modeling API.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> that contains the JSON.</param>
        /// <returns>The DTOs of the elements.</returns>
        private static List<DtoElement> ReadElementDtos(Stream stream)
        {
            return new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();
        }

        /// <summary>
        /// Copies the DTOs of the loaded model, by writing them to JSON and reading them back, so that a batch of changes can
        /// modify the copy without touching the loaded model.
        /// </summary>
        /// <returns>The copied DTOs.</returns>
        private List<DtoElement> CopyElementDtos()
        {
            using var stream = new MemoryStream();

            new Serializer().Serialize(this.elementDtos, SerializationModeKind.JSON, false, stream, new JsonWriterOptions());
            stream.Position = 0;

            return ReadElementDtos(stream);
        }

        /// <summary>
        /// Replaces the loaded model by the one made of the given DTOs, whose POCOs are built by SysML2.NET.
        /// </summary>
        /// <param name="dtos">The DTOs of the new model.</param>
        private void SetModel(List<DtoElement> dtos)
        {
            var assembler = new Assembler();
            assembler.Synchronize(dtos);

            var loadedElements = assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .ToList();

            var loadedRootElements = loadedElements
                .Where(element => element.OwningRelationship == null && element is not IRelationship { OwningRelatedElement: not null })
                .ToList();

            var loadedElementsById = loadedElements.ToDictionary(element => element.Id);

            this.elementDtos = dtos;
            this.Elements = loadedElements;
            this.RootElements = loadedRootElements;
            this.elementsById = loadedElementsById;
        }
    }
}