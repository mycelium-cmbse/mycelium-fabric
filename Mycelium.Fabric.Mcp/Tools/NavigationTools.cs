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
    using System.ComponentModel;
    using System.Linq;

    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Services;

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
    }
}