// ------------------------------------------------------------------------------------------------
//  <copyright file="AttributeValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    /// <summary>
    /// One attribute of an element, returned by the <c>get_attribute_values</c> tool.
    /// </summary>
    /// <param name="Name">The declared name of the attribute.</param>
    /// <param name="Value">The numeric value of the attribute, or <c>null</c> when it is not bound to a literal number.</param>
    /// <param name="Documentation">The documentation of the attribute, which usually gives its unit, or <c>null</c> when it has none.</param>
    public sealed record AttributeValue(string Name, double? Value, string Documentation);
}
