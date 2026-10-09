// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelValidator.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// An <see cref="IModelValidator"/> that checks the <see cref="IValidationRule"/>s registered in the dependency injection
    /// container. It holds no state, so that one instance serves all the calls.
    /// </summary>
    public class ModelValidator : IModelValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModelValidator"/> class.
        /// </summary>
        /// <param name="rules">The rules to check.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="rules"/> is <c>null</c>.
        /// </exception>
        public ModelValidator(IEnumerable<IValidationRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            this.Rules = [.. rules.OrderBy(rule => rule.Severity).ThenBy(rule => rule.Name, StringComparer.Ordinal)];
        }

        /// <summary>
        /// Gets the rules that the validator checks, the most serious first, then sorted by name.
        /// </summary>
        public IReadOnlyList<IValidationRule> Rules { get; }

        /// <summary>
        /// Checks each element of a model against each rule. A rule that cannot check an element, because SysML2.NET cannot
        /// compute a derived property of an incomplete model, skips it.
        /// </summary>
        /// <param name="elements">All the elements of the model.</param>
        /// <returns>The issues found, the most serious first, then sorted by rule and by element.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="elements"/> is <c>null</c>.
        /// </exception>
        public IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<IElement> elements)
        {
            ArgumentNullException.ThrowIfNull(elements);

            var context = new ValidationContext(elements);

            return elements
                .SelectMany(element => this.Rules
                    .Select(rule => (Rule: rule, Problem: FindProblem(rule, element, context)))
                    .Where(result => result.Problem != null)
                    .Select(result => new ValidationIssue(result.Rule.Name, result.Rule.Severity, element.Id, Describe(element), result.Problem)))
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.Rule, StringComparer.Ordinal)
                .ThenBy(issue => issue.Element, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Checks an element against a rule.
        /// </summary>
        /// <param name="rule">The <see cref="IValidationRule"/> to check.</param>
        /// <param name="element">The element to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem found by the rule, or <c>null</c> when it finds none or cannot check the element.</returns>
        private static string FindProblem(IValidationRule rule, IElement element, ValidationContext context)
        {
            try
            {
                return rule.FindProblem(element, context);
            }
            catch (IncompleteModelException)
            {
                return null;
            }
        }

        /// <summary>
        /// Describes an element in an issue: its qualified name, or its name when the qualified name cannot be computed. An
        /// element without name, such as a relationship, is described by its metaclass and the element that owns it, or else
        /// the named element that it owns.
        /// </summary>
        /// <param name="element">The element to describe.</param>
        /// <returns>The description of the element.</returns>
        private static string Describe(IElement element)
        {
            var name = GetQualifiedName(element) ?? element.DeclaredName;

            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            var metaclass = element.GetType().Name;
            var owner = element is IRelationship { OwningRelatedElement: { } owningRelatedElement } ? owningRelatedElement : element.owner;

            if (owner != null)
            {
                return $"{metaclass} in {Describe(owner)}";
            }

            var ownedElementName = element is IRelationship { OwnedRelatedElement: [var ownedElement, ..] } ? ownedElement?.DeclaredName : null;

            return ownedElementName == null ? metaclass : $"{metaclass} of {ownedElementName}";
        }

        /// <summary>
        /// Gets the qualified name of an element. SysML2.NET cannot compute it when a namespace that contains the element has
        /// a membership whose member is not in the model.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The qualified name of the element, or <c>null</c> when it has none or it cannot be computed.</returns>
        private static string GetQualifiedName(IElement element)
        {
            try
            {
                return element.qualifiedName;
            }
            catch (IncompleteModelException)
            {
                return null;
            }
        }
    }
}
