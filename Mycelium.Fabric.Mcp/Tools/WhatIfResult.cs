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
    /// <summary>
    /// The result of the <c>evaluate_what_if</c> tool: the effect on a total of another value for one part.
    /// </summary>
    /// <param name="Attribute">The name of the summed attribute.</param>
    /// <param name="ChangedElement">The qualified name of the part whose value changes.</param>
    /// <param name="OldValue">The current value of the attribute for the part.</param>
    /// <param name="NewValue">The simulated value of the attribute for the part.</param>
    /// <param name="CurrentTotal">The current total, rounded to 3 decimals.</param>
    /// <param name="NewTotal">The total with the simulated value, rounded to 3 decimals.</param>
    /// <param name="Difference">The new total minus the current total, rounded to 3 decimals.</param>
    public sealed record WhatIfResult(string Attribute, string ChangedElement, double OldValue, double NewValue, double CurrentTotal, double NewTotal, double Difference);
}
