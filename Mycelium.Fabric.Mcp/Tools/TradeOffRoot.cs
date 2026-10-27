// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffRoot.cs" company="Starion Group S.A.">
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
    /// The element under which the totals of a <see cref="TradeOffStudy"/> are computed.
    /// </summary>
    /// <param name="Id">The <c>Id</c> of the element.</param>
    /// <param name="QualifiedName">The qualified name of the element.</param>
    public sealed record TradeOffRoot(Guid Id, string QualifiedName);
}
