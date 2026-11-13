// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffTotalParts.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Tools;

    /// <summary>
    /// The total of an attribute under a part, split into what no choice changes and what each option of each choice adds,
    /// so that the solver can write the total of any combination as a sum.
    /// </summary>
    /// <param name="Fixed">The <see cref="Contribution"/>s of the parts that no choice changes.</param>
    /// <param name="Options">
    /// The <see cref="Contribution"/>s that each option adds, by choice then by option, already counted as many times as the
    /// option and the parts that hold the part say.
    /// </param>
    public sealed record TradeOffTotalParts(IReadOnlyList<Contribution> Fixed, IReadOnlyList<IReadOnlyList<IReadOnlyList<Contribution>>> Options);
}
