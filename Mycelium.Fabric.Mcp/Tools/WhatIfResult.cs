// ------------------------------------------------------------------------------------------------
//  <copyright file="WhatIfResult.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// The result of the <c>evaluate_what_if</c> tool: the effect of another value for one part on a total and on the
    /// requirements.
    /// </summary>
    /// <param name="Attribute">The name of the summed attribute.</param>
    /// <param name="ChangedElement">The qualified name of the part whose value changes.</param>
    /// <param name="OldValue">The current value of the attribute for the part.</param>
    /// <param name="NewValue">The simulated value of the attribute for the part.</param>
    /// <param name="CurrentTotal">The current total, rounded to 3 decimals.</param>
    /// <param name="NewTotal">The total with the simulated value, rounded to 3 decimals.</param>
    /// <param name="Difference">The new total minus the current total, rounded to 3 decimals.</param>
    /// <param name="RequirementImpacts">
    /// The requirements whose check depends on the simulated value, each checked with the current and the simulated value.
    /// Empty when no requirement depends on it.
    /// </param>
    public sealed record WhatIfResult(string Attribute, string ChangedElement, double OldValue, double NewValue, double CurrentTotal, double NewTotal, double Difference,
        IReadOnlyList<RequirementImpact> RequirementImpacts);
}
