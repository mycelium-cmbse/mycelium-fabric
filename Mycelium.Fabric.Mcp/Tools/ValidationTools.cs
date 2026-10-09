// ------------------------------------------------------------------------------------------------
//  <copyright file="ValidationTools.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Validation;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// The MCP tool that checks the model with deterministic rules, so that the AI assistant does not judge the model itself.
    /// </summary>
    [McpServerToolType]
    public class ValidationTools
    {
        /// <summary>
        /// The maximum number of issues returned in one page by <see cref="ValidateModel"/>.
        /// </summary>
        private const int MaximumIssueCount = 20;

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="IModelValidator"/> that checks the model.
        /// </summary>
        private readonly IModelValidator modelValidator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="modelValidator">The <see cref="IModelValidator"/> that checks the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> or <paramref name="modelValidator"/> is <c>null</c>.
        /// </exception>
        public ValidationTools(IModelProvider modelProvider, IModelValidator modelValidator)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(modelValidator);

            this.modelProvider = modelProvider;
            this.modelValidator = modelValidator;
        }

        /// <summary>
        /// Checks the model, or an element and what it owns, and returns the issues found one page at a time.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element to check with what it owns, or <c>null</c> for the whole model.</param>
        /// <param name="severity">The least serious severity of the issues to return, or <c>null</c> for all of them.</param>
        /// <param name="rule">The name of the rule whose issues to return, or <c>null</c> for all the rules.</param>
        /// <param name="offset">The number of matching issues to skip, 0 for the first page.</param>
        /// <param name="limit">The maximum number of issues of the page, from 1 to <see cref="MaximumIssueCount"/>.</param>
        /// <returns>
        /// The <see cref="ValidationReport"/> that gives the number of issues of each severity and of each rule in the checked
        /// elements, the requested page of the matching issues, and the offset of the next page.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="elementId"/> is not the identifier of an element, when <paramref name="rule"/> is not the
        /// name of a rule, when <paramref name="offset"/> is negative, or when <paramref name="limit"/> is out of range.
        /// </exception>
        [McpServerTool(Name = "validate_model", ReadOnly = true)]
        [Description("Checks the model with deterministic rules and lists the issues found, the most serious first. Errors: the model is broken (missing element, element outside the containment tree, duplicate name). "
            + "Warnings: the model is probably incomplete (a part without type nor feature, a requirement that cannot be verified or that no part satisfies). Information: points to tidy up. "
            + "The result lists every rule checked with its description and number of issues. Call it after building or changing the model, or when the user asks whether the model is valid, instead of judging the model yourself. "
            + "The results are paged: when nextOffset is not null, call the tool again with this offset to get the next page.")]
        [return: Description("The number of checked elements and of issues of each severity, each rule with its severity, description and number of issues, the number of issues that match the filters, one page of them with their rule, severity, element identifier and name, and message, and the offset of the next page (null on the last page).")]
        public ValidationReport ValidateModel([Description("Optional: the identifier (Id, a GUID) of an element, for example a subsystem, to check only this element and the elements it owns. Without it, the whole model is checked.")] Guid? elementId = null,
            [Description("Optional: the least serious severity to return: Error for the errors only, Warning for the errors and the warnings. Without it, all the issues are returned.")] ValidationSeverity? severity = null,
            [Description("Optional: the name of a rule, for example requirement-without-constraint, to return only its issues.")] string rule = null,
            [Description("The number of matching issues to skip: 0 for the first page, then the nextOffset of the previous result.")] int offset = 0,
            [Description("The maximum number of issues to return, from 1 to 20.")] int limit = MaximumIssueCount)
        {
            if (offset < 0)
            {
                throw new McpException("The offset must be 0 or greater.");
            }

            if (limit < 1 || limit > MaximumIssueCount)
            {
                throw new McpException($"The limit must be between 1 and {MaximumIssueCount}.");
            }

            var rules = this.modelValidator.Rules;

            if (rule != null && !rules.Any(validationRule => validationRule.Name == rule))
            {
                throw new McpException($"No rule is named '{rule}'. The rules are: {string.Join(", ", rules.Select(validationRule => validationRule.Name))}.");
            }

            var elements = this.modelProvider.Elements;
            var checkedElementIds = elementId == null ? null : CollectOwnedTree(this.modelProvider.GetRequiredElementById(elementId.Value));

            var issues = this.modelValidator.Validate(elements)
                .Where(issue => checkedElementIds == null || checkedElementIds.Contains(issue.ElementId))
                .ToList();

            var matches = issues
                .Where(issue => (severity == null || issue.Severity <= severity) && (rule == null || issue.Rule == rule))
                .ToList();

            var page = matches
                .Skip(offset)
                .Take(limit)
                .ToList();

            int? nextOffset = offset + page.Count < matches.Count ? offset + page.Count : null;

            var ruleCounts = issues.CountBy(issue => issue.Rule).ToDictionary();
            var severityCounts = issues.CountBy(issue => issue.Severity).ToDictionary();

            var ruleSummaries = rules
                .Select(validationRule => new RuleSummary(validationRule.Name, validationRule.Severity, validationRule.Description, ruleCounts.GetValueOrDefault(validationRule.Name)))
                .ToList();

            return new ValidationReport(checkedElementIds?.Count ?? elements.Count, severityCounts.GetValueOrDefault(ValidationSeverity.Error), severityCounts.GetValueOrDefault(ValidationSeverity.Warning),
                severityCounts.GetValueOrDefault(ValidationSeverity.Information), ruleSummaries, matches.Count, page, nextOffset);
        }

        /// <summary>
        /// Collects an element and all the elements it owns, directly or not, with the relationships between them.
        /// </summary>
        /// <param name="root">The element at the top of the tree.</param>
        /// <returns>The <c>Id</c> of the elements of the tree.</returns>
        private static HashSet<Guid> CollectOwnedTree(IElement root)
        {
            var elementIds = new HashSet<Guid>();
            var pendingElements = new Stack<IElement>([root]);

            while (pendingElements.TryPop(out var element))
            {
                if (element == null || !elementIds.Add(element.Id))
                {
                    continue;
                }

                IEnumerable<IElement> ownedElements = element is IRelationship relationship ? relationship.OwnedRelatedElement ?? [] : [];

                foreach (var ownedElement in ownedElements.Concat(element.OwnedRelationship ?? []))
                {
                    pendingElements.Push(ownedElement);
                }
            }

            return elementIds;
        }
    }
}
