// ------------------------------------------------------------------------------------------------
//  <copyright file="OrphanElementRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using System;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;

    /// <summary>
    /// The rule that finds the elements outside the containment tree of the model: an element without owner that is not a root
    /// namespace, for example after the membership that owned it has been removed. Only a <c>Namespace</c>, the metaclass of
    /// the root of a model, can have no owner.
    /// </summary>
    public class OrphanElementRule : IValidationRule
    {
        /// <summary>
        /// Gets the name of the rule.
        /// </summary>
        public string Name => "orphan-element";

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public ValidationSeverity Severity => ValidationSeverity.Error;

        /// <summary>
        /// Gets the description of what the rule checks.
        /// </summary>
        public string Description => "An element has no owner and is not a root namespace, so it is outside the containment tree of the model.";

        /// <summary>
        /// Checks that an element has an owner, unless it is a root namespace.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the element, or <c>null</c> when it has an owner or is a root namespace.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        public string FindProblem(IElement element, ValidationContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            var hasOwner = element.OwningRelationship != null || element is IRelationship { OwningRelatedElement: not null };

            return hasOwner || element.GetType() == typeof(Namespace)
                ? null
                : $"The {element.GetType().Name} has no owner: the element or the relationship that owned it is not in the model.";
        }
    }
}
