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
    /// <param name="Value">The value of the attribute for one unit of the part.</param>
    /// <param name="Unit">The unit of the value, or <c>null</c> when it has none.</param>
    /// <param name="Margin">
    /// The margin that the model gives for the value, in %, for example <c>10</c> for <c>massMargin = 10</c>, or <c>null</c>
    /// when it gives none.
    /// </param>
    /// <param name="Count">
    /// The number of units of the part under the root of the sum, from its multiplicity and the ones of the parts that hold
    /// it (<c>part wheels : Wheel[4]</c> in <c>part bogies : Bogie[2]</c> gives 8), or <c>null</c> for one unit.
    /// </param>
    public sealed record Contribution(Guid Id, string QualifiedName, IReadOnlyList<string> Types, double Value, Unit Unit = null, double? Margin = null, int? Count = null)
    {
        /// <summary>
        /// Gets the value of all the units of the part, in the unit of the value.
        /// </summary>
        /// <returns>The value multiplied by the number of units.</returns>
        public NumberValue GetTotal()
        {
            return new NumberValue(this.Value * (this.Count ?? 1), this.Unit);
        }

        /// <summary>
        /// Gets the value of all the units of the part increased by its margin, in the unit of the value, or without margin when
        /// the model gives none.
        /// </summary>
        /// <returns>The value of all the units with its margin.</returns>
        public NumberValue GetTotalWithMargin()
        {
            return new NumberValue(this.Value * (this.Count ?? 1) * (1 + (this.Margin ?? 0) / 100), this.Unit);
        }
    }
}
