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
    /// <param name="Types">The declared names of the types of the element, empty when it is not typed.</param>
    /// <param name="Attributes">The attributes of the element, including those inherited from its types.</param>
    public sealed record AttributeValues(Guid Id, string Name, IReadOnlyList<string> Types, IReadOnlyList<AttributeValue> Attributes);
}
