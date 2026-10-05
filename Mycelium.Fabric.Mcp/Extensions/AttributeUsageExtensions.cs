// ------------------------------------------------------------------------------------------------
//  <copyright file="AttributeUsageExtensions.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Systems.Attributes;

    /// <summary>
    /// Extension methods that read, from the <see cref="IAttributeUsage"/>s of a SysML v2 model, the information returned by
    /// the MCP tools.
    /// </summary>
    public static class AttributeUsageExtensions
    {
        /// <param name="attribute">The <see cref="IAttributeUsage"/> to read.</param>
        extension(IAttributeUsage attribute)
        {
            /// <summary>
            /// Gets the numeric value of the attribute: the literal number bound to it by its <see cref="IFeatureValue"/>
            /// (for example <c>38</c> for <c>attribute mass = 38;</c>).
            /// </summary>
            /// <returns>The value of the attribute, or <c>null</c> when it is not bound to a literal number.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="attribute"/> is <c>null</c>.
            /// </exception>
            public double? GetNumericValue()
            {
                ArgumentNullException.ThrowIfNull(attribute);

                return (attribute.OwnedRelationship ?? [])
                    .OfType<IFeatureValue>()
                    .SelectMany(featureValue => featureValue.OwnedRelatedElement ?? [])
                    .Select(valueElement => valueElement switch
                    {
                        ILiteralRational literalRational => literalRational.Value,
                        ILiteralInteger literalInteger => literalInteger.Value,
                        _ => (double?)null
                    })
                    .FirstOrDefault(value => value != null);
            }
        }
    }
}
