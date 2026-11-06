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
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// Extension methods for <see cref="IModelProvider"/>: what the tools read about the whole model.
    /// </summary>
    public static class ModelProviderExtensions
    {
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        extension(IModelProvider modelProvider)
        {
            /// <summary>
            /// Gets the identifiers of the requirements of the model: the <c>reqId</c> of its requirement definitions and usages.
            /// </summary>
            /// <returns>The identifiers, compared character by character.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="modelProvider"/> is <c>null</c>.</exception>
            public HashSet<string> GetRequirementIds()
            {
                ArgumentNullException.ThrowIfNull(modelProvider);

                var elements = modelProvider.Elements;

                return new HashSet<string>(elements.OfType<IRequirementUsage>().Select(requirement => requirement.ReqId)
                    .Concat(elements.OfType<IRequirementDefinition>().Select(requirement => requirement.ReqId))
                    .Where(identifier => !string.IsNullOrWhiteSpace(identifier)), StringComparer.Ordinal);
            }
        }
    }
}
