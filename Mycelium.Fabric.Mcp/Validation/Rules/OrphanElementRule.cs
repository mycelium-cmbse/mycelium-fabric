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
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;

    /// <summary>
    /// The rule that finds the elements outside the containment tree of the model: an element without owner that is not a root
    /// namespace, for example after the membership that owned it has been removed. Only a <c>Namespace</c>, the metaclass of
    /// the root of a model, can have no owner.
    /// </summary>
    public class OrphanElementRule : BaseRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OrphanElementRule"/> class.
        /// </summary>
        public OrphanElementRule() : base("orphan-element", ValidationSeverity.Error,
            "An element has no owner and is not a root namespace, so it is outside the containment tree of the model.")
        {
        }

        /// <summary>
        /// Checks that an element has an owner, unless it is a root namespace.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the element, or <c>null</c> when it has an owner or is a root namespace.</returns>
        protected override string FindProblemCore(IElement element, ValidationContext context)
        {
            var hasOwner = element.OwningRelationship != null || element is IRelationship { OwningRelatedElement: not null };

            return hasOwner || element.GetType() == typeof(Namespace)
                ? null
                : $"The {element.GetType().Name} has no owner: the element or the relationship that owned it is not in the model.";
        }
    }
}
