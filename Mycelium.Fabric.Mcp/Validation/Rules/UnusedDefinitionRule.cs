// ------------------------------------------------------------------------------------------------
//  <copyright file="UnusedDefinitionRule.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;

    /// <summary>
    /// The rule that finds the definitions that take no part in the model: no feature is typed by them, and no other type
    /// specializes them. They are often left over after a change.
    /// </summary>
    public class UnusedDefinitionRule : IValidationRule
    {
        /// <summary>
        /// Gets the name of the rule.
        /// </summary>
        public string Name => "unused-definition";

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public ValidationSeverity Severity => ValidationSeverity.Information;

        /// <summary>
        /// Gets the description of what the rule checks.
        /// </summary>
        public string Description => "No feature is typed by a definition and nothing specializes it, so it takes no part in the model.";

        /// <summary>
        /// Checks that a definition types a feature or is specialized.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the definition, or <c>null</c> when the element is not an unused definition.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        public string FindProblem(IElement element, ValidationContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            return element is IDefinition && !context.IsSpecialized(element)
                ? $"No feature is typed by the {element.GetType().Name} and nothing specializes it: use it, or remove it if it is left over."
                : null;
        }
    }
}
