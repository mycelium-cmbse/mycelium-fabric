// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementDetails.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// The result of the <c>get_element_details</c> tool: the details of one element of the model.
    /// </summary>
    /// <param name="ElementId">The identifier of the element.</param>
    /// <param name="Name">The declared name of the element, or <c>null</c> when it has none.</param>
    /// <param name="ShortName">The declared short name of the element, or <c>null</c> when it has none.</param>
    /// <param name="Type">The metaclass of the element, followed by its definition when it is typed (for example <c>PartUsage : OpticalCamera</c>).</param>
    /// <param name="QualifiedName">The qualified name of the element.</param>
    /// <param name="OwnerId">The identifier of the owner of the element, or <c>null</c> for a root element.</param>
    /// <param name="OwnerName">The declared name of the owner of the element, or <c>null</c> when it has none.</param>
    /// <param name="ChildCount">The number of elements directly owned by the element.</param>
    /// <param name="Documentation">The documentation of the element, or <c>null</c> when it has none.</param>
    public sealed record ElementDetails(string ElementId, string Name, string ShortName, string Type, string QualifiedName, string OwnerId, string OwnerName, int ChildCount, string Documentation);
}