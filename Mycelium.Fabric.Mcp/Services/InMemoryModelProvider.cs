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
    using System.Threading;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Changes;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Dal;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;

    /// <summary>
    /// An <see cref="IModelProvider"/> that loads a SysML v2 model from a JSON file of the Systems Modeling
    /// API and keeps it in memory. It holds no element until <see cref="LoadModel"/> is called.
    /// </summary>
    /// <remarks>
    /// The model is kept twice: as DTOs, which hold identifiers and are the payloads of the <see cref="DataVersionRequest"/>
    /// records of a <see cref="CommitRequest"/>, and as the POCOs that SysML2.NET builds from them, which the tools
    /// navigate. A commit replaces the DTOs it changes, then new POCOs replace the current ones. Only the identifier of the
    /// last commit is kept: the history of the commits is left to the persistence of the Fabric server.
    /// </remarks>
    public class InMemoryModelProvider : IModelProvider
    {
        /// <summary>
        /// The <see cref="IModelChangeApplier"/> that turns a batch of changes into the change of a commit.
        /// </summary>
        private readonly IModelChangeApplier changeApplier;

        /// <summary>
        /// The lock that prevents two commits, or a commit and a load, from modifying the model at the same time.
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
        /// The <c>Id</c> of the last commit, or <see cref="Guid.Empty"/> when no commit has been created since the model
        /// was loaded.
        /// </summary>
        private Guid headCommitId = Guid.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryModelProvider"/> class.
        /// </summary>
        /// <param name="changeApplier">The <see cref="IModelChangeApplier"/> that turns a batch of changes into a commit.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="changeApplier"/> is <c>null</c>.
        /// </exception>
        public InMemoryModelProvider(IModelChangeApplier changeApplier)
        {
            ArgumentNullException.ThrowIfNull(changeApplier);

            this.changeApplier = changeApplier;
        }

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
                this.headCommitId = Guid.Empty;
            }
        }

        /// <summary>
        /// Applies a batch of changes to the loaded model, all or nothing: the batch is turned into the change of a commit by
        /// the <see cref="IModelChangeApplier"/>, then committed. When one change is invalid, the model is left unchanged and
        /// the problems are returned.
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
                var pendingCommit = this.changeApplier.Apply(this.elementDtos, changes);

                if (pendingCommit.Problems.Count > 0)
                {
                    return new ApplyChangesResult(false, [], pendingCommit.Problems);
                }

                this.ApplyCommit(pendingCommit.CommitRequest);

                return new ApplyChangesResult(true, pendingCommit.CreatedElements, []);
            }
        }

        /// <summary>
        /// Creates a commit from the given request: each <see cref="DataVersionRequest"/> of its change with a payload adds or
        /// replaces the element that has its identity, and each one without payload removes it. The provider keeps the
        /// payloads as the DTOs of the model.
        /// </summary>
        /// <param name="commitRequest">The <see cref="CommitRequest"/> that describes the commit.</param>
        /// <returns>The created <see cref="Commit"/>, whose previous commit is the one created before it.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="commitRequest"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a <see cref="DataVersionRequest"/> has no identity, or a payload that is not the element of its
        /// identity. The model is then left unchanged.
        /// </exception>
        public Commit CreateCommit(CommitRequest commitRequest)
        {
            ArgumentNullException.ThrowIfNull(commitRequest);

            lock (this.modelLock)
            {
                return this.ApplyCommit(commitRequest);
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
        /// Tells whether a <see cref="DataVersionRequest"/> can be committed: it has an identity, and either no payload or
        /// the element that has this identity as payload.
        /// </summary>
        /// <param name="dataVersion">The <see cref="DataVersionRequest"/> to check.</param>
        /// <returns><c>true</c> when the <see cref="DataVersionRequest"/> can be committed.</returns>
        private static bool IsValid(DataVersionRequest dataVersion)
        {
            return dataVersion?.Identity != null && dataVersion.Payload switch
            {
                null => true,
                DtoElement element => element.Id == dataVersion.Identity.Id,
                _ => false
            };
        }

        /// <summary>
        /// Applies a <see cref="CommitRequest"/> to the loaded model. The caller holds the lock.
        /// </summary>
        /// <param name="commitRequest">The <see cref="CommitRequest"/> that describes the commit.</param>
        /// <returns>The created <see cref="Commit"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the request has no change or a <see cref="DataVersionRequest"/> cannot be committed, before any
        /// modification.
        /// </exception>
        private Commit ApplyCommit(CommitRequest commitRequest)
        {
            if (commitRequest.Change == null || !commitRequest.Change.TrueForAll(IsValid))
            {
                throw new ArgumentException("Each data version must have an identity, and either no payload or the element that has this identity as payload.", nameof(commitRequest));
            }

            var commit = new Commit
            {
                Id = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                Name = commitRequest.Name,
                Description = commitRequest.Description,
                PreviousCommit = this.headCommitId == Guid.Empty ? [] : [this.headCommitId]
            };

            var dtosById = this.elementDtos.ToDictionary(dto => dto.Id);

            foreach (var dataVersion in commitRequest.Change)
            {
                if (dataVersion.Payload is DtoElement element)
                {
                    dtosById[element.Id] = element;
                }
                else
                {
                    dtosById.Remove(dataVersion.Identity.Id);
                }
            }

            this.SetModel([.. dtosById.Values]);
            this.headCommitId = commit.Id;

            return commit;
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
