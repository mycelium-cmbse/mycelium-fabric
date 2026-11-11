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
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Exceptions;

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
            /// <returns>
            /// The names of the types of the element, empty when it is not a typed <see cref="IFeature"/>, or when SysML2.NET
            /// cannot compute its types because the model is incomplete (for example an <c>EnumerationUsage</c> that no
            /// enumeration definition types).
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<string> GetTypeNames()
            {
                ArgumentNullException.ThrowIfNull(element);

                if (element is not IFeature feature)
                {
                    return [];
                }

                try
                {
                    return [.. (feature.type ?? []).Select(type => type.name)];
                }
                catch (IncompleteModelException)
                {
                    return [];
                }
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
            /// counts the engines of <c>Stage</c>). A part counts as many times as its multiplicity and the ones of the parts
            /// that hold it under the element say (<c>part wheels : Wheel[4]</c>); the multiplicity of the element itself is
            /// not counted. A part without sub-parts and without value misses its value ("to be defined") when it declares
            /// the attribute without any value, or when it has the type of a contributing part. A part that carries the
            /// attribute but whose number of units is not fixed (<c>[1..4]</c>, <c>[0..*]</c>) is not counted and misses its
            /// value too. A part that takes a variant of a catalogue (<c>part battery : BatteryOptions = BatteryOptions::liIon40</c>)
            /// holds what this variant holds; a part typed by a catalogue that takes no variant yet misses its value; a part
            /// taken 0 times (<c>[0]</c>), such as an optional tank that is not on board, counts for 0.
            /// </summary>
            /// <param name="attributeName">The declared name of the attribute.</param>
            /// <param name="openPartIds">
            /// The <c>Id</c> of the parts left open, or <c>null</c> for none: an open part is not visited, and its place is given
            /// as a <see cref="PartSlot"/>, so that a trade-off can fill it with each of its variants.
            /// </param>
            /// <returns>
            /// The <see cref="CollectedValues"/>: the contributions, the parts whose value is missing, and the open parts.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            public CollectedValues CollectValues(string attributeName, IReadOnlySet<Guid> openPartIds = null)
            {
                return element.CollectValueParts(attributeName, openPartIds).ToCollectedValues();
            }

            /// <summary>
            /// Collects the values of an attribute over the element and its sub-parts, as <c>CollectValues</c> does, without
            /// deciding yet which parts miss their value, so that what the open parts hold can be added first.
            /// </summary>
            /// <param name="attributeName">The declared name of the attribute.</param>
            /// <param name="openPartIds">The <c>Id</c> of the parts left open, or <c>null</c> for none.</param>
            /// <returns>The <see cref="ValueCollection"/>.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="element"/> is <c>null</c>.
            /// </exception>
            internal ValueCollection CollectValueParts(string attributeName, IReadOnlySet<Guid> openPartIds = null)
            {
                ArgumentNullException.ThrowIfNull(element);

                var collector = new ValueCollection(attributeName, openPartIds);
                CollectPartValues(element, collector, [], 1);

                return collector;
            }
        }

        /// <summary>
        /// Collects the values of an element and of its sub-parts, for <c>CollectValues</c>. A part that is already on the path
        /// from the element is not visited again, so that a definition that contains a part of its own kind
        /// (<c>part def Node { part next : Node; }</c>) ends.
        /// </summary>
        /// <param name="element">The <see cref="IElement"/> to read.</param>
        /// <param name="collector">The <see cref="ValueCollection"/> that receives the values found.</param>
        /// <param name="path">The identifiers of the parts between the first element and this one.</param>
        /// <param name="count">The number of units of the element under the first element.</param>
        private static void CollectPartValues(IElement element, ValueCollection collector, HashSet<Guid> path, int count)
        {
            if (element is IPartUsage choice && choice.GetVariants() is { Count: > 0 } variants)
            {
                CollectVariantValues(choice, variants, collector, path, count);

                return;
            }

            var attribute = element.GetAttributeUsages().FirstOrDefault(candidate => candidate.DeclaredName == collector.AttributeName);
            var value = attribute?.GetNumericValue();

            if (value != null)
            {
                var margin = element.GetAttributeUsages().FirstOrDefault(candidate => candidate.DeclaredName == collector.AttributeName + MarginSuffix)?.GetNumericValue();

                collector.Contributions.Add(new Contribution(element.Id, element.qualifiedName, element.GetTypeNames(), value.Number, value.Unit, margin?.Number, count == 1 ? null : count));
                collector.ContributingTypeIds.UnionWith(ValueCollection.GetTypeIds(element));

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
                if (collector.OpenPartIds?.Contains(part.Id) == true)
                {
                    collector.Slots.Add(new PartSlot(part, count));
                    continue;
                }

                if (part.GetUnitCount() is { } unitCount)
                {
                    CollectPartValues(part, collector, path, count * unitCount);
                    continue;
                }

                // A part whose number of units is not fixed is not counted, and is reported only when it carries the attribute.
                var uncountedValues = new ValueCollection(collector.AttributeName);
                CollectPartValues(part, uncountedValues, path, 1);

                if (uncountedValues.Contributions.Count > 0 || uncountedValues.UncountedParts.Count > 0 || uncountedValues.ValuelessParts.Exists(valueless => valueless.DeclaresAttribute))
                {
                    collector.UncountedParts.Add(part);
                }
            }

            path.Remove(element.Id);
        }

        /// <summary>
        /// Collects the values of a part typed by a catalogue: the values of the variant that it takes, or a missing value
        /// when it takes none yet and one of the variants carries the attribute.
        /// </summary>
        /// <param name="choice">The part typed by a catalogue.</param>
        /// <param name="variants">The variants of the catalogue.</param>
        /// <param name="collector">The <see cref="ValueCollection"/> that receives the values found.</param>
        /// <param name="path">The identifiers of the parts between the first element and this one.</param>
        /// <param name="count">The number of units of the part under the first element.</param>
        private static void CollectVariantValues(IPartUsage choice, IReadOnlyList<IUsage> variants, ValueCollection collector, HashSet<Guid> path, int count)
        {
            if (!path.Add(choice.Id))
            {
                return;
            }

            if (choice.GetChosenVariant() is { } chosenVariant)
            {
                CollectPartValues(chosenVariant, collector, path, count);
            }
            else
            {
                collector.ValuelessParts.Add((choice, variants.Any(variant => variant.CollectContributions(collector.AttributeName).Count > 0)));
            }

            path.Remove(choice.Id);
        }
    }
}
