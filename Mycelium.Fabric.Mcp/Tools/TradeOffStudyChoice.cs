// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffStudyChoice.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// A choice of a <see cref="TradeOffStudy"/>, with every variant that the server considers.
    /// </summary>
    /// <param name="Name">The name of the choice.</param>
    /// <param name="PartId">The <c>Id</c> of the existing part, or <c>null</c> for equipment to add.</param>
    /// <param name="QualifiedName">The qualified name of the existing part, or <c>null</c> for equipment to add.</param>
    /// <param name="Variants">
    /// The <see cref="TradeOffStudyVariant"/> of each variant, the current one (or <c>none</c> for equipment to add) first.
    /// </param>
    public sealed record TradeOffStudyChoice(string Name, Guid? PartId, string QualifiedName, IReadOnlyList<TradeOffStudyVariant> Variants);
}
