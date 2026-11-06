// ------------------------------------------------------------------------------------------------
//  <copyright file="PlanProgress.cs" company="Starion Group S.A.">
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
    /// What the model holds of a <see cref="ConstructionPlan"/>: the sections that are done and the ones that are left.
    /// </summary>
    /// <param name="DoneSections">The names of the sections whose planned elements are all in the model, in the order of the plan.</param>
    /// <param name="RemainingSections">The sections that are left, in the order of the plan.</param>
    /// <param name="Reminder">The sentence that tells the AI assistant how many sections and elements are left.</param>
    public sealed record PlanProgress(IReadOnlyList<string> DoneSections, IReadOnlyList<RemainingSection> RemainingSections, string Reminder);
}
