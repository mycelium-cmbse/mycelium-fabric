// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelEvaluationContext.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Expressions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Enumerations;

    /// <summary>
    /// Gives the values of the model to the evaluation of the constraints of a requirement: the subject of the requirement
    /// stands for the part that satisfies it, and the value of an attribute of a part is its own value, or else the sum of
    /// the values of its sub-parts, as <c>sum_attribute</c> computes it. It records these sums, so that the checker knows
    /// which values a constraint depends on.
    /// </summary>
    public sealed class ModelEvaluationContext : IEvaluationContext
    {
        /// <summary>
        /// The subject of the requirement, or <c>null</c>.
        /// </summary>
        private readonly IFeature subject;

        /// <summary>
        /// The part that satisfies the requirement, or <c>null</c>.
        /// </summary>
        private readonly IFeature satisfyingFeature;

        /// <summary>
        /// The simulated value of an attribute for one part, or <c>null</c>.
        /// </summary>
        private readonly ValueOverride valueOverride;

        /// <summary>
        /// The sums computed by the evaluation.
        /// </summary>
        private readonly List<RollUp> rollUps = [];

        /// <summary>
        /// The <c>Id</c>s of the features whose value is being evaluated, to detect a value that depends on itself.
        /// </summary>
        private readonly HashSet<Guid> featuresBeingEvaluated = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelEvaluationContext"/> class.
        /// </summary>
        /// <param name="subject">The subject of the requirement, or <c>null</c>.</param>
        /// <param name="satisfyingFeature">The part that satisfies the requirement, or <c>null</c>.</param>
        /// <param name="valueOverride">The simulated value of an attribute for one part, or <c>null</c>.</param>
        public ModelEvaluationContext(IFeature subject, IFeature satisfyingFeature, ValueOverride valueOverride = null)
        {
            this.subject = subject;
            this.satisfyingFeature = satisfyingFeature;
            this.valueOverride = valueOverride;
        }

        /// <summary>
        /// Gets the sums computed by the evaluation, in order.
        /// </summary>
        public IReadOnlyList<RollUp> RollUps => this.rollUps;

        /// <summary>
        /// Gets the value of a referenced feature: the satisfying part for the subject, an enumeration value, the value of an
        /// attribute (its own value, or else the sum over the sub-parts of its owner), or the feature itself for a part.
        /// </summary>
        /// <param name="feature">The referenced feature.</param>
        /// <returns>The value, or an error that tells why the feature has none.</returns>
        public ErrorOr<ModelValue> GetValue(IFeature feature)
        {
            ArgumentNullException.ThrowIfNull(feature);

            if (this.subject != null && feature.Id == this.subject.Id)
            {
                return this.satisfyingFeature == null
                    ? Error.Validation(description: $"No part satisfies the requirement, so its subject '{feature.DeclaredName}' has no value.")
                    : new PartValue(this.satisfyingFeature);
            }

            if (IsEnumerationValue(feature))
            {
                return new EnumValue(feature.DeclaredName);
            }

            if (TermReader.GetValueExpression(feature) is { } valueExpression)
            {
                return this.EvaluateValue(feature, valueExpression);
            }

            if (feature is not IAttributeUsage)
            {
                return new PartValue(feature);
            }

            // An attribute without value, such as the mass of a part, is the sum over the sub-parts of its owner; the owner is
            // the satisfying part when it is the subject.
            if (feature.owningType is not IFeature owner || feature.DeclaredName == null)
            {
                return Error.Validation(description: $"'{feature.DeclaredName}' has no value.");
            }

            return this.subject != null && owner.Id == this.subject.Id
                ? this.GetValue(this.subject).Then(subjectValue => this.RollUp(((PartValue)subjectValue).Feature, feature.DeclaredName))
                : this.RollUp(owner, feature.DeclaredName);
        }

        /// <summary>
        /// Gets the value of a feature of a part, from its name: a sub-part, the value of an attribute that is not a plain
        /// number (a Boolean, a text, an enumeration value, an expression), or else the sum of the values of the attribute
        /// over the part and its sub-parts.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>The value, or an error that tells why the feature has none.</returns>
        public ErrorOr<ModelValue> Navigate(PartValue part, string featureName)
        {
            ArgumentNullException.ThrowIfNull(part);

            var member = (part.Feature.feature ?? []).FirstOrDefault(candidate => candidate.DeclaredName == featureName);

            if (member is not null and not IAttributeUsage)
            {
                return new PartValue(member);
            }

            if (member is IAttributeUsage attribute && TermReader.GetValueExpression(attribute) is { } valueExpression && attribute.GetNumericValue() == null)
            {
                return this.EvaluateValue(attribute, valueExpression);
            }

            return this.RollUp(part.Feature, featureName);
        }

        /// <summary>
        /// Tells whether a feature is a value of an enumeration, such as <c>sunSynchronous</c> in <c>enum def OrbitKind</c>.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <returns><c>true</c> when the feature is an enumeration value.</returns>
        public static bool IsEnumerationValue(IFeature feature)
        {
            ArgumentNullException.ThrowIfNull(feature);

            return feature is IEnumerationUsage && feature.owningNamespace is IEnumerationDefinition;
        }

        /// <summary>
        /// Evaluates the expression of the value of a feature.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <param name="valueExpression">The expression of its value.</param>
        /// <returns>The value, or an error when it cannot be evaluated or depends on itself.</returns>
        private ErrorOr<ModelValue> EvaluateValue(IFeature feature, IElement valueExpression)
        {
            if (!this.featuresBeingEvaluated.Add(feature.Id))
            {
                return Error.Validation(description: $"The value of '{feature.DeclaredName}' depends on itself.");
            }

            var value = TermReader.Read(valueExpression).Then(term => term.Evaluate(this));
            this.featuresBeingEvaluated.Remove(feature.Id);

            return value;
        }

        /// <summary>
        /// Computes the value of an attribute of a part as <c>sum_attribute</c> does, with the simulated value of one part
        /// if any, and records it.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="attributeName">The name of the attribute.</param>
        /// <returns>The value, or an error when no part under the part has a numeric value for it.</returns>
        private ErrorOr<ModelValue> RollUp(IFeature part, string attributeName)
        {
            var contributions = part.CollectContributions(attributeName)
                .Select(contribution => this.valueOverride != null && this.valueOverride.ElementId == contribution.Id && this.valueOverride.Attribute == attributeName
                    ? contribution with { Value = this.valueOverride.Value }
                    : contribution)
                .ToList();

            if (contributions.Count == 0)
            {
                return Error.Validation(description: $"No part under '{part.qualifiedName}' has a numeric value for '{attributeName}'.");
            }

            var total = NumberValue.Sum(contributions.Select(contribution => new NumberValue(contribution.Value, contribution.Unit)));

            if (total.IsError)
            {
                return total.Errors;
            }

            this.rollUps.Add(new RollUp(part, attributeName, total.Value, contributions));

            return total.Value;
        }
    }
}
