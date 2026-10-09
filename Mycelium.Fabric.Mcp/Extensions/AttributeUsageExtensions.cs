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

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Values;

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
            /// Gets the value bound to the attribute by its <see cref="IFeatureValue"/> when it is written without reading
            /// other values: a number with or without unit (<c>38</c>, <c>38 [kg]</c>, <c>-20</c>), a Boolean, a text or an
            /// enumeration value.
            /// </summary>
            /// <returns>The value of the attribute, or <c>null</c> when it has none or when its value reads other values.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="attribute"/> is <c>null</c>.
            /// </exception>
            public ModelValue GetConstantValue()
            {
                ArgumentNullException.ThrowIfNull(attribute);

                var valueExpression = TermReader.GetValueExpression(attribute);

                if (valueExpression == null)
                {
                    return null;
                }

                var value = TermReader.Read(valueExpression).Then(term => term.Evaluate(ConstantEvaluationContext.Instance));

                return value.IsError ? null : value.Value;
            }

            /// <summary>
            /// Gets the numeric value of the attribute, with its unit if it has one (for example <c>38 [kg]</c> for
            /// <c>attribute mass = 38 [kg];</c>).
            /// </summary>
            /// <returns>The value of the attribute, or <c>null</c> when it is not bound to a finite number.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="attribute"/> is <c>null</c>.
            /// </exception>
            public NumberValue GetNumericValue()
            {
                return attribute.GetConstantValue() is NumberValue { Number: var number } numericValue && double.IsFinite(number) ? numericValue : null;
            }
        }
    }
}
