// ------------------------------------------------------------------------------------------------
//  <copyright file="DuplicateQualifiedNameRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// The rule that finds the elements whose qualified name does not designate them alone, so that resolving it is ambiguous.
    /// The same name may be used in different namespaces, for example a <c>camera</c> part in two subsystems: their qualified
    /// names differ. Two root namespaces that each declare a package of the same name conflict, however. Inside a namespace,
    /// the metamodel gives no qualified name to a member whose name an earlier member already has: such a member is reported
    /// too.
    /// </summary>
    public class DuplicateQualifiedNameRule : BaseRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateQualifiedNameRule"/> class.
        /// </summary>
        public DuplicateQualifiedNameRule() : base("duplicate-qualified-name", ValidationSeverity.Error,
            "Several elements have the same qualified name, so resolving this name is ambiguous.")
        {
        }

        /// <summary>
        /// Checks that the qualified name of an element designates it alone.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the element, or <c>null</c> when it has no name or its qualified name designates it alone.</returns>
        protected override string FindProblemCore(IElement element, ValidationContext context)
        {
            var qualifiedName = element.qualifiedName;

            if (qualifiedName != null)
            {
                var count = context.CountElementsNamed(qualifiedName);

                return count > 1
                    ? $"{count} elements have the qualified name '{qualifiedName}', so resolving it is ambiguous: rename them so that each qualified name designates one element."
                    : null;
            }

            var owningNamespace = element.owningNamespace;

            if (string.IsNullOrEmpty(element.name) || owningNamespace == null)
            {
                return null;
            }

            var isRootMember = owningNamespace.owningNamespace == null;

            if (!isRootMember && owningNamespace.qualifiedName == null)
            {
                return null;
            }

            var owner = isRootMember ? "the root namespace" : $"'{owningNamespace.qualifiedName}'";

            return $"Another member of {owner} is already named '{element.name}', so this element has no qualified name and resolving its name gives the other one: rename one of them.";
        }
    }
}
