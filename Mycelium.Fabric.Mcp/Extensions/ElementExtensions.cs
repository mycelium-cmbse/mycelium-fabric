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

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Extension methods that read, from the <see cref="IElement"/>s of a SysML v2 model, the information returned by the MCP
    /// tools.
    /// </summary>
    public static class ElementExtensions
    {
        /// <summary>
        /// The end of the name of the attribute that gives the margin of a value, in %: <c>massMargin</c> for <c>mass</c>.
        /// </summary>
        public const string MarginSuffix = "Margin";

        /// <param name="element">The <see cref="IElement"/> to read.</param>
        extension(IElement element)
        {
            /// <summary>
            /// Gets the names of the types of the element (for example <c>OpticalCamera</c> for <c>camera : OpticalCamera</c>,
            /// or <c>~CommandPort</c> for a port typed by the conjugate of <c>CommandPort</c>). Only an <see cref="IFeature"/>
            /// can be typed.
            /// </summary>
            /// <returns>The names of the types of the element, empty when it is not a typed <see cref="IFeature"/>.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<string> GetTypeNames()
            {
                ArgumentNullException.ThrowIfNull(element);

                return element is IFeature feature ? [.. (feature.type ?? []).Select(type => type.name)] : [];
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

            /// <summary>
            /// Gets the attributes of the element: the <see cref="IAttributeUsage"/>s among the features of its
            /// <see cref="IType"/>, owned or inherited (for example the attributes of <c>OpticalCamera</c> for
            /// <c>camera : OpticalCamera</c>).
            /// </summary>
            /// <returns>The attributes of the element, empty when it is not an <see cref="IType"/>.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public IEnumerable<IAttributeUsage> GetAttributeUsages()
            {
                ArgumentNullException.ThrowIfNull(element);

                return element is IType type ? (type.feature ?? []).OfType<IAttributeUsage>() : [];
            }

            /// <summary>
            /// Collects the contributions of the element and its sub-parts to the sum of an attribute, as
            /// <c>CollectValues</c> does.
            /// </summary>
            /// <param name="attributeName">The declared name of the attribute.</param>
            /// <returns>The <see cref="Contribution"/> of each contributing part, empty when no part has a value.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<Contribution> CollectContributions(string attributeName)
            {
                return element.CollectValues(attributeName).Contributions;
            }

            /// <summary>
            /// Collects the values of an attribute over the element and its sub-parts, before they are added up. An element
            /// that has a numeric value for the attribute (with or without unit), owned or inherited from its types,
            /// contributes this value, with the margin that the model gives for it (the attribute named after it with
            /// <c>Margin</c>, in %, for example <c>massMargin</c>), and its own sub-parts are not visited, so that no value is
            /// counted twice. The sub-parts of a type are its part features, owned or inherited from its types: the parts
            /// declared in a part definition count for each part that this definition types (<c>part stage : Stage</c>
            /// counts the engines of <c>Stage</c>). A part without sub-parts and without value misses its value ("to be
            /// defined") when it declares the attribute without any value, or when it has the type of a contributing part.
            /// </summary>
            /// <param name="attributeName">The declared name of the attribute.</param>
            /// <returns>The <see cref="CollectedValues"/>: the contributions, and the parts whose value is missing.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public CollectedValues CollectValues(string attributeName)
            {
                ArgumentNullException.ThrowIfNull(element);

                var collector = new ValueCollector(attributeName);
                CollectPartValues(element, collector, []);

                var missingValues = collector.ValuelessParts
                    .Where(part => part.DeclaresAttribute || GetTypeIds(part.Part).Any(collector.ContributingTypeIds.Contains))
                    .Select(part => new MissingValue(part.Part.Id, part.Part.qualifiedName, part.Part.GetTypeNames()))
                    .ToList();

                return new CollectedValues(collector.Contributions, missingValues);
            }
        }

        /// <summary>
        /// Collects the values of an element and of its sub-parts, for <c>CollectValues</c>. A part that is already on the path
        /// from the element is not visited again, so that a definition that contains a part of its own kind
        /// (<c>part def Node { part next : Node; }</c>) ends.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to read.</param>
        /// <param name="collector">The <see cref="ValueCollector"/> that receives the values found.</param>
        /// <param name="path">The identifiers of the parts between the first element and this one.</param>
        private static void CollectPartValues(IElement element, ValueCollector collector, HashSet<Guid> path)
        {
            var attribute = element.GetAttributeUsages().FirstOrDefault(candidate => candidate.DeclaredName == collector.AttributeName);
            var value = attribute?.GetNumericValue();

            if (value != null)
            {
                var margin = element.GetAttributeUsages().FirstOrDefault(candidate => candidate.DeclaredName == collector.AttributeName + MarginSuffix)?.GetNumericValue();

                collector.Contributions.Add(new Contribution(element.Id, element.qualifiedName, element.GetTypeNames(), value.Number, value.Unit, margin?.Number));
                collector.ContributingTypeIds.UnionWith(GetTypeIds(element));

                return;
            }

            if (!path.Add(element.Id))
            {
                return;
            }

            // A type, such as a part or a part definition, has its parts among its features; a package owns its parts.
            IEnumerable<IElement> members = element is IType type ? type.feature ?? [] : element.ownedElement ?? [];
            var parts = members.OfType<IPartUsage>().ToList();

            // An attribute whose value is an expression, such as 2 * otherMass, has a value: only an attribute without any
            // value is to be defined.
            if (parts.Count == 0 && element is IPartUsage && (attribute == null || TermReader.GetValueExpression(attribute) == null))
            {
                collector.ValuelessParts.Add((element, attribute != null));
            }

            foreach (var part in parts)
            {
                CollectPartValues(part, collector, path);
            }

            path.Remove(element.Id);
        }

        /// <summary>
        /// Gets the identifiers of the types of an element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The <c>Id</c> of each type of the element, empty when it is not a typed <see cref="IFeature"/>.</returns>
        private static IEnumerable<Guid> GetTypeIds(IElement element)
        {
            return element is IFeature feature ? (feature.type ?? []).Select(type => type.Id) : [];
        }

        /// <summary>
        /// What <c>CollectValues</c> finds while it visits the parts.
        /// </summary>
        /// <param name="AttributeName">The declared name of the attribute.</param>
        private sealed record ValueCollector(string AttributeName)
        {
            /// <summary>
            /// Gets the <see cref="Contribution"/> of each part that gives a value.
            /// </summary>
            public List<Contribution> Contributions { get; } = [];

            /// <summary>
            /// Gets the <c>Id</c> of the types of the parts that give a value.
            /// </summary>
            public HashSet<Guid> ContributingTypeIds { get; } = [];

            /// <summary>
            /// Gets the parts without sub-parts and without value, and whether each one declares the attribute.
            /// </summary>
            public List<(IElement Part, bool DeclaresAttribute)> ValuelessParts { get; } = [];
        }
    }
}
