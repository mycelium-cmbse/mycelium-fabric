// ------------------------------------------------------------------------------------------------
//  <copyright file="ValueCollection.cs" company="Starion Group S.A.">
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
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// What <c>CollectValues</c> finds while it visits the parts, before it decides which parts miss their value. Several
    /// collections add up, so that a trade-off fills the open parts of one collection with what each of its variants holds
    /// and gets what a single visit of the changed model would find.
    /// </summary>
    internal sealed class ValueCollection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValueCollection"/> class.
        /// </summary>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="openPartIds">The <c>Id</c> of the parts left open, or <c>null</c> for none.</param>
        public ValueCollection(string attributeName, IReadOnlySet<Guid> openPartIds = null)
        {
            this.AttributeName = attributeName;
            this.OpenPartIds = openPartIds;
        }

        /// <summary>
        /// Gets the declared name of the attribute.
        /// </summary>
        public string AttributeName { get; }

        /// <summary>
        /// Gets the <c>Id</c> of the parts left open, or <c>null</c> for none.
        /// </summary>
        public IReadOnlySet<Guid> OpenPartIds { get; }

        /// <summary>
        /// Gets the open parts found, with the number of times that the parts that hold them count.
        /// </summary>
        public List<PartSlot> Slots { get; } = [];

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

        /// <summary>
        /// Gets the parts that carry the attribute but whose number of units is not fixed, which are not counted.
        /// </summary>
        public List<IPartUsage> UncountedParts { get; } = [];

        /// <summary>
        /// Gets the identifiers of the types of an element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The <c>Id</c> of each type of the element, empty when it is not a typed <see cref="IFeature"/>.</returns>
        public static IEnumerable<Guid> GetTypeIds(IElement element)
        {
            return element is IFeature feature ? (feature.type ?? []).Select(type => type.Id) : [];
        }

        /// <summary>
        /// Adds what another collection found, each of its parts counting a number of times.
        /// </summary>
        /// <param name="other">The other collection.</param>
        /// <param name="count">The number of times that the parts of the other collection count, at least 1.</param>
        public void Add(ValueCollection other, int count)
        {
            this.Contributions.AddRange(other.Contributions.Select(contribution => count == 1 ? contribution : contribution with { Count = (contribution.Count ?? 1) * count }));
            this.ContributingTypeIds.UnionWith(other.ContributingTypeIds);
            this.ValuelessParts.AddRange(other.ValuelessParts);
            this.UncountedParts.AddRange(other.UncountedParts);
        }

        /// <summary>
        /// Decides which parts miss their value: a part without sub-parts and without value that declares the attribute or
        /// has the type of a contributing part, and a part that carries the attribute but whose number of units is not fixed.
        /// </summary>
        /// <returns>The <see cref="CollectedValues"/>.</returns>
        public CollectedValues ToCollectedValues()
        {
            var missingValues = this.ValuelessParts
                .Where(part => part.DeclaresAttribute || GetTypeIds(part.Part).Any(this.ContributingTypeIds.Contains))
                .Select(part => new MissingValue(part.Part.Id, part.Part.qualifiedName, part.Part.GetTypeNames()))
                .Concat(this.UncountedParts.Select(part => new MissingValue(part.Id, part.qualifiedName, part.GetTypeNames(), part.DescribeMultiplicity())))
                .ToList();

            return new CollectedValues(this.Contributions, missingValues, this.Slots);
        }
    }
}
