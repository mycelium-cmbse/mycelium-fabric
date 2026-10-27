// ------------------------------------------------------------------------------------------------
//  <copyright file="PendingBatch.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Changes;

    /// <summary>
    /// A valid batch of changes that waits for the decision of the user.
    /// </summary>
    /// <param name="Id">The identifier of the batch, given by the server.</param>
    /// <param name="Token">
    /// The secret that the view of the batch sends with the decision, so that only the view can apply or reject the batch:
    /// it is sent to the view only, never to the AI assistant.
    /// </param>
    /// <param name="Changes">The changes of the batch, applied again when the user approves it.</param>
    public sealed record PendingBatch(Guid Id, string Token, IReadOnlyList<ModelChange> Changes);
}
