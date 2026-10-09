// ------------------------------------------------------------------------------------------------
//  <copyright file="BrokenReferenceRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// The rule that finds the relationships that refer to an element that is not in the model, for example the typing of a
    /// part after its definition has been removed. SysML2.NET leaves such a reference empty, or cannot compute the elements
    /// that the relationship relates. A relationship without owner is left to the <see cref="OrphanElementRule"/>.
    /// </summary>
    public class BrokenReferenceRule : IValidationRule
    {
        /// <summary>
        /// Gets the name of the rule.
        /// </summary>
        public string Name => "broken-reference";

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public ValidationSeverity Severity => ValidationSeverity.Error;

        /// <summary>
        /// Gets the description of what the rule checks.
        /// </summary>
        public string Description => "A relationship refers to an element that is not in the model, for example the type of a part whose definition has been removed.";

        /// <summary>
        /// Checks that a relationship refers only to elements of the model.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the relationship, or <c>null</c> when the element is not an owned relationship that is broken.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        public string FindProblem(IElement element, ValidationContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            if (element is not IRelationship { OwningRelatedElement: not null } relationship)
            {
                return null;
            }

            var problem = $"The {relationship.GetType().Name} refers to an element that is not in the model: it has been removed, or it was never loaded.";

            try
            {
                return IsMissing(relationship.Source) || IsMissing(relationship.Target) || (relationship.relatedElement ?? []).Contains(null) ? problem : null;
            }
            catch (IncompleteModelException)
            {
                return problem;
            }
        }

        /// <summary>
        /// Tells whether an end of a relationship, its sources or its targets, misses an element. SysML2.NET leaves out the
        /// elements that are not in the model, so that the end is empty.
        /// </summary>
        /// <param name="relatedElements">The elements at the end of the relationship.</param>
        /// <returns><c>true</c> when the end is empty or holds no element.</returns>
        private static bool IsMissing(List<IElement> relatedElements)
        {
            return relatedElements == null || relatedElements.Count == 0 || relatedElements.Contains(null);
        }
    }
}
