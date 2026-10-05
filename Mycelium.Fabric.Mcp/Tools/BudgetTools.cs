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
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Parts;

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
        [Description("Gives the attributes of an element (mass, power, data rate, capacity...) with their value and documentation, which gives the unit. For a part typed by a definition (for example reactionWheel1 : ReactionWheel), it includes the attributes of the definition.")]
        [return: Description("The identifier, name and types of the element, and each attribute with its value (null when it is not a number) and documentation.")]
        public AttributeValues GetAttributeValues([Description("The identifier (Id, a GUID) of the element, as returned by the other tools.")] Guid elementId)
        {
            var element = this.modelProvider.GetRequiredElementById(elementId);

            var attributes = GetAttributes(element)
                .Select(attribute => new AttributeValue(attribute.DeclaredName, attribute.GetNumericValue(), attribute.GetDocumentationBodies()))
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
        [Description("Computes the sum of a numeric attribute (for example 'mass' or 'power') over an element and all its sub-parts, using the values of their definitions. Gives the total and the contribution of each part. Use it for any mass or power budget instead of adding the values yourself.")]
        [return: Description("The total, the number of contributing parts, and the contribution of each part with its identifier, qualified name, types and value.")]
        public SumResult SumAttribute([Description("The identifier (Id, a GUID) of the root element of the sum, for example the satellite or a subsystem.")] Guid elementId,
            [Description("The name of the attribute to add up, for example 'mass' or 'power'.")] string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                throw new McpException("The attribute name must not be empty.");
            }

            var element = this.modelProvider.GetRequiredElementById(elementId);
            var contributions = CollectContributions(element, attributeName);

            if (contributions.Count == 0)
            {
                throw new McpException($"No part under '{element.DeclaredName}' has a numeric value for '{attributeName}'. Use get_attribute_values to get the attribute names.");
            }

            var total = Math.Round(contributions.Sum(contribution => contribution.Value), TotalDecimalCount);

            return new SumResult(attributeName, total, contributions.Count, contributions);
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
        [Description("Simulates a change of value without modifying the model: if one part had another value for an attribute (for example a 35 kg camera instead of 38 kg), computes the new total of this attribute over a root element (the satellite or a subsystem). Use it for any 'what if' question or to compare options instead of computing it yourself.")]
        [return: Description("The changed part, its current and simulated values, the current and new totals, and the difference between them.")]
        public WhatIfResult EvaluateWhatIf([Description("The identifier (Id, a GUID) of the root element of the sum, for example the satellite or a subsystem.")] Guid rootElementId,
            [Description("The name of the attribute, for example 'mass' or 'power'.")] string attributeName,
            [Description("The identifier (Id, a GUID) of the part whose value changes, as returned by sum_attribute. It must contribute to the sum.")] Guid elementId,
            [Description("The new value of the attribute for this part, in the same unit as its current value.")] double newValue)
        {
            var sum = this.SumAttribute(rootElementId, attributeName);

            var contribution = sum.Contributions.FirstOrDefault(candidate => candidate.Id == elementId)
                ?? throw new McpException($"The element '{elementId}' does not contribute to this sum of '{attributeName}'. Use sum_attribute to get the contributing parts.");

            var newTotal = Math.Round(sum.Total - contribution.Value + newValue, TotalDecimalCount);
            var difference = Math.Round(newTotal - sum.Total, TotalDecimalCount);

            return new WhatIfResult(attributeName, contribution.QualifiedName, contribution.Value, newValue, sum.Total, newTotal, difference);
        }

        /// <summary>
        /// Collects the contributions of the given element and its sub-parts to the sum of an attribute. An element that has
        /// a numeric value for the attribute, owned or inherited from its types, contributes this value and its own
        /// sub-parts are not visited, so that no value is counted twice.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> whose contributions are collected.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <returns>The <see cref="Contribution"/> of each contributing part.</returns>
        private static List<Contribution> CollectContributions(IElement element, string attributeName)
        {
            var value = GetAttributes(element)
                .FirstOrDefault(attribute => attribute.DeclaredName == attributeName)?
                .GetNumericValue();

            if (value != null)
            {
                return [new Contribution(element.Id, element.qualifiedName, element.GetTypeNames(), value.Value)];
            }

            return (element.ownedElement ?? [])
                .OfType<IPartUsage>()
                .SelectMany(part => CollectContributions(part, attributeName))
                .ToList();
        }

        /// <summary>
        /// Gets the attributes of the given element: the <see cref="IAttributeUsage"/>s among the features of its
        /// <see cref="IType"/>, owned or inherited (for example the attributes of <c>OpticalCamera</c> for
        /// <c>camera : OpticalCamera</c>).
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> whose attributes are read.</param>
        /// <returns>The attributes of the element, empty when it is not an <see cref="IType"/>.</returns>
        private static IEnumerable<IAttributeUsage> GetAttributes(IElement element)
        {
            return element is IType type ? (type.feature ?? []).OfType<IAttributeUsage>() : [];
        }
    }
}
