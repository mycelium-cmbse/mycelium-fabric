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

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// The result of an <see cref="IModelChangeApplier"/>: the <see cref="SysML2.NET.PSM.DTO.CommitRequest"/> that makes the
    /// modifications of a batch of <see cref="ModelChange"/>s, or the problems that prevent it.
    /// </summary>
    /// <param name="CommitRequest">
    /// The request that creates the commit, as the body of <c>POST /projects/{projectId}/commits</c> in the Systems Modeling
    /// API and Services 1.0 (§8.1.3): a <c>DataVersionRequest</c> with a payload for each created or updated element, and
    /// one without payload for each deleted element. <c>null</c> when the batch has problems.
    /// </param>
    /// <param name="CreatedElements">The elements created by the batch, empty when the batch has problems.</param>
    /// <param name="Problems">The problems that prevent the batch from being applied, empty when it can be committed.</param>
    public sealed record PendingCommit(CommitRequest CommitRequest, IReadOnlyList<CreatedElement> CreatedElements, IReadOnlyList<string> Problems);
}
