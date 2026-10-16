// ------------------------------------------------------------------------------------------------
//  <copyright file="ChosenVariant.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;

    /// <summary>
    /// The variant taken for one choice in a <see cref="TradeOffCombination"/>.
    /// </summary>
    /// <param name="Choice">The name of the choice.</param>
    /// <param name="PartId">The <c>Id</c> of the existing part, or <c>null</c> for equipment to add.</param>
    /// <param name="QualifiedName">The qualified name of the existing part, or <c>null</c> for equipment to add.</param>
    /// <param name="Variant">The name of the taken variant.</param>
    /// <param name="Quantity">The number of units, 0 for the variant <c>none</c>.</param>
    /// <param name="Changed">Whether the variant differs from the current state.</param>
    public sealed record ChosenVariant(string Choice, Guid? PartId, string QualifiedName, string Variant, int Quantity, bool Changed);
}
