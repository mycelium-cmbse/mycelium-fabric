// ------------------------------------------------------------------------------------------------
//  <copyright file="AttributeValues.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The result of the <c>get_attribute_values</c> tool: the attributes of one element of the model.
    /// </summary>
    /// <param name="Id">The identifier of the element.</param>
    /// <param name="Name">The declared name of the element, or <c>null</c> when it has none.</param>
    /// <param name="Definition">The declared name of the definition of the element, or <c>null</c> when it is not typed.</param>
    /// <param name="Attributes">The attributes of the element, followed by those of its definition.</param>
    public sealed record AttributeValues(Guid Id, string Name, string Definition, IReadOnlyList<AttributeValue> Attributes);
}
