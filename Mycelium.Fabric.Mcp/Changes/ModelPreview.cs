// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelPreview.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Collections.Generic;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// What a batch of changes would do to the model, computed on a copy: either the batch is valid and the model it would
    /// give is described, or it is not and the problems are listed. The loaded model is never modified.
    /// </summary>
    /// <param name="Problems">The problems that prevent the batch from being applied, empty when it is valid.</param>
    public sealed record ModelPreview(IReadOnlyList<string> Problems)
    {
        /// <summary>
        /// Gets a value indicating whether the batch is valid, which is the case when it has no problem.
        /// </summary>
        public bool IsValid => this.Problems.Count == 0;

        /// <summary>
        /// Gets all the elements of the model that the batch would give, built by SysML2.NET; empty when it is not valid.
        /// </summary>
        public IReadOnlyList<IElement> Elements { get; init; } = [];

        /// <summary>
        /// Gets the elements that the batch would create, as <see cref="ApplyChangesResult.CreatedElements"/> lists them.
        /// </summary>
        public IReadOnlyList<CreatedElement> CreatedElements { get; init; } = [];

        /// <summary>
        /// Gets the existing elements that a change of the batch would update.
        /// </summary>
        public IReadOnlyList<ChangedElement> ModifiedElements { get; init; } = [];

        /// <summary>
        /// Gets the existing elements that a change of the batch would delete.
        /// </summary>
        public IReadOnlyList<ChangedElement> DeletedElements { get; init; } = [];
    }
}
