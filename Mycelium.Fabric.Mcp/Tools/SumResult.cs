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

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// The result of the <c>sum_attribute</c> tool: the total of an attribute over an element and its sub-parts.
    /// </summary>
    /// <param name="Attribute">The name of the summed attribute.</param>
    /// <param name="Total">The sum of the contributions, converted to the unit of the total and rounded to 3 decimals.</param>
    /// <param name="Unit">The unit of the total: the unit of the first contribution that has one, or <c>null</c>.</param>
    /// <param name="ContributorCount">The number of parts that contribute to the total.</param>
    /// <param name="Contributions">The value that each contributing part adds to the total, in its own unit.</param>
    /// <param name="TotalWithMargins">
    /// The total increased by the margins that the model gives for the values (attribute <c>massMargin</c> in % for
    /// <c>mass</c>), in the unit of the total and rounded to 3 decimals, or <c>null</c> when the model gives no margin.
    /// </param>
    /// <param name="MissingValues">
    /// The parts that should contribute and have no value yet ("to be defined"), so that the total is incomplete, or
    /// <c>null</c> when no value is missing.
    /// </param>
    public sealed record SumResult(string Attribute, double Total, Unit Unit, int ContributorCount, IReadOnlyList<Contribution> Contributions, double? TotalWithMargins = null,
        IReadOnlyList<MissingValue> MissingValues = null);
}
