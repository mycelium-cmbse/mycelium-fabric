// ------------------------------------------------------------------------------------------------
//  <copyright file="SequenceValue.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A sequence of values, for example <c>(1, 2, 3)</c>, or <c>null</c> when it is empty, as in SysML v2.
    /// </summary>
    /// <param name="Items">The values of the sequence.</param>
    public sealed record SequenceValue(IReadOnlyList<ModelValue> Items) : ModelValue
    {
        /// <inheritdoc/>
        public override string KindName => "a sequence";

        /// <summary>
        /// Gets the empty sequence, which <c>null</c> stands for.
        /// </summary>
        public static SequenceValue Empty { get; } = new([]);

        /// <summary>
        /// Writes the sequence as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The sequence, for example <c>(1, 2, 3)</c>, or <c>null</c> when it is empty.</returns>
        public override string ToString()
        {
            return this.Items.Count == 0 ? "null" : $"({string.Join(", ", this.Items.Select(item => item.ToString()))})";
        }
    }
}
