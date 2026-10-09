// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using System;
    using System.Linq;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// The base class of the rules that check the requirements of the model. They check the requirements stated in the model,
    /// not the satisfy links, which are requirements too, nor the requirements declared inside a definition.
    /// </summary>
    public abstract class RequirementRule : IValidationRule
    {
        /// <summary>
        /// Gets the name of the rule.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public abstract ValidationSeverity Severity { get; }

        /// <summary>
        /// Gets the description of what the rule checks.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Checks an element when it is a requirement stated in the model.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the requirement, or <c>null</c> when the element is not a requirement or has no problem.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="element"/> or <paramref name="context"/> is <c>null</c>.
        /// </exception>
        public string FindProblem(IElement element, ValidationContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            return element is IRequirementUsage requirement and not ISatisfyRequirementUsage && requirement.owner is not IDefinition
                ? this.FindRequirementProblem(requirement, context)
                : null;
        }

        /// <summary>
        /// Tells whether a requirement groups other requirements, which then carry the constraints and the satisfy links.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <returns><c>true</c> when the requirement owns other requirements.</returns>
        protected static bool IsGroup(IRequirementUsage requirement)
        {
            return (((IType)requirement).ownedFeature ?? []).Any(feature => feature is IRequirementUsage and not ISatisfyRequirementUsage);
        }

        /// <summary>
        /// Checks a requirement stated in the model.
        /// </summary>
        /// <param name="requirement">The requirement to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the requirement, or <c>null</c> when the rule finds none.</returns>
        protected abstract string FindRequirementProblem(IRequirementUsage requirement, ValidationContext context);
    }
}
