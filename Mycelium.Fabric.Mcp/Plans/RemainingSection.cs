// ------------------------------------------------------------------------------------------------
//  <copyright file="RemainingSection.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Plans
{
    using System.Collections.Generic;

    /// <summary>
    /// A section of a <see cref="ConstructionPlan"/> that the model does not hold entirely yet.
    /// </summary>
    /// <param name="Name">The name of the section.</param>
    /// <param name="MissingElementCount">The number of planned elements of the section that the model does not hold.</param>
    /// <param name="MissingElements">
    /// The first missing elements, in the order of the plan, at most 20 for the whole plan: they can be fewer than
    /// <paramref name="MissingElementCount"/>.
    /// </param>
    public sealed record RemainingSection(string Name, int MissingElementCount, IReadOnlyList<PlannedElement> MissingElements);
}
