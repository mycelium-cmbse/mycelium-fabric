// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementImpact.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    /// <summary>
    /// The effect of a simulated change of value on the check of a requirement whose constraint depends on this value.
    /// </summary>
    /// <param name="Current">The check of the requirement with the current value.</param>
    /// <param name="New">The check of the requirement with the simulated value.</param>
    public sealed record RequirementImpact(RequirementCheck Current, RequirementCheck New);
}
