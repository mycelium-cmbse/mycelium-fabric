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
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;

    /// <summary>
    /// The rule that finds the definitions that take no part in the model: no feature is typed by them, and no other type
    /// specializes them. They are often left over after a change.
    /// </summary>
    public class UnusedDefinitionRule : BaseRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnusedDefinitionRule"/> class.
        /// </summary>
        public UnusedDefinitionRule() : base("unused-definition", ValidationSeverity.Information,
            "No feature is typed by a definition and nothing specializes it, so it takes no part in the model.")
        {
        }

        /// <summary>
        /// Checks that a definition types a feature or is specialized.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the definition, or <c>null</c> when the element is not an unused definition.</returns>
        protected override string FindProblemCore(IElement element, ValidationContext context)
        {
            return element is IDefinition && !context.IsSpecialized(element)
                ? $"No feature is typed by the {element.GetType().Name} and nothing specializes it: use it, or remove it if it is left over."
                : null;
        }
    }
}
