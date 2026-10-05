// ------------------------------------------------------------------------------------------------
//  <copyright file="CreatedElement.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;

    /// <summary>
    /// An element created by a batch of changes, returned so that the AI assistant can use its identifier afterwards.
    /// </summary>
    /// <param name="TemporaryName">The temporary name given to the element in the batch, or <c>null</c> when it had none.</param>
    /// <param name="Id">The identifier given to the element by the server.</param>
    /// <param name="Name">The declared name of the element.</param>
    /// <param name="Type">The metaclass of the element, for example <c>PartUsage</c>.</param>
    public sealed record CreatedElement(string TemporaryName, Guid Id, string Name, string Type);
}
