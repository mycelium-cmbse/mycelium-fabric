// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelProviderExtensions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extensions
{
    using System;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Extension methods of <see cref="IModelProvider"/> shared by the MCP tools.
    /// </summary>
    public static class ModelProviderExtensions
    {
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        extension(IModelProvider modelProvider)
        {
            /// <summary>
            /// Gets the element that has the given identifier, or fails with a message the AI assistant can act on.
            /// </summary>
            /// <param name="elementId">The <c>Id</c> of the element.</param>
            /// <returns>The <see cref="IElement"/> that has the given identifier.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="modelProvider"/> is <c>null</c>.
            /// </exception>
            /// <exception cref="McpException">
            /// Thrown when no element of the model has the given identifier. Unlike other exceptions, the message of an
            /// <see cref="McpException"/> is sent back to the AI assistant.
            /// </exception>
            public IElement GetRequiredElementById(Guid elementId)
            {
                ArgumentNullException.ThrowIfNull(modelProvider);

                return modelProvider.GetElementById(elementId)
                    ?? throw new McpException($"No element has the identifier '{elementId}'. Use find_elements_by_name or list_children to get a valid identifier.");
            }
        }
    }
}
