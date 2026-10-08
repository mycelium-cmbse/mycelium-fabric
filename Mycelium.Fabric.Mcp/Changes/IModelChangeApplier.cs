// ------------------------------------------------------------------------------------------------
//  <copyright file="IModelChangeApplier.cs" company="Starion Group S.A.">
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

    using ErrorOr;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PSM.DTO;

    using Error = ErrorOr.Error;

    /// <summary>
    /// Turns a batch of <see cref="ModelChange"/>s into a <see cref="CommitRequest"/>, without modifying the model.
    /// </summary>
    public interface IModelChangeApplier
    {
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
        ErrorOr<CommitRequest> Apply(IReadOnlyCollection<IElement> model, IReadOnlyList<ModelChange> changes);
    }
}
