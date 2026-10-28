// ------------------------------------------------------------------------------------------------
//  <copyright file="CollectedValues.cs" company="Starion Group S.A.">
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
    /// The values of an attribute found over a part and its sub-parts, before they are added up: the parts that give a
    /// value, and the parts whose value is still missing.
    /// </summary>
    /// <param name="Contributions">The <see cref="Contribution"/> of each part that gives a value.</param>
    /// <param name="MissingValues">The <see cref="MissingValue"/> of each part that should give a value and has none yet.</param>
    public sealed record CollectedValues(IReadOnlyList<Contribution> Contributions, IReadOnlyList<MissingValue> MissingValues);
}
