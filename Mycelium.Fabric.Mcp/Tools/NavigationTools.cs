// ------------------------------------------------------------------------------------------------
//  <copyright file="NavigationTools.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// The MCP tools that let an AI assistant discover and navigate the SysML v2 model.
    /// </summary>
    [McpServerToolType]
    public class NavigationTools
    {
        /// <summary>
        /// The maximum number of element types listed by <see cref="GetModelOverview"/>.
        /// </summary>
        private const int MaximumTypeCount = 15;

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> is <c>null</c>.
        /// </exception>
        public NavigationTools(IModelProvider modelProvider)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);

            this.modelProvider = modelProvider;
        }

        /// <summary>
        /// Gives an overview of the model: its top-level elements, its number of elements and its most
        /// frequent element types.
        /// </summary>
        /// <returns>The <see cref="ModelOverview"/> of the model.</returns>
        [McpServerTool(Name = "get_model_overview", ReadOnly = true)]
        [Description("Gives an overview of the loaded model: its top-level elements, the total number of elements and the most frequent element types. Call it first to discover the model.")]
        [return: Description("The top-level elements of the model, its total and named element counts, and its most frequent element types with their counts.")]
        public ModelOverview GetModelOverview()
        {
            var elements = this.modelProvider.Elements;

            var topLevelElements = this.modelProvider.RootElements
                .SelectMany(rootElement => rootElement.ownedElement ?? [])
                .Select(element => element.DeclaredName ?? element.GetType().Name)
                .ToList();

            var mostFrequentTypes = elements
                .GroupBy(element => element.GetType().Name)
                .OrderByDescending(group => group.Count())
                .Take(MaximumTypeCount)
                .ToDictionary(group => group.Key, group => group.Count());

            return new ModelOverview(
                TopLevelElements: topLevelElements,
                ElementCount: elements.Count,
                NamedElementCount: elements.Count(element => !string.IsNullOrWhiteSpace(element.DeclaredName)),
                MostFrequentTypes: mostFrequentTypes);
        }

        /// <summary>
        /// Gives the details of the element that has the given identifier.
        /// </summary>
        /// <param name="elementId">The <see cref="IElement.ElementId"/> of the element.</param>
        /// <returns>The <see cref="ElementDetails"/> of the element.</returns>
        /// <exception cref="McpException">
        /// Thrown when no element of the model has the given identifier.
        /// </exception>
        [McpServerTool(Name = "get_element_details", ReadOnly = true)]
        [Description("Gives the details of an element from its identifier: type, name, short name, qualified name, owner, number of children and documentation.")]
        [return: Description("The details of the element, including the identifier of its owner to navigate up the model.")]
        public ElementDetails GetElementDetails([Description("The identifier (ElementId) of the element, as returned by the other tools.")] string elementId)
        {
            var element = this.GetElement(elementId);

            return new ElementDetails(
                ElementId: element.ElementId,
                Name: element.DeclaredName,
                ShortName: element.DeclaredShortName,
                Type: DescribeType(element),
                QualifiedName: element.qualifiedName,
                OwnerId: element.owner?.ElementId,
                OwnerName: element.owner?.DeclaredName,
                ChildCount: element.ownedElement?.Count ?? 0,
                Documentation: GetDocumentation(element));
        }

        /// <summary>
        /// Lists the elements directly owned by the element that has the given identifier.
        /// </summary>
        /// <param name="elementId">The <see cref="IElement.ElementId"/> of the parent element.</param>
        /// <returns>An <see cref="ElementSummary"/> for each child of the element.</returns>
        /// <exception cref="McpException">
        /// Thrown when no element of the model has the given identifier.
        /// </exception>
        [McpServerTool(Name = "list_children", ReadOnly = true)]
        [Description("Lists the direct children (owned elements) of an element from its identifier.")]
        [return: Description("The identifier, name, type and qualified name of each child of the element.")]
        public IReadOnlyList<ElementSummary> ListChildren([Description("The identifier (ElementId) of the parent element, as returned by the other tools.")] string elementId)
        {
            var element = this.GetElement(elementId);

            return (element.ownedElement ?? [])
                .Select(CreateSummary)
                .ToList();
        }

        /// <summary>
        /// Gets the element that has the given identifier, or fails with a message the AI assistant can act on.
        /// </summary>
        /// <param name="elementId">The <see cref="IElement.ElementId"/> of the element.</param>
        /// <returns>The <see cref="IElement"/> that has the given identifier.</returns>
        /// <exception cref="McpException">
        /// Thrown when no element of the model has the given identifier. Unlike other exceptions, the message of an
        /// <see cref="McpException"/> is sent back to the AI assistant.
        /// </exception>
        private IElement GetElement(string elementId)
        {
            var element = string.IsNullOrWhiteSpace(elementId) ? null : this.modelProvider.GetElementById(elementId);

            if (element == null)
            {
                throw new McpException($"No element has the identifier '{elementId}'. Use find_elements_by_name or list_children to get a valid identifier.");
            }

            return element;
        }

        /// <summary>
        /// Creates the <see cref="ElementSummary"/> of the given element.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to summarize.</param>
        /// <returns>The <see cref="ElementSummary"/> of the element.</returns>
        private static ElementSummary CreateSummary(IElement element)
        {
            return new ElementSummary(
                ElementId: element.ElementId,
                Name: element.DeclaredName,
                Type: DescribeType(element),
                QualifiedName: element.qualifiedName);
        }

        /// <summary>
        /// Describes the type of the given element: its metaclass, followed by its definition when it is typed by
        /// an <see cref="IFeatureTyping"/> (for example <c>PartUsage : OpticalCamera</c>).
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to describe.</param>
        /// <returns>The description of the type of the element.</returns>
        private static string DescribeType(IElement element)
        {
            var definition = (element.OwnedRelationship ?? [])
                .OfType<IFeatureTyping>()
                .FirstOrDefault()?.Type;

            return definition == null ? element.GetType().Name : $"{element.GetType().Name} : {definition.DeclaredName}";
        }

        /// <summary>
        /// Gets the documentation of the given element, joining the bodies of its documentation comments.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> whose documentation is read.</param>
        /// <returns>The documentation of the element, or <c>null</c> when it has none.</returns>
        private static string GetDocumentation(IElement element)
        {
            var bodies = (element.documentation ?? [])
                .Select(documentation => documentation.Body)
                .ToList();

            return bodies.Count == 0 ? null : string.Join("\n", bodies);
        }
    }
}