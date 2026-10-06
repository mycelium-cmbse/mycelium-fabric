// ------------------------------------------------------------------------------------------------
//  <copyright file="SatisfyRequirementUsageExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;
    using System.Linq;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// Extension methods that read, from the <see cref="ISatisfyRequirementUsage"/>s of a SysML v2 model, the information
    /// used by the MCP tools.
    /// </summary>
    public static class SatisfyRequirementUsageExtensions
    {
        /// <param name="satisfy">The <see cref="ISatisfyRequirementUsage"/> to read.</param>
        extension(ISatisfyRequirementUsage satisfy)
        {
            /// <summary>
            /// Resolves the feature that satisfies the requirement: the feature referenced by the value of the subject
            /// parameter, which is how the textual notation <c>satisfy massBudget by eosat1;</c> is built. Otherwise, the
            /// derived <c>satisfyingFeature</c>, which SysML2.NET computes from a <c>BindingConnector</c> that models
            /// exchanged without their implied relationships do not contain.
            /// </summary>
            /// <returns>The satisfying feature, or <c>null</c> when the satisfy link does not name one.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="satisfy"/> is <c>null</c>.
            /// </exception>
            public IFeature ResolveSatisfyingFeature()
            {
                ArgumentNullException.ThrowIfNull(satisfy);

                var referencedFeature = (satisfy.subjectParameter?.OwnedRelationship ?? [])
                    .OfType<IFeatureValue>()
                    .Select(featureValue => featureValue.value)
                    .OfType<IFeatureReferenceExpression>()
                    .Select(reference => reference.referent)
                    .FirstOrDefault();

                return referencedFeature ?? satisfy.satisfyingFeature;
            }
        }
    }
}
