// ------------------------------------------------------------------------------------------------
//  <copyright file="SumResult.cs" company="Starion Group S.A.">
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
    /// The result of the <c>sum_attribute</c> tool: the total of an attribute over an element and its sub-parts.
    /// </summary>
    /// <param name="Attribute">The name of the summed attribute.</param>
    /// <param name="Total">The sum of the contributions, rounded to 3 decimals.</param>
    /// <param name="ContributorCount">The number of parts that contribute to the total.</param>
    /// <param name="Contributions">The value that each contributing part adds to the total.</param>
    public sealed record SumResult(string Attribute, double Total, int ContributorCount, IReadOnlyList<Contribution> Contributions);
}
