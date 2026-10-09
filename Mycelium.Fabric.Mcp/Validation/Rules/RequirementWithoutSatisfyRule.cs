// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementWithoutSatisfyRule.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation.Rules
{
    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// The rule that finds the requirements that no part is known to satisfy: no satisfy link refers to them, nor to a
    /// requirement that owns them. A group of requirements is left out: the requirements it owns are checked.
    /// </summary>
    public class RequirementWithoutSatisfyRule : RequirementRule
    {
        /// <summary>
        /// Gets the name of the rule.
        /// </summary>
        public override string Name => "requirement-without-satisfy";

        /// <summary>
        /// Gets the <see cref="ValidationSeverity"/> of the issues found by the rule.
        /// </summary>
        public override ValidationSeverity Severity => ValidationSeverity.Warning;

        /// <summary>
        /// Gets the description of what the rule checks.
        /// </summary>
        public override string Description => "No satisfy link refers to a requirement, so no part is known to satisfy it and it cannot be checked on the design.";

        /// <summary>
        /// Checks that a satisfy link refers to a requirement.
        /// </summary>
        /// <param name="requirement">The requirement to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the requirement, or <c>null</c> when it is satisfied or is a group.</returns>
        protected override string FindRequirementProblem(IRequirementUsage requirement, ValidationContext context)
        {
            return context.IsSatisfied(requirement) || IsGroup(requirement)
                ? null
                : "No satisfy link refers to the requirement, for example satisfy massBudget by eosat1, so no part is known to satisfy it.";
        }
    }
}
