// ------------------------------------------------------------------------------------------------
//  <copyright file="IEvaluationContext.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using ErrorOr;

    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;

    /// <summary>
    /// Gives the values of the model that a <see cref="Term"/> reads while it is evaluated.
    /// </summary>
    public interface IEvaluationContext
    {
        /// <summary>
        /// Gets the value of a feature that an expression references by name: the part that satisfies the requirement for
        /// its subject, a part of the model, the value of an attribute, or an enumeration value.
        /// </summary>
        /// <param name="feature">The referenced feature.</param>
        /// <returns>The value of the feature, or an error that tells why it has none.</returns>
        ErrorOr<ModelValue> GetValue(IFeature feature);

        /// <summary>
        /// Gets the value of a feature of a part, from its name, as <c>subj.camera</c> or <c>subj.mass</c> does: a sub-part,
        /// or the value of an attribute (its own value, or else the sum of the values of its sub-parts, as <c>sum_attribute</c>
        /// computes it).
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>The value of the feature, or an error that tells why it has none.</returns>
        ErrorOr<ModelValue> Navigate(PartValue part, string featureName);
    }
}
