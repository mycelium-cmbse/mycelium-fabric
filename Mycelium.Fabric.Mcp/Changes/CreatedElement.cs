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
    /// <param name="Id">The identifier given to the element by the server.</param>
    /// <param name="QualifiedName">
    /// The qualified name of the element, for example <c>EOSat1::Architecture::eosat1::camera</c>, or <c>null</c> when the
    /// element or one of its owners has no name.
    /// </param>
    /// <param name="Type">The metaclass of the element, for example <c>PartUsage</c>.</param>
    public sealed record CreatedElement(Guid Id, string QualifiedName, string Type);
}
