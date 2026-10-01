// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementSummary.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// A short description of one element of the model, returned by the tools that list elements.
    /// </summary>
    /// <param name="ElementId">The identifier of the element.</param>
    /// <param name="Name">The declared name of the element, or <c>null</c> when it has none.</param>
    /// <param name="Type">The metaclass of the element, followed by its definition when it is typed (for example <c>PartUsage : OpticalCamera</c>).</param>
    /// <param name="QualifiedName">The qualified name of the element.</param>
    public sealed record ElementSummary(string ElementId, string Name, string Type, string QualifiedName);
}