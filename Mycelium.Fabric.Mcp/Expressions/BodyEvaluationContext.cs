// ------------------------------------------------------------------------------------------------
//  <copyright file="BodyEvaluationContext.cs" company="Starion Group S.A.">
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
    /// Gives the values of the model to the body of a <c>select</c> or a <c>collect</c>: its parameter stands for the item
    /// being evaluated, and any other value comes from the context of the whole expression.
    /// </summary>
    public sealed class BodyEvaluationContext : IEvaluationContext
    {
        /// <summary>
        /// The context of the whole expression.
        /// </summary>
        private readonly IEvaluationContext outerContext;

        /// <summary>
        /// The parameter of the body.
        /// </summary>
        private readonly IFeature parameter;

        /// <summary>
        /// The item that the parameter stands for.
        /// </summary>
        private readonly ModelValue item;

        /// <summary>
        /// Initializes a new instance of the <see cref="BodyEvaluationContext"/> class.
        /// </summary>
        /// <param name="outerContext">The context of the whole expression.</param>
        /// <param name="parameter">The parameter of the body.</param>
        /// <param name="item">The item that the parameter stands for.</param>
        public BodyEvaluationContext(IEvaluationContext outerContext, IFeature parameter, ModelValue item)
        {
            this.outerContext = outerContext ?? throw new ArgumentNullException(nameof(outerContext));
            this.parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            this.item = item;
        }

        /// <summary>
        /// Gets the value of a referenced feature: the item for the parameter, or else the value that the context of the
        /// whole expression gives.
        /// </summary>
        /// <param name="feature">The referenced feature.</param>
        /// <returns>The value of the feature, or an error that tells why it has none.</returns>
        public ErrorOr<ModelValue> GetValue(IFeature feature)
        {
            ArgumentNullException.ThrowIfNull(feature);

            return feature.Id == this.parameter.Id ? this.item : this.outerContext.GetValue(feature);
        }

        /// <summary>
        /// Gets the value of a feature of a part, as the context of the whole expression gives it.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>The value of the feature, or an error that tells why it has none.</returns>
        public ErrorOr<ModelValue> Navigate(PartValue part, string featureName)
        {
            return this.outerContext.Navigate(part, featureName);
        }
    }
}
