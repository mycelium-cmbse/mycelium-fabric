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
    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// One attribute of an element, returned by the <c>get_attribute_values</c> tool.
    /// </summary>
    /// <param name="Name">The declared name of the attribute.</param>
    /// <param name="Value">The numeric value of the attribute, or <c>null</c> when it is not bound to a number.</param>
    /// <param name="Unit">The unit of the numeric value, or <c>null</c> when it has none.</param>
    /// <param name="ValueText">
    /// The value when it is not a number, as written in the model (<c>true</c>, <c>"S-band"</c>, <c>sunSynchronous</c>), or
    /// <c>null</c>.
    /// </param>
    /// <param name="Documentation">The documentation of the attribute, which can give its unit, or <c>null</c> when it has none.</param>
    public sealed record AttributeValue(string Name, double? Value, Unit Unit, string ValueText, string Documentation);
}
