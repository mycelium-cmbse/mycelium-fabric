// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffAttribute.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// An attribute whose total a <see cref="TradeOffStudy"/> computes, with the unit of every value and total of it.
    /// </summary>
    /// <param name="Name">The name of the attribute, for example <c>mass</c>.</param>
    /// <param name="Unit">The unit of the attribute, or <c>null</c> when it has none.</param>
    public sealed record TradeOffAttribute(string Name, Unit Unit = null);
}
