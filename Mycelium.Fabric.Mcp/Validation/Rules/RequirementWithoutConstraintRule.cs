// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementWithoutConstraintRule.cs" company="Starion Group S.A.">
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
    /// The rule that finds the requirements that cannot be verified, because they have no required constraint, of their own
    /// or from their requirement definition. A group of requirements is left out: the requirements it owns carry the
    /// constraints.
    /// </summary>
    public class RequirementWithoutConstraintRule : RequirementRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequirementWithoutConstraintRule"/> class.
        /// </summary>
        public RequirementWithoutConstraintRule() : base("requirement-without-constraint", ValidationSeverity.Warning,
            "A requirement has no required constraint, so it is only text and cannot be verified.")
        {
        }

        /// <summary>
        /// Checks that a requirement has a required constraint.
        /// </summary>
        /// <param name="requirement">The requirement to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the requirement, or <c>null</c> when it has a required constraint or is a group.</returns>
        protected override string FindRequirementProblem(IRequirementUsage requirement, ValidationContext context)
        {
            var hasConstraint = requirement.requiredConstraint is { Count: > 0 } || requirement.requirementDefinition?.requiredConstraint is { Count: > 0 };

            return hasConstraint || IsGroup(requirement)
                ? null
                : "The requirement has no required constraint, for example subj.mass <= 150 [kg], so it cannot be verified.";
        }
    }
}
