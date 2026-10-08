// ------------------------------------------------------------------------------------------------
//  <copyright file="PendingCommit.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Collections.Generic;

    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// The result of an <see cref="IModelChangeApplier"/>: the change of the commit that a batch of <see cref="ModelChange"/>s
    /// makes, or the problems that prevent it.
    /// </summary>
    /// <param name="Change">
    /// The <see cref="DataVersion"/> records of the commit, as <c>Commit.change</c> of the Systems Modeling API and Services
    /// 1.0 (§7.1.2): a payload for each created or updated element, none for each deleted element. Empty when the batch has
    /// problems.
    /// </param>
    /// <param name="CreatedElements">The elements created by the batch, empty when the batch has problems.</param>
    /// <param name="Problems">The problems that prevent the batch from being applied, empty when it can be committed.</param>
    public sealed record PendingCommit(IReadOnlyList<DataVersion> Change, IReadOnlyList<CreatedElement> CreatedElements, IReadOnlyList<string> Problems);
}
