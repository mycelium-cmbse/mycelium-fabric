// ------------------------------------------------------------------------------------------------
//  <copyright file="BatchNotes.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Writes the notes of an applied batch, which tell the AI assistant what it may not have meant: a new value of a
    /// definition changes every usage that shares it, and a redefinition with a type that does not specialize the type of the
    /// redefined feature makes the usage of both types. A note does not prevent the batch from being applied.
    /// </summary>
    public static class BatchNotes
    {
        /// <summary>
        /// The number of usages named in a note.
        /// </summary>
        private const int NamedUsageCount = 5;

        /// <summary>
        /// Writes the notes of a batch, once its commit has been applied to the model.
        /// </summary>
        /// <param name="commitRequest">The <see cref="CommitRequest"/> of the batch.</param>
        /// <param name="previousElementIds">The <c>Id</c> of the elements of the model before the batch.</param>
        /// <param name="elementsById">The elements of the model after the batch, indexed by their <c>Id</c>.</param>
        /// <returns>The notes, empty when there is none.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="commitRequest"/>, <paramref name="previousElementIds"/> or <paramref name="elementsById"/>
        /// is <c>null</c>.
        /// </exception>
        public static IReadOnlyList<string> Describe(CommitRequest commitRequest, IReadOnlySet<Guid> previousElementIds, IReadOnlyDictionary<Guid, IElement> elementsById)
        {
            ArgumentNullException.ThrowIfNull(commitRequest);
            ArgumentNullException.ThrowIfNull(previousElementIds);
            ArgumentNullException.ThrowIfNull(elementsById);

            var createdElements = commitRequest.Change
                .Where(dataVersion => dataVersion.Payload != null && !previousElementIds.Contains(dataVersion.Identity.Id))
                .Select(dataVersion => elementsById.GetValueOrDefault(dataVersion.Identity.Id))
                .Where(element => element != null)
                .ToList();

            // A value created for an attribute that was already there is a new value, which its usages share.
            var sharedValueNotes = createdElements
                .OfType<IFeatureValue>()
                .Select(featureValue => featureValue.featureWithValue)
                .Where(attribute => attribute != null && previousElementIds.Contains(attribute.Id) && attribute.owningType is IDefinition)
                .DistinctBy(attribute => attribute.Id)
                .Select(attribute => DescribeSharedValue(attribute, elementsById.Values))
                .Where(note => note != null);

            var addedTypeNotes = createdElements
                .OfType<IFeature>()
                .SelectMany(feature => feature.GetTypesAddedByRedefinition().Select(types =>
                    $"'{types.Added.name}' does not specialize '{types.Redefined.name}': the {feature.GetType().Name} '{feature.qualifiedName}' is now both, with the features of both. "
                    + $"If a {types.Added.name} is a kind of {types.Redefined.name}, make {types.Added.name} specialize it (specializes)."));

            return [.. sharedValueNotes, .. addedTypeNotes];
        }

        /// <summary>
        /// Describes the usages that share the new value of an attribute of a definition, when there are some.
        /// </summary>
        /// <param name="attribute">The attribute whose value has changed.</param>
        /// <param name="elements">The elements of the model.</param>
        /// <returns>The note, or <c>null</c> when no usage shares the value.</returns>
        private static string DescribeSharedValue(IFeature attribute, IEnumerable<IElement> elements)
        {
            var usages = attribute.GetInheritingUsages(elements);

            if (usages.Count == 0)
            {
                return null;
            }

            var names = string.Join(", ", usages.Select(usage => usage.qualifiedName).Order(StringComparer.Ordinal).Take(NamedUsageCount).Select(name => $"'{name}'"));
            var others = usages.Count > NamedUsageCount ? $" and {usages.Count - NamedUsageCount} others" : string.Empty;

            var usageCount = usages.Count == 1 ? "1 usage" : $"{usages.Count} usages";

            return $"The value of '{attribute.qualifiedName}' is shared by {usageCount} ({names}{others}): they all take the new value. "
                + $"To change the value of one usage only, keep the value of the definition and create under the usage an element that redefines '{attribute.name}' (redefines) with its own value.";
        }
    }
}
