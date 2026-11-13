// ------------------------------------------------------------------------------------------------
//  <copyright file="PartSlot.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// A part left open while the values of an attribute are collected: its place is known, what it holds is not decided yet,
    /// so that a trade-off can fill it with each of its variants.
    /// </summary>
    /// <param name="Part">The open part.</param>
    /// <param name="Count">
    /// The number of times that the parts that hold it count under the element whose values are collected. The number of
    /// units of the part itself is not included: it is part of what the trade-off decides.
    /// </param>
    public sealed record PartSlot(IPartUsage Part, int Count);
}
