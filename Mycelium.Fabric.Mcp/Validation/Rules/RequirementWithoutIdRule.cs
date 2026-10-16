// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementWithoutIdRule.cs" company="Starion Group S.A.">
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
    /// The rule that finds the requirements without identifier (<c>reqId</c>), which cannot be traced to the document they
    /// come from.
    /// </summary>
    public class RequirementWithoutIdRule : RequirementRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequirementWithoutIdRule"/> class.
        /// </summary>
        public RequirementWithoutIdRule() : base("requirement-without-id", ValidationSeverity.Information,
            "A requirement has no identifier (reqId), so it cannot be traced to the document it comes from.")
        {
        }

        /// <summary>
        /// Checks that a requirement has an identifier.
        /// </summary>
        /// <param name="requirement">The requirement to check.</param>
        /// <param name="context">The <see cref="ValidationContext"/> of the model.</param>
        /// <returns>The problem of the requirement, or <c>null</c> when it has an identifier.</returns>
        protected override string FindRequirementProblem(IRequirementUsage requirement, ValidationContext context)
        {
            return string.IsNullOrWhiteSpace(requirement.ReqId) ? "The requirement has no identifier (reqId), for example REQ-SYS-001." : null;
        }
    }
}
