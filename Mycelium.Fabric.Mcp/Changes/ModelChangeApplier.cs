// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeApplier.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    using ErrorOr;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    using Error = ErrorOr.Error;

    /// <summary>
    /// The <see cref="IModelChangeApplier"/> that applies a batch of <see cref="ModelChange"/>s to a copy of a SysML v2 model
    /// made of DTOs, and describes the result as a <see cref="CommitRequest"/>: the <c>DataVersionRequest</c> records of the
    /// elements that the batch creates, updates and deletes.
    /// </summary>
    /// <remarks>
    /// The applier holds no state: each call works on its own copy of the model, through a <see cref="ModelChangeBatch"/>.
    /// A single instance can therefore be registered as a singleton, and the model it is given is never modified.
    /// </remarks>
    public class ModelChangeApplier : IModelChangeApplier
    {
        /// <summary>
        /// The options of the JSON writer, shared by every copy: the default ones, since the copy is read back at once.
        /// </summary>
        private static readonly JsonWriterOptions WriterOptions = new();

        /// <summary>
        /// The <see cref="ISerializer"/> that writes the DTOs to copy.
        /// </summary>
        private readonly ISerializer serializer;

        /// <summary>
        /// The <see cref="IDeSerializer"/> that reads the copied DTOs back.
        /// </summary>
        private readonly IDeSerializer deSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelChangeApplier"/> class.
        /// </summary>
        /// <param name="serializer">The <see cref="ISerializer"/> that writes the DTOs to copy.</param>
        /// <param name="deSerializer">The <see cref="IDeSerializer"/> that reads the copied DTOs back.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serializer"/> or <paramref name="deSerializer"/> is <c>null</c>.
        /// </exception>
        public ModelChangeApplier(ISerializer serializer, IDeSerializer deSerializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(deSerializer);

            this.serializer = serializer;
            this.deSerializer = deSerializer;
        }

        /// <summary>
        /// Applies a batch of changes, in order, to a copy of the given model, and returns the <see cref="CommitRequest"/>
        /// that makes the same modifications. Every change is checked, so that all the problems of the batch are reported at
        /// once.
        /// </summary>
        /// <param name="model">The DTOs of the model to modify, which are left unchanged.</param>
        /// <param name="changes">The changes to apply.</param>
        /// <returns>
        /// The <see cref="CommitRequest"/>, or a validation <see cref="Error"/> for each problem of the batch.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="model"/> or <paramref name="changes"/> is <c>null</c>.
        /// </exception>
        public ErrorOr<CommitRequest> Apply(IReadOnlyCollection<IElement> model, IReadOnlyList<ModelChange> changes)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(changes);

            var batch = new ModelChangeBatch(this.CopyElements(model));
            var problems = batch.Apply(changes);

            if (problems.Count > 0)
            {
                return problems.Select(problem => Error.Validation(description: problem)).ToList();
            }

            return batch.CreateCommitRequest();
        }

        /// <summary>
        /// Copies DTOs by writing them to JSON and reading them back, so that a batch can modify the copy without touching
        /// the model.
        /// </summary>
        /// <param name="elements">The DTOs to copy.</param>
        /// <returns>The copied DTOs.</returns>
        private List<IElement> CopyElements(IReadOnlyCollection<IElement> elements)
        {
            using var stream = new MemoryStream();

            this.serializer.Serialize(elements, SerializationModeKind.JSON, false, stream, WriterOptions);
            stream.Position = 0;

            return this.deSerializer
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<IElement>()
                .ToList();
        }
    }
}
