// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelElementExtensions.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Attributes;

    /// <summary>
    /// Extension methods that read, from the elements of a SysML v2 model, the information returned by the MCP tools.
    /// </summary>
    public static class ModelElementExtensions
    {
        /// <param name="element">The <see cref="IElement"/> to read.</param>
        extension(IElement element)
        {
            /// <summary>
            /// Gets the definition of the element: the <see cref="IType"/> applied by its first <see cref="IFeatureTyping"/>
            /// (for example <c>OpticalCamera</c> for <c>camera : OpticalCamera</c>).
            /// </summary>
            /// <returns>The definition of the element, or <c>null</c> when the element is not typed.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public IType GetDefinition()
            {
                ArgumentNullException.ThrowIfNull(element);

                return (element.OwnedRelationship ?? [])
                    .OfType<IFeatureTyping>()
                    .FirstOrDefault()?.Type;
            }

            /// <summary>
            /// Describes the type of the element: its metaclass, followed by its definition when it is typed (for example
            /// <c>PartUsage : OpticalCamera</c>).
            /// </summary>
            /// <returns>The description of the type of the element.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public string DescribeType()
            {
                var definition = element.GetDefinition();

                return definition == null ? element.GetType().Name : $"{element.GetType().Name} : {definition.DeclaredName}";
            }

            /// <summary>
            /// Gets the documentation of the element, joining the bodies of its documentation comments.
            /// </summary>
            /// <returns>The documentation of the element, or <c>null</c> when it has none.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public string GetDocumentation()
            {
                ArgumentNullException.ThrowIfNull(element);

                var bodies = (element.documentation ?? [])
                    .Select(documentation => documentation.Body)
                    .ToList();

                return bodies.Count == 0 ? null : string.Join("\n", bodies);
            }

            /// <summary>
            /// Gets the attributes of the element: the <see cref="IAttributeUsage"/>s it owns, followed by those owned by
            /// its definition.
            /// </summary>
            /// <returns>The attributes of the element.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<IAttributeUsage> GetAttributes()
            {
                var definition = element.GetDefinition();

                var ownAttributes = (element.ownedElement ?? []).OfType<IAttributeUsage>();
                var definitionAttributes = (definition?.ownedElement ?? []).OfType<IAttributeUsage>();

                return [.. ownAttributes, .. definitionAttributes];
            }
        }

        /// <param name="attribute">The <see cref="IAttributeUsage"/> to read.</param>
        extension(IAttributeUsage attribute)
        {
            /// <summary>
            /// Gets the numeric value of the attribute: the literal number bound to it by its <see cref="IFeatureValue"/>
            /// (for example <c>38</c> for <c>attribute mass = 38;</c>).
            /// </summary>
            /// <returns>The value of the attribute, or <c>null</c> when it is not bound to a literal number.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="attribute"/> is <c>null</c>.
            /// </exception>
            public double? GetNumericValue()
            {
                ArgumentNullException.ThrowIfNull(attribute);

                return (attribute.OwnedRelationship ?? [])
                    .OfType<IFeatureValue>()
                    .SelectMany(featureValue => featureValue.OwnedRelatedElement ?? [])
                    .Select(valueElement => valueElement switch
                    {
                        ILiteralRational literalRational => literalRational.Value,
                        ILiteralInteger literalInteger => literalInteger.Value,
                        _ => (double?)null
                    })
                    .FirstOrDefault(value => value != null);
            }
        }
    }
}
