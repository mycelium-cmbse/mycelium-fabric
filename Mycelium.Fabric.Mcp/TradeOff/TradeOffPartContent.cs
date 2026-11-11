// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffPartContent.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// What a part holds when a choice takes one of its options: where its values come from and how many times they count.
    /// </summary>
    /// <param name="Source">
    /// The feature whose values count: the variant of a catalogue, or the part itself as it is in the model; <c>null</c> for
    /// equipment that is not in the model.
    /// </param>
    /// <param name="Values">The values that replace those of the source for one unit, by attribute name.</param>
    /// <param name="Quantity">
    /// The number of times that the values count under the parts that hold the part: the number of units taken, 0 when the
    /// part is removed or the equipment is not added.
    /// </param>
    /// <param name="IsVariant">Whether the source is a variant of a catalogue, which stands for the part.</param>
    /// <param name="IsCurrent">Whether this is what the part holds in the model.</param>
    public sealed record TradeOffPartContent(IFeature Source, IReadOnlyDictionary<string, NumberValue> Values, int Quantity, bool IsVariant = false, bool IsCurrent = false)
    {
        /// <summary>
        /// Gets a value indicating whether the part is not in the combination at all: it is removed, or the equipment is not
        /// added. A part of a catalogue taken 0 times is not removed: it stays in the model, with a multiplicity of 0, and
        /// counts for 0.
        /// </summary>
        public bool IsRemoved => this.Quantity == 0 && this.Source == null;
    }
}
