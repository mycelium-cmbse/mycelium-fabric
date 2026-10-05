// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementExtensions.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Extension methods that read, from the <see cref="IElement"/>s of a SysML v2 model, the information returned by the MCP
    /// tools.
    /// </summary>
    public static class ElementExtensions
    {
        /// <param name="element">The <see cref="IElement"/> to read.</param>
        extension(IElement element)
        {
            /// <summary>
            /// Gets the declared names of the types of the element (for example <c>OpticalCamera</c> for
            /// <c>camera : OpticalCamera</c>). Only an <see cref="IFeature"/> can be typed.
            /// </summary>
            /// <returns>The names of the types of the element, empty when it is not a typed <see cref="IFeature"/>.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<string> GetTypeNames()
            {
                ArgumentNullException.ThrowIfNull(element);

                return element is IFeature feature ? [.. (feature.type ?? []).Select(type => type.DeclaredName)] : [];
            }

            /// <summary>
            /// Describes the type of the element: its metaclass, followed by its types when it is a typed
            /// <see cref="IFeature"/> (for example <c>PartUsage : OpticalCamera</c>).
            /// </summary>
            /// <returns>The description of the type of the element.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public string DescribeType()
            {
                var typeNames = element.GetTypeNames();
                var metaclass = element.GetType().Name;

                return typeNames.Count == 0 ? metaclass : $"{metaclass} : {string.Join(", ", typeNames)}";
            }

            /// <summary>
            /// Gets the bodies of the documentation comments of the element, joined in a single text.
            /// </summary>
            /// <returns>The documentation bodies of the element, or <c>null</c> when it has none.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public string GetDocumentationBodies()
            {
                ArgumentNullException.ThrowIfNull(element);

                var bodies = (element.documentation ?? [])
                    .Select(documentation => documentation.Body)
                    .ToList();

                return bodies.Count == 0 ? null : string.Join("\n", bodies);
            }
        }
    }
}
