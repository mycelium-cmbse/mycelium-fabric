// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffRequirement.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// A requirement of the model that a trade-off checks on every combination.
    /// </summary>
    /// <param name="Source">The <see cref="SatisfiedRequirement"/>: the requirement and the part that satisfies it.</param>
    /// <param name="Name">
    /// The name shown for the requirement: its identifier (<c>REQ-SYS-001</c>) or its name, with the satisfying part when
    /// several parts satisfy it.
    /// </param>
    /// <param name="LimitIndex">
    /// The position, in the limits of the problem, of the limit that the solver searches with, or <c>null</c> when the
    /// requirement is not a comparison of sums: it is then only checked on the combinations found.
    /// </param>
    /// <param name="IsIgnored">Whether the user turned the requirement off: it is not checked.</param>
    public sealed record TradeOffRequirement(SatisfiedRequirement Source, string Name, int? LimitIndex = null, bool IsIgnored = false);
}
