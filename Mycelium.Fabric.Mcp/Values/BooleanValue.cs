// ------------------------------------------------------------------------------------------------
//  <copyright file="BooleanValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    /// <summary>
    /// A Boolean, for example the result of a constraint, with the reason of its value when an expression computed it.
    /// </summary>
    /// <param name="Value">The Boolean.</param>
    public sealed record BooleanValue(bool Value) : ModelValue
    {
        /// <inheritdoc/>
        public override string KindName => "a Boolean";

        /// <summary>
        /// Gets why the Boolean has its value, for example <c>150.96 &gt; 150</c>, or <c>null</c> when it is a literal.
        /// </summary>
        public string Explanation { get; init; }

        /// <summary>
        /// Gets, for a comparison of numbers, how far the left number is from the right one: positive or zero when the
        /// comparison holds, negative by the excess when it does not; <c>null</c> otherwise.
        /// </summary>
        public double? Gap { get; init; }

        /// <summary>
        /// Writes the Boolean as in the SysML v2 textual notation.
        /// </summary>
        /// <returns><c>true</c> or <c>false</c>.</returns>
        public override string ToString()
        {
            return this.Value ? "true" : "false";
        }
    }
}
