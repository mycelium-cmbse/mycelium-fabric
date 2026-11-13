// ------------------------------------------------------------------------------------------------
//  <copyright file="SatisfiedRequirement.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    using System.Collections.Generic;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Expressions;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// A requirement that can be checked, with one part that satisfies it: what a trade-off checks again on each combination.
    /// </summary>
    /// <param name="Requirement">The requirement.</param>
    /// <param name="SatisfyingFeature">The part that satisfies the requirement.</param>
    /// <param name="Subject">The subject of the requirement, or <c>null</c> when it has none.</param>
    /// <param name="Assumptions">The expressions of the assumptions, or the errors that tell why they cannot be read.</param>
    /// <param name="Required">The expressions of the required constraints, or the errors that tell why they cannot be read.</param>
    public sealed record SatisfiedRequirement(IRequirementUsage Requirement, IFeature SatisfyingFeature, IFeature Subject, IReadOnlyList<ErrorOr<Term>> Assumptions,
        IReadOnlyList<ErrorOr<Term>> Required);
}
