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

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

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
        /// The maximum number of elements returned by one call of <see cref="FindElementsByName"/>.
        /// </summary>
        private const int MaximumSearchResultCount = 20;

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

            var namedElementCount = elements.Count(element => !string.IsNullOrWhiteSpace(element.DeclaredName));

            return new ModelOverview(topLevelElements, elements.Count, namedElementCount, mostFrequentTypes);
        }

        /// <summary>
        /// Finds the elements whose declared name contains the given text, ignoring case, one page at a time.
        /// </summary>
        /// <param name="text">The text to look for in the names of the elements.</param>
        /// <param name="offset">The number of matching elements to skip, 0 for the first page.</param>
        /// <param name="limit">The maximum number of elements of the page, from 1 to <see cref="MaximumSearchResultCount"/>.</param>
        /// <returns>
        /// The <see cref="SearchResult"/> that gives the number of matching elements, the requested page of them sorted by
        /// name, and the offset of the next page.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="text"/> is <c>null</c>, empty or white space, when <paramref name="offset"/> is
        /// negative, or when <paramref name="limit"/> is out of range.
        /// </exception>
        [McpServerTool(Name = "find_elements_by_name", ReadOnly = true)]
        [Description("Finds the elements whose name contains a text, ignoring case. Use it to get the identifier of an element from its name. The results are paged: when nextOffset is not null, call the tool again with this offset to get the next page.")]
        [return: Description("The total number of matching elements, one page of them sorted by name with their identifier, name, type and qualified name, and the offset of the next page (null on the last page).")]
        public SearchResult FindElementsByName([Description("The text to look for in the names of the elements, for example 'camera'.")] string text,
            [Description("The number of matching elements to skip: 0 for the first page, then the nextOffset of the previous result.")] int offset = 0,
            [Description("The maximum number of elements to return, from 1 to 20.")] int limit = MaximumSearchResultCount)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new McpException("The text to look for must not be empty.");
            }

            if (offset < 0)
            {
                throw new McpException("The offset must be 0 or greater.");
            }

            if (limit < 1 || limit > MaximumSearchResultCount)
            {
                throw new McpException($"The limit must be between 1 and {MaximumSearchResultCount}.");
            }

            var matches = this.modelProvider.Elements
                .Where(element => element.DeclaredName != null && element.DeclaredName.Contains(text, StringComparison.OrdinalIgnoreCase))
                .OrderBy(element => element.DeclaredName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var page = matches
                .Skip(offset)
                .Take(limit)
                .Select(CreateSummary)
                .ToList();

            int? nextOffset = offset + page.Count < matches.Count ? offset + page.Count : null;

            return new SearchResult(matches.Count, page, nextOffset);
        }

        /// <summary>
        /// Gives the details of the element that has the given identifier.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>The <see cref="ElementDetails"/> of the element.</returns>
        /// <exception cref="McpException">
        /// Thrown when no element of the model has the given identifier.
        /// </exception>
        [McpServerTool(Name = "get_element_details", ReadOnly = true)]
        [Description("Gives the details of an element from its identifier: type, name, short name, qualified name, owner, number of children and documentation.")]
        [return: Description("The details of the element, including the identifier of its owner to navigate up the model.")]
        public ElementDetails GetElementDetails([Description("The identifier (Id, a GUID) of the element, as returned by the other tools.")] Guid elementId)
        {
            var element = this.modelProvider.GetRequiredElementById(elementId);

            return new ElementDetails(element.Id, element.DeclaredName, element.DeclaredShortName, element.DescribeType(), element.qualifiedName,
                element.owner?.Id, element.owner?.DeclaredName, element.ownedElement?.Count ?? 0, element.GetDocumentationBodies());
        }

        /// <summary>
        /// Lists the elements directly owned by the element that has the given identifier.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the parent element.</param>
        /// <returns>An <see cref="ElementSummary"/> for each child of the element.</returns>
        /// <exception cref="McpException">
        /// Thrown when no element of the model has the given identifier.
        /// </exception>
        [McpServerTool(Name = "list_children", ReadOnly = true)]
        [Description("Lists the direct children (owned elements) of an element from its identifier.")]
        [return: Description("The identifier, name, type and qualified name of each child of the element.")]
        public IReadOnlyList<ElementSummary> ListChildren([Description("The identifier (Id, a GUID) of the parent element, as returned by the other tools.")] Guid elementId)
        {
            var element = this.modelProvider.GetRequiredElementById(elementId);

            return (element.ownedElement ?? [])
                .Select(CreateSummary)
                .ToList();
        }

        /// <summary>
        /// Creates the <see cref="ElementSummary"/> of the given element.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to summarize.</param>
        /// <returns>The <see cref="ElementSummary"/> of the element.</returns>
        private static ElementSummary CreateSummary(IElement element)
        {
            return new ElementSummary(element.Id, element.DeclaredName, element.DescribeType(), element.qualifiedName);
        }
    }
}