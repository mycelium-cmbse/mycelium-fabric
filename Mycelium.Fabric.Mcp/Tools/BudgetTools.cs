// ------------------------------------------------------------------------------------------------
//  <copyright file="BudgetTools.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// The MCP tools that let an AI assistant compute budgets (mass, power...) on the SysML v2 model, instead of adding
    /// the values itself.
    /// </summary>
    [McpServerToolType]
    public class BudgetTools
    {
        /// <summary>
        /// The number of decimals of the totals returned by the tools.
        /// </summary>
        private const int TotalDecimalCount = 3;

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BudgetTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> is <c>null</c>.
        /// </exception>
        public BudgetTools(IModelProvider modelProvider)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);

            this.modelProvider = modelProvider;
        }

        /// <summary>
        /// Gives the attributes of the element that has the given identifier, including those inherited from its types.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>The <see cref="AttributeValues"/> of the element.</returns>
        /// <exception cref="McpException">
        /// Thrown when no element of the model has the given identifier.
        /// </exception>
        [McpServerTool(Name = "get_attribute_values", ReadOnly = true)]
        [Description("Gives the attributes of an element (mass, power, data rate, capacity...) with their value, unit and documentation. For a part typed by a definition (for example reactionWheel1 : ReactionWheel), it includes the attributes of the definition.")]
        [return: Description("The identifier, name and types of the element, and each attribute with its numeric value and unit, or its value as text when it is not a number (true, \"S-band\", sunSynchronous), and its documentation.")]
        public AttributeValues GetAttributeValues([Description("The identifier (Id, a GUID) of the element, as returned by the other tools.")] Guid elementId)
        {
            var element = this.modelProvider.GetRequiredElementById(elementId);

            var attributes = element.GetAttributeUsages()
                .Select(attribute => (Attribute: attribute, Value: attribute.GetConstantValue()))
                .Select(attribute => new AttributeValue(attribute.Attribute.DeclaredName, (attribute.Value as NumberValue)?.Number, (attribute.Value as NumberValue)?.Unit,
                    attribute.Value is null or NumberValue ? null : attribute.Value.ToString(), attribute.Attribute.GetDocumentationBodies()))
                .ToList();

            return new AttributeValues(element.Id, element.DeclaredName, element.GetTypeNames(), attributes);
        }

        /// <summary>
        /// Computes the sum of a numeric attribute over the element that has the given identifier and its sub-parts.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the root element of the sum.</param>
        /// <param name="attributeName">The declared name of the attribute to add up.</param>
        /// <returns>The <see cref="SumResult"/> that gives the total and the contribution of each part.</returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="attributeName"/> is <c>null</c>, empty or white space, when no element of the model
        /// has the given identifier, or when no part under the element has a numeric value for the attribute.
        /// </exception>
        [McpServerTool(Name = "sum_attribute", ReadOnly = true)]
        [Description("Computes the sum of a numeric attribute (for example 'mass' or 'power') over an element and all its sub-parts, using the values of their definitions and converting their units. Gives the total and the contribution of each part. Use it for any mass or power budget instead of adding the values yourself.")]
        [return: Description("The total and its unit, the number of contributing parts, and the contribution of each part with its identifier, qualified name, types, value and unit.")]
        public SumResult SumAttribute([Description("The identifier (Id, a GUID) of the root element of the sum, for example the satellite or a subsystem.")] Guid elementId,
            [Description("The name of the attribute to add up, for example 'mass' or 'power'.")] string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new McpException("The attribute name must not be empty.");
            }

            var element = this.modelProvider.GetRequiredElementById(elementId);
            var contributions = element.CollectContributions(attributeName);

            if (contributions.Count == 0)
            {
                throw new McpException($"No part under '{element.DeclaredName}' has a numeric value for '{attributeName}'. Use get_attribute_values to get the attribute names.");
            }

            var total = SumContributions(contributions);

            return new SumResult(attributeName, total.Number, total.Unit, contributions.Count, contributions);
        }

        /// <summary>
        /// Simulates another value of an attribute for one part, without modifying the model, and computes the new total of
        /// this attribute over a root element.
        /// </summary>
        /// <param name="rootElementId">The <c>Id</c> of the root element of the sum.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="elementId">The <c>Id</c> of the part whose value changes.</param>
        /// <param name="newValue">The simulated value of the attribute for the part.</param>
        /// <returns>The <see cref="WhatIfResult"/> that compares the current and new totals.</returns>
        /// <exception cref="McpException">
        /// Thrown when the sum of the attribute over the root element fails (see <see cref="SumAttribute"/>), or when the
        /// part does not contribute to this sum.
        /// </exception>
        [McpServerTool(Name = "evaluate_what_if", ReadOnly = true)]
        [Description("Simulates a change of value without modifying the model: if one part had another value for an attribute (for example a 35 kg camera instead of 38 kg), computes the new total of this attribute over a root element (the satellite or a subsystem), "
            + "and checks again the requirements that depend on this value (constraint or assumption on this attribute, satisfied by a part that includes the changed part). Use it for any 'what if' question or to compare options instead of computing it yourself.")]
        [return: Description("The changed part, its current and simulated values, the current and new totals, the difference between them, and each impacted requirement checked with the current and the simulated value (status, value, gap, explanation).")]
        public WhatIfResult EvaluateWhatIf([Description("The identifier (Id, a GUID) of the root element of the sum, for example the satellite or a subsystem.")] Guid rootElementId,
            [Description("The name of the attribute, for example 'mass' or 'power'.")] string attributeName,
            [Description("The identifier (Id, a GUID) of the part whose value changes, as returned by sum_attribute. It must contribute to the sum.")] Guid elementId,
            [Description("The new value of the attribute for this part, in the same unit as its current value.")] double newValue)
        {
            var sum = this.SumAttribute(rootElementId, attributeName);

            var contribution = sum.Contributions.FirstOrDefault(candidate => candidate.Id == elementId)
                ?? throw new McpException($"The element '{elementId}' does not contribute to this sum of '{attributeName}'. Use sum_attribute to get the contributing parts.");

            var newTotal = SumContributions(sum.Contributions.Select(candidate => candidate.Id == elementId ? candidate with { Value = newValue } : candidate)).Number;
            var difference = Math.Round(newTotal - sum.Total, TotalDecimalCount);

            var requirementImpacts = new RequirementChecker(this.modelProvider.Elements).EvaluateImpacts(elementId, attributeName, newValue);

            return new WhatIfResult(attributeName, contribution.QualifiedName, contribution.Value, newValue, contribution.Unit, sum.Total, newTotal, difference, sum.Unit,
                requirementImpacts);
        }

        /// <summary>
        /// Adds the values of contributions, converted to the unit of the first one that has a unit.
        /// </summary>
        /// <param name="contributions">The contributions.</param>
        /// <returns>The total, rounded to 3 decimals.</returns>
        /// <exception cref="McpException">Thrown when two contributions have units of different dimensions.</exception>
        private static NumberValue SumContributions(IEnumerable<Contribution> contributions)
        {
            var total = NumberValue.Sum(contributions.Select(contribution => new NumberValue(contribution.Value, contribution.Unit)));

            return total.IsError ? throw new McpException(total.FirstError.Description) : total.Value;
        }
    }
}
