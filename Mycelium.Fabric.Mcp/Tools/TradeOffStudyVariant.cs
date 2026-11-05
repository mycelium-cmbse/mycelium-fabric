// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffStudyVariant.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    /// <summary>
    /// A variant of a <see cref="TradeOffStudyChoice"/>, with its values as the server reads them.
    /// </summary>
    /// <param name="Name">The name of the variant, for example <c>current</c> or <c>A</c>.</param>
    /// <param name="IsCurrent">Whether the variant is the current state: the part as it is, or the equipment not added.</param>
    /// <param name="Excluded">Whether the user has turned the variant off.</param>
    /// <param name="MinQuantity">The smallest number of units.</param>
    /// <param name="MaxQuantity">The largest number of units.</param>
    /// <param name="Values">The value of each attribute for one unit, in the unit of the attribute.</param>
    public sealed record TradeOffStudyVariant(string Name, bool IsCurrent, bool Excluded, int MinQuantity, int MaxQuantity, IReadOnlyDictionary<string, double> Values);
}
