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
    using System.Linq;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// The base class of the rules that check the requirements of the model. They check the requirements stated in the model,
    /// not the satisfy links, which are requirements too, nor the requirements declared inside a definition.
    /// </summary>
    public abstract class RequirementRule : BaseRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequirementRule"/> class.
        /// </summary>
        /// <param name="name">The name of the rule, in kebab case.</param>
        /// <param name="severity">The <see cref="ValidationSeverity"/> of the issues found by the rule.</param>
        /// <param name="description">The description of what the rule checks, in one sentence.</param>
        protected RequirementRule(string name, ValidationSeverity severity, string description) : base(name, severity, description)
        {
        }

        /// <summary>
        /// Checks an element when it is a requirement stated in the model.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the requirement, or <c>null</c> when the element is not a requirement or has no problem.</returns>
        protected sealed override string FindProblemCore(IElement element, ValidationContext context)
        {
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
