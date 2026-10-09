// ------------------------------------------------------------------------------------------------
//  <copyright file="DuplicateNameRule.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.POCO.Root.Namespaces;

    /// <summary>
    /// The rule that finds the namespaces in which two members have the same name or short name, which breaks the
    /// <c>validateNamespaceDistinguishability</c> constraint of the metamodel: a reference to such a name is ambiguous. The
    /// short name of a requirement is its <c>reqId</c>.
    /// </summary>
    public class DuplicateNameRule : IValidationRule
    {
        /// <summary>
        /// Gets the name of the rule.
        /// </summary>
        public string Name => "duplicate-name";

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public ValidationSeverity Severity => ValidationSeverity.Error;

        /// <summary>
        /// Gets the description of what the rule checks.
        /// </summary>
        public string Description => "Two members of a namespace have the same name or short name (reqId for a requirement), so a reference to this name is ambiguous.";

        /// <summary>
        /// Checks that the members of a namespace have distinct names and short names.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the namespace, or <c>null</c> when the element is not a namespace with duplicate names.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        public string FindProblem(IElement element, ValidationContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            if (element is not INamespace @namespace)
            {
                return null;
            }

            var duplicateNames = (@namespace.ownedMembership ?? [])
                .SelectMany(GetNames)
                .CountBy(name => name, StringComparer.Ordinal)
                .Where(nameCount => nameCount.Value > 1)
                .Select(nameCount => $"'{nameCount.Key}'")
                .ToList();

            return duplicateNames.Count == 0 ? null : $"Several members are named {string.Join(", ", duplicateNames)}: rename them so that each name designates one member.";
        }

        /// <summary>
        /// Gets the names by which a membership makes its member known: its name and its short name.
        /// </summary>
        /// <param name="membership">The <see cref="IMembership"/>.</param>
        /// <returns>The distinct names of the member, without the empty ones.</returns>
        private static IEnumerable<string> GetNames(IMembership membership)
        {
            string[] names = [membership.MemberName ?? membership.MemberElement?.DeclaredName, membership.MemberShortName ?? membership.MemberElement?.shortName];

            return names
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.Ordinal);
        }
    }
}
