// ------------------------------------------------------------------------------------------------
//  <copyright file="Contribution.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// The value that one part adds to the total computed by the <c>sum_attribute</c> tool.
    /// </summary>
    /// <param name="Id">The identifier of the part, to pass to <c>evaluate_what_if</c>.</param>
    /// <param name="QualifiedName">The qualified name of the part.</param>
    /// <param name="Types">The declared names of the types of the part, empty when it is not typed.</param>
    /// <param name="Value">The value of the attribute for the part.</param>
    /// <param name="Unit">The unit of the value, or <c>null</c> when it has none.</param>
    /// <param name="Margin">
    /// The margin that the model gives for the value, in %, for example <c>10</c> for <c>massMargin = 10</c>, or <c>null</c>
    /// when it gives none.
    /// </param>
    public sealed record Contribution(Guid Id, string QualifiedName, IReadOnlyList<string> Types, double Value, Unit Unit = null, double? Margin = null)
    {
        /// <summary>
        /// Gets the value increased by its margin, in the unit of the value, or the value alone when the model gives no margin.
        /// </summary>
        /// <returns>The value with its margin.</returns>
        public NumberValue GetValueWithMargin()
        {
            return new NumberValue(this.Value * (1 + (this.Margin ?? 0) / 100), this.Unit);
        }
    }
}
