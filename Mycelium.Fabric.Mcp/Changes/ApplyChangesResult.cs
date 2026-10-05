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
    /// <param name="Applied">Whether the batch has been applied to the model.</param>
    /// <param name="CreatedElements">The elements created by the batch, empty when it has not been applied.</param>
    /// <param name="Problems">The problems that prevent the batch from being applied, empty when it has been applied.</param>
    public sealed record ApplyChangesResult(bool Applied, IReadOnlyList<CreatedElement> CreatedElements, IReadOnlyList<string> Problems);
}
