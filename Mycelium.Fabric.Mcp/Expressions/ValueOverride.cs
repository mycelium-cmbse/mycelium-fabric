// ------------------------------------------------------------------------------------------------
//  <copyright file="ValueOverride.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;

    /// <summary>
    /// A simulated value of an attribute for one part, used by <c>evaluate_what_if</c> instead of the value in the model.
    /// </summary>
    /// <param name="ElementId">The <c>Id</c> of the part whose value changes.</param>
    /// <param name="Attribute">The name of the attribute, for example <c>mass</c>.</param>
    /// <param name="Value">The simulated value, in the unit of the current value.</param>
    public sealed record ValueOverride(Guid ElementId, string Attribute, double Value);
}
