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

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// The <see cref="IModelChangeApplier"/> that applies a batch of <see cref="ModelChange"/>s to a copy of a SysML v2 model
    /// made of DTOs, and describes the result as the change of a commit: the <c>DataVersion</c> records of the elements that
    /// the batch creates, updates and deletes.
    /// </summary>
    /// <remarks>
    /// The applier holds no state: each call works on its own copy of the model, through a <see cref="ModelChangeBatch"/>.
    /// A single instance can therefore be registered as a singleton, and the model it is given is never modified.
    /// </remarks>
    public class ModelChangeApplier : IModelChangeApplier
    {
        /// <summary>
        /// Applies a batch of changes, in order, to a copy of the given model, and returns the change of the commit that
        /// makes the same modifications. Every change is checked, so that all the problems of the batch are reported at once.
        /// </summary>
        /// <param name="model">The DTOs of the model to modify, which are left unchanged.</param>
        /// <param name="changes">The changes to apply.</param>
        /// <returns>
        /// The <see cref="PendingCommit"/>: the <c>DataVersion</c> records and the created elements, or the problems.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="model"/> or <paramref name="changes"/> is <c>null</c>.
        /// </exception>
        public PendingCommit Apply(IReadOnlyCollection<IElement> model, IReadOnlyList<ModelChange> changes)
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(changes);

            var batch = new ModelChangeBatch(CopyElements(model));
            var problems = batch.Apply(changes);

            return problems.Count == 0
                ? new PendingCommit(batch.CreateChange(), batch.CreatedElements, [])
                : new PendingCommit([], [], problems);
        }

        /// <summary>
        /// Copies DTOs by writing them to JSON and reading them back, so that a batch can modify the copy without touching
        /// the model.
        /// </summary>
        /// <param name="elements">The DTOs to copy.</param>
        /// <returns>The copied DTOs.</returns>
        private static List<IElement> CopyElements(IReadOnlyCollection<IElement> elements)
        {
            using var stream = new MemoryStream();

            new Serializer().Serialize(elements, SerializationModeKind.JSON, false, stream, new JsonWriterOptions());
            stream.Position = 0;

            return new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<IElement>()
                .ToList();
        }
    }
}
