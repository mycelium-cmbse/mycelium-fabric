// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementChecker.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// Checks the requirements of a SysML v2 model: for each requirement with a constraint and a part that satisfies it,
    /// computes the value of the constrained attribute on the part, as <c>sum_attribute</c> does, and evaluates the constraint.
    /// </summary>
    public class RequirementChecker
    {
        /// <summary>
        /// The number of decimals of the values of the attributes, as for the totals of the budget tools.
        /// </summary>
        private const int ValueDecimalCount = 3;

        /// <summary>
        /// The explanation of a requirement that has no constraint.
        /// </summary>
        private const string NoConstraintExplanation = "The requirement has no constraint: add one with the SetConstraint change of apply_changes.";

        /// <summary>
        /// The explanation of a requirement that no part satisfies.
        /// </summary>
        private const string NoSatisfyingPartExplanation = "No part satisfies the requirement: add one with the Satisfy change of apply_changes.";

        /// <summary>
        /// The explanation of a requirement whose constraint has another form than the one built by <c>SetConstraint</c>.
        /// </summary>
        private const string UnsupportedConstraintExplanation =
            "The constraint is not a comparison between an attribute of the subject and a limit, with an optional margin, so it is not evaluated.";

        /// <summary>
        /// The elements of the model.
        /// </summary>
        private readonly IReadOnlyList<IElement> elements;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequirementChecker"/> class.
        /// </summary>
        /// <param name="elements">The elements of the model whose requirements are checked.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="elements"/> is <c>null</c>.
        /// </exception>
        public RequirementChecker(IEnumerable<IElement> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            this.elements = [.. elements];
        }

        /// <summary>
        /// Checks every requirement of the model, once for each part that satisfies it, or once when no part satisfies it.
        /// </summary>
        /// <returns>The <see cref="RequirementCheck"/>s, sorted by requirement identifier, then by name.</returns>
        public IReadOnlyList<RequirementCheck> CheckRequirements()
        {
            var satisfyingFeatures = this.GetSatisfyingFeatures();

            var checks = this.elements
                .OfType<IRequirementUsage>()
                .Where(requirement => requirement is not ISatisfyRequirementUsage)
                .SelectMany(requirement => CheckRequirement(requirement, [.. satisfyingFeatures[requirement]]));

            return OrderByRequirement(checks, check => check).ToList();
        }

        /// <summary>
        /// Evaluates the effect of another value of an attribute for one part on the requirements: those whose constraint
        /// is on this attribute and whose satisfying part gets a contribution from this part to its value.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the part whose value changes.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="newValue">The simulated value of the attribute for the part.</param>
        /// <returns>The <see cref="RequirementImpact"/>s, sorted by requirement identifier, then by name.</returns>
        public IReadOnlyList<RequirementImpact> EvaluateImpacts(Guid elementId, string attributeName, double newValue)
        {
            var impacts = this.GetSatisfyingFeatures()
                .SelectMany(requirementFeatures => requirementFeatures.Select(feature => EvaluateImpact(requirementFeatures.Key, feature, elementId, attributeName, newValue)))
                .Where(impact => impact != null);

            return OrderByRequirement(impacts, impact => impact.Current).ToList();
        }

        /// <summary>
        /// Gets the features that satisfy each requirement, from the satisfy links of the model that are not negated.
        /// </summary>
        /// <returns>The satisfying features, grouped by requirement.</returns>
        private ILookup<IRequirementUsage, IFeature> GetSatisfyingFeatures()
        {
            return this.elements
                .OfType<ISatisfyRequirementUsage>()
                .Where(satisfy => !satisfy.IsNegated && satisfy.satisfiedRequirement != null)
                .Select(satisfy => (Requirement: satisfy.satisfiedRequirement, Feature: satisfy.ResolveSatisfyingFeature()))
                .Where(satisfaction => satisfaction.Feature != null)
                .ToLookup(satisfaction => satisfaction.Requirement, satisfaction => satisfaction.Feature);
        }

        /// <summary>
        /// Checks a requirement against each part that satisfies it.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="satisfyingFeatures">The features that satisfy the requirement.</param>
        /// <returns>One <see cref="RequirementCheck"/> for each satisfying feature, or a single one when there is none.</returns>
        private static IEnumerable<RequirementCheck> CheckRequirement(IRequirementUsage requirement, IReadOnlyList<IFeature> satisfyingFeatures)
        {
            var requiredConstraints = requirement.requiredConstraint ?? [];
            var constraint = GetEvaluatedConstraint(requirement);
            var check = CreateCheck(requirement, constraint);

            if (requiredConstraints.Count == 0 || satisfyingFeatures.Count == 0)
            {
                var explanation = (requiredConstraints.Count, satisfyingFeatures.Count) switch
                {
                    (0, 0) => $"{NoConstraintExplanation} {NoSatisfyingPartExplanation}",
                    (0, _) => NoConstraintExplanation,
                    _ => NoSatisfyingPartExplanation
                };

                return ForEachSatisfyingFeature(check with { Status = RequirementStatus.NotVerifiable, Explanation = explanation }, satisfyingFeatures);
            }

            if (constraint == null)
            {
                var explanation = requiredConstraints.Count > 1
                    ? $"The requirement has {requiredConstraints.Count} required constraints, and only a single one is evaluated."
                    : UnsupportedConstraintExplanation;

                return ForEachSatisfyingFeature(check with { Status = RequirementStatus.NotEvaluated, Explanation = explanation }, satisfyingFeatures);
            }

            return satisfyingFeatures.Select(satisfyingFeature => CheckSatisfyingFeature(check, constraint, satisfyingFeature));
        }

        /// <summary>
        /// Repeats a check that does not depend on the satisfying features for each of them.
        /// </summary>
        /// <param name="check">The check of the requirement.</param>
        /// <param name="satisfyingFeatures">The features that satisfy the requirement.</param>
        /// <returns>The check for each satisfying feature, or the check alone when there is none.</returns>
        private static IEnumerable<RequirementCheck> ForEachSatisfyingFeature(RequirementCheck check, IReadOnlyList<IFeature> satisfyingFeatures)
        {
            return satisfyingFeatures.Count == 0 ? [check] : satisfyingFeatures.Select(satisfyingFeature => check with { SatisfiedBy = satisfyingFeature.qualifiedName });
        }

        /// <summary>
        /// Checks the constraint of a requirement against the value of its attribute on a satisfying feature.
        /// </summary>
        /// <param name="check">The check of the requirement, without outcome.</param>
        /// <param name="constraint">The constraint of the requirement.</param>
        /// <param name="satisfyingFeature">The feature that satisfies the requirement.</param>
        /// <returns>The completed <see cref="RequirementCheck"/>.</returns>
        private static RequirementCheck CheckSatisfyingFeature(RequirementCheck check, AttributeConstraint constraint, IFeature satisfyingFeature)
        {
            var contributions = satisfyingFeature.CollectContributions(constraint.Attribute);
            check = check with { SatisfiedBy = satisfyingFeature.qualifiedName };

            if (contributions.Count == 0)
            {
                var explanation = $"No part under '{satisfyingFeature.qualifiedName}' has a numeric value for '{constraint.Attribute}'.";

                return check with { Status = RequirementStatus.NotEvaluated, Explanation = explanation };
            }

            return Evaluate(check, constraint, Math.Round(contributions.Sum(contribution => contribution.Value), ValueDecimalCount));
        }

        /// <summary>
        /// Evaluates the effect of another value of an attribute for one part on the check of a requirement.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="satisfyingFeature">A feature that satisfies the requirement.</param>
        /// <param name="elementId">The <c>Id</c> of the part whose value changes.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="newValue">The simulated value of the attribute for the part.</param>
        /// <returns>
        /// The <see cref="RequirementImpact"/>, or <c>null</c> when the requirement is not evaluated, constrains another
        /// attribute, or gets no contribution from the part.
        /// </returns>
        private static RequirementImpact EvaluateImpact(IRequirementUsage requirement, IFeature satisfyingFeature, Guid elementId, string attributeName, double newValue)
        {
            var constraint = GetEvaluatedConstraint(requirement);

            if (constraint?.Attribute != attributeName)
            {
                return null;
            }

            var contributions = satisfyingFeature.CollectContributions(attributeName);
            var changedContribution = contributions.FirstOrDefault(contribution => contribution.Id == elementId);

            if (changedContribution == null)
            {
                return null;
            }

            var currentValue = Math.Round(contributions.Sum(contribution => contribution.Value), ValueDecimalCount);
            var simulatedValue = Math.Round(currentValue - changedContribution.Value + newValue, ValueDecimalCount);
            var check = CreateCheck(requirement, constraint) with { SatisfiedBy = satisfyingFeature.qualifiedName };

            return new RequirementImpact(Evaluate(check, constraint, currentValue), Evaluate(check, constraint, simulatedValue));
        }

        /// <summary>
        /// Gets the constraint of a requirement that the checker can evaluate: its single required constraint, when it has
        /// the form of an <see cref="AttributeConstraint"/>.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <returns>The <see cref="AttributeConstraint"/>, or <c>null</c> when the requirement has none to evaluate.</returns>
        private static AttributeConstraint GetEvaluatedConstraint(IRequirementUsage requirement)
        {
            var requiredConstraints = requirement.requiredConstraint ?? [];

            return requiredConstraints.Count == 1 ? requiredConstraints[0].GetAttributeConstraint() : null;
        }

        /// <summary>
        /// Creates the check of a requirement, without outcome.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="constraint">The constraint of the requirement, or <c>null</c>.</param>
        /// <returns>The new <see cref="RequirementCheck"/>.</returns>
        private static RequirementCheck CreateCheck(IRequirementUsage requirement, AttributeConstraint constraint)
        {
            return new RequirementCheck
            {
                Id = requirement.Id,
                ReqId = requirement.ReqId,
                Name = requirement.DeclaredName,
                Constraint = constraint?.ToString()
            };
        }

        /// <summary>
        /// Completes the check of a requirement with the evaluation of its constraint for a value.
        /// </summary>
        /// <param name="check">The check of the requirement, without outcome.</param>
        /// <param name="constraint">The constraint of the requirement.</param>
        /// <param name="value">The value of the constrained attribute.</param>
        /// <returns>The completed <see cref="RequirementCheck"/>.</returns>
        private static RequirementCheck Evaluate(RequirementCheck check, AttributeConstraint constraint, double value)
        {
            var evaluation = constraint.Evaluate(value);

            return check with
            {
                Status = evaluation.IsSatisfied ? RequirementStatus.Satisfied : RequirementStatus.NotSatisfied,
                Value = value,
                Gap = evaluation.Gap,
                Explanation = evaluation.Comparison
            };
        }

        /// <summary>
        /// Sorts items by the requirement of their check: first the requirements that have an identifier, by identifier,
        /// then by name and by satisfying part.
        /// </summary>
        /// <typeparam name="T">The type of the items.</typeparam>
        /// <param name="items">The items to sort.</param>
        /// <param name="getCheck">Gets the check of an item.</param>
        /// <returns>The sorted items.</returns>
        private static IOrderedEnumerable<T> OrderByRequirement<T>(IEnumerable<T> items, Func<T, RequirementCheck> getCheck)
        {
            return items
                .OrderBy(item => getCheck(item).ReqId == null)
                .ThenBy(item => getCheck(item).ReqId, StringComparer.Ordinal)
                .ThenBy(item => getCheck(item).Name, StringComparer.Ordinal)
                .ThenBy(item => getCheck(item).SatisfiedBy, StringComparer.Ordinal);
        }
    }
}
