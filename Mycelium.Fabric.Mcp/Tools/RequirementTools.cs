// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementTools.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// The MCP tools that let an AI assistant check the requirements of the SysML v2 model, instead of comparing the values
    /// with the limits itself.
    /// </summary>
    [McpServerToolType]
    public class RequirementTools
    {
        /// <summary>
        /// The maximum number of checks returned in one page by <see cref="CheckRequirements"/>.
        /// </summary>
        private const int MaximumCheckCount = 20;

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequirementTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> is <c>null</c>.
        /// </exception>
        public RequirementTools(IModelProvider modelProvider)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);

            this.modelProvider = modelProvider;
        }

        /// <summary>
        /// Checks the requirements of the model, one page at a time.
        /// </summary>
        /// <param name="status">The status of the checks to return, or <c>null</c> for all of them.</param>
        /// <param name="offset">The number of matching checks to skip, 0 for the first page.</param>
        /// <param name="limit">The maximum number of checks of the page, from 1 to <see cref="MaximumCheckCount"/>.</param>
        /// <returns>
        /// The <see cref="RequirementChecks"/> that give the number of checks of each status, the requested page of the
        /// matching checks, and the offset of the next page.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="offset"/> is negative, or when <paramref name="limit"/> is out of range.
        /// </exception>
        [McpServerTool(Name = "check_requirements", ReadOnly = true)]
        [Description("Checks the requirements of the model. For each requirement with a constraint (for example subj.mass * 1.2 <= 150, built by the SetConstraint change of apply_changes) and a part that satisfies it (Satisfy change), computes the value of the attribute on this part as sum_attribute does and tells whether the constraint holds, by how much (gap) and why. "
            + "Statuses: Satisfied, NotSatisfied, NotVerifiable (no constraint or no satisfying part), NotEvaluated (constraint of another form, or no value for the attribute). Use it instead of comparing values with limits yourself. "
            + "The results are paged: when nextOffset is not null, call the tool again with this offset to get the next page.")]
        [return: Description("The number of checks of each status in the whole model, the number of checks that match the requested status, one page of them sorted by requirement identifier with their requirement, status, constraint, satisfying part, value, gap and explanation, and the offset of the next page (null on the last page).")]
        public RequirementChecks CheckRequirements([Description("Optional: only the checks with this status, for example NotSatisfied to list the problems. Without it, all the checks are returned.")] RequirementStatus? status = null,
            [Description("The number of matching checks to skip: 0 for the first page, then the nextOffset of the previous result.")] int offset = 0,
            [Description("The maximum number of checks to return, from 1 to 20.")] int limit = MaximumCheckCount)
        {
            if (offset < 0)
            {
                throw new McpException("The offset must be 0 or greater.");
            }

            if (limit < 1 || limit > MaximumCheckCount)
            {
                throw new McpException($"The limit must be between 1 and {MaximumCheckCount}.");
            }

            var checks = new RequirementChecker(this.modelProvider.Elements).CheckRequirements();

            var matches = status == null ? checks : checks.Where(check => check.Status == status).ToList();

            var page = matches
                .Skip(offset)
                .Take(limit)
                .ToList();

            int? nextOffset = offset + page.Count < matches.Count ? offset + page.Count : null;

            var statusCounts = checks.CountBy(check => check.Status).ToDictionary();

            return new RequirementChecks(statusCounts.GetValueOrDefault(RequirementStatus.Satisfied), statusCounts.GetValueOrDefault(RequirementStatus.NotSatisfied),
                statusCounts.GetValueOrDefault(RequirementStatus.NotVerifiable), statusCounts.GetValueOrDefault(RequirementStatus.NotEvaluated), matches.Count, page, nextOffset);
        }
    }
}
