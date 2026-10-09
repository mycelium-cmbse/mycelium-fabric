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

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// Checks the requirements of a SysML v2 model: for each requirement with a required constraint and a part that
    /// satisfies it, evaluates its assumptions and its required constraints with the values of this part, any expression of
    /// the SysML v2 expression language (numbers with units, Booleans, texts, enumeration values, feature chains, logical
    /// operators, library functions).
    /// </summary>
    public class RequirementChecker
    {
        /// <summary>
        /// The explanation of a requirement that has no required constraint.
        /// </summary>
        private const string NoConstraintExplanation = "The requirement has no required constraint: add one with the SetConstraint change of apply_changes.";

        /// <summary>
        /// The explanation of a requirement that no part satisfies.
        /// </summary>
        private const string NoSatisfyingPartExplanation = "No part satisfies the requirement: add one with the Satisfy change of apply_changes.";

        /// <summary>
        /// How a constraint whose expression cannot be read is written in a check.
        /// </summary>
        private const string UnreadableExpression = "(unreadable)";

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
        /// Evaluates the effect of another value of an attribute for one part on the requirements: those whose constraints
        /// read this value, through a sum over a satisfying part that includes this part.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the part whose value changes.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="newValue">The simulated value of the attribute for the part, in the unit of its current value.</param>
        /// <returns>The <see cref="RequirementImpact"/>s, sorted by requirement identifier, then by name.</returns>
        public IReadOnlyList<RequirementImpact> EvaluateImpacts(Guid elementId, string attributeName, double newValue)
        {
            var valueOverride = new ValueOverride(elementId, attributeName, newValue);

            var impacts = this.GetSatisfyingFeatures()
                .SelectMany(requirementFeatures => requirementFeatures.Select(feature => EvaluateImpact(requirementFeatures.Key, feature, valueOverride)))
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
            var constraints = ReadConstraints(requirement);
            var check = CreateCheck(requirement, constraints);

            if (constraints.Required.Count == 0 || satisfyingFeatures.Count == 0)
            {
                var explanation = (constraints.Required.Count, satisfyingFeatures.Count) switch
                {
                    (0, 0) => $"{NoConstraintExplanation} {NoSatisfyingPartExplanation}",
                    (0, _) => NoConstraintExplanation,
                    _ => NoSatisfyingPartExplanation
                };

                return ForEachSatisfyingFeature(check with { Status = RequirementStatus.NotVerifiable, Explanation = explanation }, satisfyingFeatures);
            }

            return satisfyingFeatures.Select(satisfyingFeature =>
                Evaluate(check with { SatisfiedBy = satisfyingFeature.qualifiedName }, constraints, new ModelEvaluationContext(constraints.Subject, satisfyingFeature)));
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
        /// Evaluates the effect of another value of an attribute for one part on the check of a requirement.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="satisfyingFeature">A feature that satisfies the requirement.</param>
        /// <param name="valueOverride">The simulated value.</param>
        /// <returns>
        /// The <see cref="RequirementImpact"/>, or <c>null</c> when the requirement has no required constraint or does not
        /// read the simulated value.
        /// </returns>
        private static RequirementImpact EvaluateImpact(IRequirementUsage requirement, IFeature satisfyingFeature, ValueOverride valueOverride)
        {
            var constraints = ReadConstraints(requirement);

            if (constraints.Required.Count == 0)
            {
                return null;
            }

            var check = CreateCheck(requirement, constraints) with { SatisfiedBy = satisfyingFeature.qualifiedName };
            var currentContext = new ModelEvaluationContext(constraints.Subject, satisfyingFeature);
            var currentCheck = Evaluate(check, constraints, currentContext);

            var readsValue = currentContext.RollUps.Any(rollUp =>
                rollUp.Attribute == valueOverride.Attribute && rollUp.Contributions.Any(contribution => contribution.Id == valueOverride.ElementId));

            if (!readsValue)
            {
                return null;
            }

            var newCheck = Evaluate(check, constraints, new ModelEvaluationContext(constraints.Subject, satisfyingFeature, valueOverride));

            return new RequirementImpact(currentCheck, newCheck);
        }

        /// <summary>
        /// Reads the subject, the assumptions and the required constraints of a requirement.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <returns>The <see cref="RequirementConstraints"/>.</returns>
        private static RequirementConstraints ReadConstraints(IRequirementUsage requirement)
        {
            return new RequirementConstraints(requirement.subjectParameter,
                [.. (requirement.assumedConstraint ?? []).Select(constraint => constraint.GetTerm())],
                [.. (requirement.requiredConstraint ?? []).Select(constraint => constraint.GetTerm())]);
        }

        /// <summary>
        /// Creates the check of a requirement, without outcome.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="constraints">The constraints of the requirement.</param>
        /// <returns>The new <see cref="RequirementCheck"/>.</returns>
        private static RequirementCheck CreateCheck(IRequirementUsage requirement, RequirementConstraints constraints)
        {
            return new RequirementCheck
            {
                Id = requirement.Id,
                ReqId = requirement.ReqId,
                Name = requirement.DeclaredName,
                Constraint = JoinExpressions(constraints.Required),
                Assumption = JoinExpressions(constraints.Assumptions)
            };
        }

        /// <summary>
        /// Completes the check of a requirement with the evaluation of its constraints. As in the <c>RequirementCheck</c> of
        /// the SysML v2 library, the requirement holds when its assumptions imply its required constraints: when the
        /// required constraints hold, or when one of the assumptions does not.
        /// </summary>
        /// <param name="check">The check of the requirement, without outcome.</param>
        /// <param name="constraints">The constraints of the requirement.</param>
        /// <param name="context">The <see cref="ModelEvaluationContext"/> that gives the values of the satisfying part.</param>
        /// <returns>The completed <see cref="RequirementCheck"/>.</returns>
        private static RequirementCheck Evaluate(RequirementCheck check, RequirementConstraints constraints, ModelEvaluationContext context)
        {
            var assumptions = constraints.Assumptions.Select(term => EvaluateConstraint(term, context)).ToList();
            var assumptionRollUpCount = context.RollUps.Count;
            var required = constraints.Required.Select(term => EvaluateConstraint(term, context)).ToList();

            var status = (AllHold(required), AllHold(assumptions)) switch
            {
                (true, _) or (_, false) => RequirementStatus.Satisfied,
                (false, true) => RequirementStatus.NotSatisfied,
                _ => RequirementStatus.NotEvaluated
            };

            // The value of the check is the value that the required constraints read, when they read a single one.
            var readValues = context.RollUps
                .Skip(assumptionRollUpCount)
                .DistinctBy(rollUp => (rollUp.Part.Id, rollUp.Attribute))
                .ToList();

            var value = readValues.Count == 1 ? readValues[0].Total : null;

            return check with
            {
                Status = status,
                Value = value?.Number,
                Unit = value?.Unit,
                Gap = required.Count == 1 ? required[0].Gap : null,
                Explanation = Explain(assumptions, required)
            };
        }

        /// <summary>
        /// Evaluates one constraint of a requirement.
        /// </summary>
        /// <param name="term">The expression of the constraint, or the error that tells why it cannot be read.</param>
        /// <param name="context">The <see cref="IEvaluationContext"/> that gives the values of the satisfying part.</param>
        /// <returns>The <see cref="ConstraintOutcome"/>.</returns>
        private static ConstraintOutcome EvaluateConstraint(ErrorOr<Term> term, IEvaluationContext context)
        {
            if (term.IsError)
            {
                return new ConstraintOutcome(null, null, null, $"The constraint cannot be read: {term.FirstError.Description}");
            }

            var value = term.Value.Evaluate(context);

            if (value.IsError)
            {
                return new ConstraintOutcome(term.Value, null, null, value.FirstError.Description);
            }

            return value.Value is BooleanValue boolean
                ? new ConstraintOutcome(term.Value, boolean.Value, boolean.Gap, boolean.Explanation ?? $"{term.Value} is {boolean}")
                : new ConstraintOutcome(term.Value, null, null, $"The constraint gives {value.Value.KindName} ({value.Value}), not a Boolean.");
        }

        /// <summary>
        /// Tells whether all the constraints hold, in a logic with three values.
        /// </summary>
        /// <param name="outcomes">The outcomes of the constraints.</param>
        /// <returns>
        /// <c>false</c> when one constraint does not hold, otherwise <c>null</c> when one is not evaluated, otherwise <c>true</c>
        /// (also when there is none).
        /// </returns>
        private static bool? AllHold(IReadOnlyList<ConstraintOutcome> outcomes)
        {
            var holds = outcomes.Select(outcome => outcome.Holds).ToList();

            if (holds.Contains(false))
            {
                return false;
            }

            return holds.Contains(null) ? null : true;
        }

        /// <summary>
        /// Writes the explanation of a check: the outcome of its single required constraint, or the outcome of each of its
        /// assumptions and required constraints.
        /// </summary>
        /// <param name="assumptions">The outcomes of the assumptions.</param>
        /// <param name="required">The outcomes of the required constraints.</param>
        /// <returns>The explanation, for example <c>150.96 &gt; 150</c>.</returns>
        private static string Explain(IReadOnlyList<ConstraintOutcome> assumptions, IReadOnlyList<ConstraintOutcome> required)
        {
            if (assumptions.Count == 0 && required.Count == 1)
            {
                return required[0].Explanation;
            }

            var outcomes = assumptions
                .Select(outcome => $"assume {Describe(outcome)}")
                .Concat(required.Select(outcome => $"require {Describe(outcome)}"));

            return string.Join("; ", outcomes);
        }

        /// <summary>
        /// Writes a constraint and its outcome.
        /// </summary>
        /// <param name="outcome">The outcome of the constraint.</param>
        /// <returns>The constraint and its outcome, for example <c>subj.mass * 1.2 &lt;= 150: 150.96 &gt; 150</c>.</returns>
        private static string Describe(ConstraintOutcome outcome)
        {
            return $"{outcome.Term?.ToString() ?? UnreadableExpression}: {outcome.Explanation}";
        }

        /// <summary>
        /// Writes constraints as their expressions joined by <c>and</c>.
        /// </summary>
        /// <param name="terms">The expressions of the constraints, or the errors that tell why they cannot be read.</param>
        /// <returns>The expressions, or <c>null</c> when there is no constraint.</returns>
        private static string JoinExpressions(IReadOnlyList<ErrorOr<Term>> terms)
        {
            return terms.Count == 0 ? null : string.Join(" and ", terms.Select(term => term.IsError ? UnreadableExpression : term.Value.ToString()));
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

        /// <summary>
        /// The subject and the constraints of a requirement.
        /// </summary>
        /// <param name="Subject">The subject of the requirement, or <c>null</c> when it has none.</param>
        /// <param name="Assumptions">The expressions of the assumptions, or the errors that tell why they cannot be read.</param>
        /// <param name="Required">The expressions of the required constraints, or the errors that tell why they cannot be read.</param>
        private sealed record RequirementConstraints(IFeature Subject, IReadOnlyList<ErrorOr<Term>> Assumptions, IReadOnlyList<ErrorOr<Term>> Required);

        /// <summary>
        /// The outcome of the evaluation of one constraint of a requirement.
        /// </summary>
        /// <param name="Term">The expression of the constraint, or <c>null</c> when it cannot be read.</param>
        /// <param name="Holds">Whether the constraint holds, or <c>null</c> when it is not evaluated.</param>
        /// <param name="Gap">How far the constraint is from its limit, for a comparison of numbers, or <c>null</c>.</param>
        /// <param name="Explanation">Why the constraint holds or not, for example <c>150.96 &gt; 150</c>, or why it is not evaluated.</param>
        private sealed record ConstraintOutcome(Term Term, bool? Holds, double? Gap, string Explanation);
    }
}
