// ------------------------------------------------------------------------------------------------
//  <copyright file="ApplyChangesResult.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Collections.Generic;

    /// <summary>
    /// The result of a batch of changes: either the whole batch is applied, or nothing is and the problems are listed.
    /// </summary>
    /// <param name="CreatedElements">The elements created by the batch, empty when it has not been applied.</param>
    /// <param name="Problems">The problems that prevent the batch from being applied, empty when it has been applied.</param>
    public sealed record ApplyChangesResult(IReadOnlyList<CreatedElement> CreatedElements, IReadOnlyList<string> Problems)
    {
        /// <summary>
        /// Gets a value indicating whether the batch has been applied to the model, which is the case when it has no problem.
        /// </summary>
        public bool Applied => this.Problems.Count == 0;
    }
}
