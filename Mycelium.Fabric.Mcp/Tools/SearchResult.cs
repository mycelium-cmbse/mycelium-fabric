// ------------------------------------------------------------------------------------------------
//  <copyright file="SearchResult.cs" company="Starion Group S.A.">
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
    /// The result of the <c>find_elements_by_name</c> tool: the elements whose name matches the searched text.
    /// </summary>
    /// <param name="TotalFound">The total number of matching elements, which can be greater than the number of returned elements.</param>
    /// <param name="Elements">The matching elements of the requested page, sorted by name.</param>
    /// <param name="NextOffset">The offset that gives the next page, or <c>null</c> when this page is the last one.</param>
    public sealed record SearchResult(int TotalFound, IReadOnlyList<ElementSummary> Elements, int? NextOffset);
}