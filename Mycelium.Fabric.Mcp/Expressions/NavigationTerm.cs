// ------------------------------------------------------------------------------------------------
//  <copyright file="NavigationTerm.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// A step of a feature chain, for example <c>.mass</c> in <c>subj.camera.mass</c>: the feature of that name of the part
    /// that its source gives.
    /// </summary>
    /// <param name="Source">The term that gives the part, for example <c>subj.camera</c>.</param>
    /// <param name="FeatureName">The name of the feature of the part, for example <c>mass</c>.</param>
    public sealed record NavigationTerm(Term Source, string FeatureName) : Term
    {
        /// <summary>
        /// Gets the value of the feature of the part that the source gives.
        /// </summary>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the model.</param>
        /// <returns>The value of the feature, or an error when the source is not a part or the feature has no value.</returns>
        public override ErrorOr<ModelValue> Evaluate(IEvaluationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return this.Source.Evaluate(context).Then(source => source is PartValue part
                ? context.Navigate(part, this.FeatureName)
                : Error.Validation(description: $"'{this.Source}' gives {source.Describe()}, which has no feature '{this.FeatureName}'."));
        }

        /// <summary>
        /// Writes the feature chain as in the SysML v2 textual notation.
        /// </summary>
        /// <returns>The feature chain, for example <c>subj.camera.mass</c>.</returns>
        public override string ToString()
        {
            return $"{this.Enclose(this.Source)}.{this.FeatureName}";
        }
    }
}
