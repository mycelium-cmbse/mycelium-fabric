// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstantEvaluationContext.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// Evaluates the values written in the model without reading other values, as <c>38 [kg]</c>, <c>-20</c>, <c>true</c>
    /// or <c>OrbitKind::sunSynchronous</c>: a reference to another feature or a feature chain cannot be evaluated.
    /// </summary>
    public sealed class ConstantEvaluationContext : IEvaluationContext
    {
        /// <summary>
        /// Gets the single instance of the context, which has no state.
        /// </summary>
        public static ConstantEvaluationContext Instance { get; } = new();

        /// <summary>
        /// Gets the value of an enumeration value; another feature is not a constant.
        /// </summary>
        /// <param name="feature">The referenced feature.</param>
        /// <returns>The <see cref="EnumValue"/>, or an error for another feature.</returns>
        public ErrorOr<ModelValue> GetValue(IFeature feature)
        {
            ArgumentNullException.ThrowIfNull(feature);

            return ModelEvaluationContext.IsEnumerationValue(feature)
                ? new EnumValue(feature.DeclaredName)
                : Error.Validation(description: $"'{feature.DeclaredName}' is not a constant.");
        }

        /// <summary>
        /// Refuses to navigate a part, which is not a constant.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>An error.</returns>
        public ErrorOr<ModelValue> Navigate(PartValue part, string featureName)
        {
            return Error.Validation(description: $"'{featureName}' is not a constant.");
        }
    }
}
