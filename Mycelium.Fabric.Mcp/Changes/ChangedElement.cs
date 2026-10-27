// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangedElement.cs" company="Starion Group S.A.">
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
    /// An existing element that a batch of changes modifies or deletes, shown to the user before the batch is applied.
    /// </summary>
    /// <param name="Id">The identifier of the element.</param>
    /// <param name="QualifiedName">
    /// The qualified name of the element, after the batch for a modified element and before it for a deleted one, or
    /// <c>null</c> when the element or one of its owners has no name.
    /// </param>
    /// <param name="Type">The metaclass of the element, for example <c>PartUsage</c>.</param>
    public sealed record ChangedElement(Guid Id, string QualifiedName, string Type);
}
