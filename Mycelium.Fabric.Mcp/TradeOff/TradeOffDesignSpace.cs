// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffDesignSpace.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ErrorOr;

    using Mycelium.Fabric.Mcp.Expressions;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// The parts whose content a trade-off decides, with what each one may hold. It reads the model once with these parts
    /// left open, then fills them: with every option at once, to give the solver the totals as sums, or with the options of
    /// one combination, to check a requirement on it with the values that a visit of the changed model would find.
    /// </summary>
    public sealed class TradeOffDesignSpace
    {
        /// <summary>
        /// The <c>Id</c> of the element under which the equipment to add counts.
        /// </summary>
        private readonly Guid rootId;

        /// <summary>
        /// The open parts, in the order of the choices.
        /// </summary>
        private readonly IReadOnlyList<TradeOffOpenPart> openParts;

        /// <summary>
        /// The key of each open part, in the order of the choices: the <c>Id</c> of the part, or a new one for equipment to add.
        /// </summary>
        private readonly List<Guid> keys;

        /// <summary>
        /// The position of each open part, by key.
        /// </summary>
        private readonly Dictionary<Guid, int> indexes;

        /// <summary>
        /// The <c>Id</c>s of the open parts that are in the model.
        /// </summary>
        private readonly HashSet<Guid> openPartIds;

        /// <summary>
        /// What the visit of a part finds with the open parts left open, by part and attribute.
        /// </summary>
        private readonly Dictionary<(Guid PartId, string Attribute), ValueCollection> openCollections = [];

        /// <summary>
        /// What the visit of the source of a content finds, by source and attribute.
        /// </summary>
        private readonly Dictionary<(Guid SourceId, string Attribute), ValueCollection> sourceCollections = [];

        /// <summary>
        /// The nearest open part that owns a part, or <c>null</c>, by part.
        /// </summary>
        private readonly Dictionary<Guid, IFeature> openOwners = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="TradeOffDesignSpace"/> class.
        /// </summary>
        /// <param name="rootId">The <c>Id</c> of the element under which the totals are computed.</param>
        /// <param name="openParts">The <see cref="TradeOffOpenPart"/>s, in the order of the choices.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="openParts"/> is <c>null</c>.
        /// </exception>
        public TradeOffDesignSpace(Guid rootId, IReadOnlyList<TradeOffOpenPart> openParts)
        {
            ArgumentNullException.ThrowIfNull(openParts);

            this.rootId = rootId;
            this.openParts = openParts;
            this.keys = [.. openParts.Select(openPart => openPart.Part?.Id ?? Guid.NewGuid())];
            this.indexes = this.keys.Select((key, index) => (key, index)).ToDictionary(pair => pair.key, pair => pair.index);
            this.openPartIds = [.. openParts.Where(openPart => openPart.Part != null).Select(openPart => openPart.Part.Id)];
        }

        /// <summary>
        /// Gets the key of each open part, in the order of the choices: what a check reports to have read.
        /// </summary>
        public IReadOnlyList<Guid> Keys => this.keys;

        /// <summary>
        /// Creates what the open parts hold in one combination.
        /// </summary>
        /// <param name="selection">The index of the option taken for each choice.</param>
        /// <returns>The <see cref="IPartSelection"/> of the combination.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <c>null</c>.
        /// </exception>
        public IPartSelection CreateSelection(IReadOnlyList<int> selection)
        {
            ArgumentNullException.ThrowIfNull(selection);

            return new Selection(this, selection);
        }

        /// <summary>
        /// Splits the total of an attribute under a part into what no choice changes and what each option adds.
        /// </summary>
        /// <param name="part">The part, for example the root or the part that satisfies a requirement.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <returns>
        /// The <see cref="TradeOffTotalParts"/>, or an error when the part belongs to an open part: its total then depends
        /// on the choices in a way that is not a sum.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="part"/> is <c>null</c>.
        /// </exception>
        public ErrorOr<TradeOffTotalParts> GetTotalParts(IElement part, string attributeName)
        {
            ArgumentNullException.ThrowIfNull(part);

            if (this.GetOpenOwner(part) is { } owner)
            {
                return Error.Validation(description: $"'{part.DeclaredName}' belongs to '{owner.DeclaredName}', whose content is a choice.");
            }

            if (this.indexes.TryGetValue(part.Id, out var partIndex))
            {
                // The total of an open part itself is the one of one unit of what it holds.
                return new TradeOffTotalParts([], [.. this.openParts.Select((openPart, index) =>
                    (IReadOnlyList<IReadOnlyList<Contribution>>)[.. openPart.Contents.Select(content => index == partIndex ? this.CountUnit(index, content, attributeName, 1) : [])])]);
            }

            var collection = this.GetOpenCollection(part, attributeName);

            return new TradeOffTotalParts(collection.Contributions, [.. this.openParts.Select((openPart, index) =>
            {
                var count = this.CountPlaces(index, part, collection);

                return (IReadOnlyList<IReadOnlyList<Contribution>>)[.. openPart.Contents.Select(content => this.CountUnit(index, content, attributeName, count * content.Quantity))];
            })]);
        }

        /// <summary>
        /// Counts the places of an open part under a part: how many times the parts that hold it count, 1 for equipment to
        /// add under the root, 0 when the open part is not under the part.
        /// </summary>
        /// <param name="index">The position of the open part.</param>
        /// <param name="part">The part whose values are collected.</param>
        /// <param name="collection">What the visit of the part finds with the open parts left open.</param>
        /// <returns>The number of places.</returns>
        private int CountPlaces(int index, IElement part, ValueCollection collection)
        {
            var openPart = this.openParts[index].Part;

            if (openPart == null)
            {
                return part.Id == this.rootId ? 1 : 0;
            }

            return collection.Slots.Where(slot => slot.Part.Id == openPart.Id).Sum(slot => slot.Count);
        }

        /// <summary>
        /// Tells whether what an open part holds can change the values of an attribute: one of its options gives a value,
        /// carries the attribute without value or without a fixed number of units, or holds a part without value whose type
        /// gives a value elsewhere, which then misses its value.
        /// </summary>
        /// <param name="index">The position of the open part.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="contributingTypeIds">The <c>Id</c> of the types of the parts that give a value.</param>
        /// <returns><c>true</c> when the values depend on the option taken.</returns>
        private bool CanChange(int index, string attributeName, HashSet<Guid> contributingTypeIds)
        {
            return this.openParts[index].Contents
                .Where(content => !content.IsRemoved)
                .Select(content => this.GetUnitCollection(index, content, attributeName))
                .Any(unit => unit.Contributions.Count > 0 || unit.UncountedParts.Count > 0
                    || unit.ValuelessParts.Exists(valueless => valueless.DeclaresAttribute || ValueCollection.GetTypeIds(valueless.Part).Any(contributingTypeIds.Contains)));
        }

        /// <summary>
        /// Gets the contributions of one unit of a content, counted a number of times.
        /// </summary>
        /// <param name="index">The position of the open part.</param>
        /// <param name="content">The content of the open part.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <param name="count">The number of times that the unit counts.</param>
        /// <returns>The contributions, empty when the unit does not count.</returns>
        private List<Contribution> CountUnit(int index, TradeOffPartContent content, string attributeName, int count)
        {
            if (count <= 0 || content.Quantity == 0 || content.IsRemoved)
            {
                return [];
            }

            var counted = new ValueCollection(attributeName);
            counted.Add(this.GetUnitCollection(index, content, attributeName), count);

            return counted.Contributions;
        }

        /// <summary>
        /// Gets what the visit of a part finds with the open parts left open.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <returns>The <see cref="ValueCollection"/>, read once.</returns>
        private ValueCollection GetOpenCollection(IElement part, string attributeName)
        {
            if (!this.openCollections.TryGetValue((part.Id, attributeName), out var collection))
            {
                collection = part.CollectValueParts(attributeName, this.openPartIds);
                this.openCollections.Add((part.Id, attributeName), collection);
            }

            return collection;
        }

        /// <summary>
        /// Gets what one unit of a content gives for an attribute: the value that the content gives, or else what the visit of
        /// its source finds.
        /// </summary>
        /// <param name="index">The position of the open part.</param>
        /// <param name="content">The content of the open part.</param>
        /// <param name="attributeName">The declared name of the attribute.</param>
        /// <returns>The <see cref="ValueCollection"/>.</returns>
        private ValueCollection GetUnitCollection(int index, TradeOffPartContent content, string attributeName)
        {
            var openPart = this.openParts[index];

            if (content.Values != null && content.Values.TryGetValue(attributeName, out var value))
            {
                var given = new ValueCollection(attributeName);
                given.Contributions.Add(new Contribution(openPart.Part?.Id ?? Guid.Empty, openPart.Part?.qualifiedName ?? openPart.Name, openPart.Part?.GetTypeNames() ?? [], value.Number, value.Unit));

                return given;
            }

            if (content.Source == null)
            {
                return new ValueCollection(attributeName);
            }

            if (!this.sourceCollections.TryGetValue((content.Source.Id, attributeName), out var collection))
            {
                collection = content.Source.CollectValueParts(attributeName);
                this.sourceCollections.Add((content.Source.Id, attributeName), collection);
            }

            return collection;
        }

        /// <summary>
        /// Gets the nearest open part that owns a part.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns>The open part, or <c>null</c> when no open part owns the part.</returns>
        private IFeature GetOpenOwner(IElement part)
        {
            if (this.openOwners.TryGetValue(part.Id, out var openOwner))
            {
                return openOwner;
            }

            for (var owner = part.owner; owner != null && openOwner == null; owner = owner.owner)
            {
                openOwner = this.openPartIds.Contains(owner.Id) ? owner as IFeature : null;
            }

            this.openOwners.Add(part.Id, openOwner);

            return openOwner;
        }

        /// <summary>
        /// What the open parts hold in one combination.
        /// </summary>
        private sealed class Selection : IPartSelection
        {
            /// <summary>
            /// The <see cref="TradeOffDesignSpace"/> of the trade-off.
            /// </summary>
            private readonly TradeOffDesignSpace space;

            /// <summary>
            /// The index of the option taken for each choice.
            /// </summary>
            private readonly IReadOnlyList<int> selection;

            /// <summary>
            /// Initializes a new instance of the <see cref="Selection"/> class.
            /// </summary>
            /// <param name="space">The <see cref="TradeOffDesignSpace"/> of the trade-off.</param>
            /// <param name="selection">The index of the option taken for each choice.</param>
            public Selection(TradeOffDesignSpace space, IReadOnlyList<int> selection)
            {
                this.space = space;
                this.selection = selection;
            }

            /// <summary>
            /// Collects the values of an attribute over a part and its sub-parts in the combination: what the visit of the
            /// part finds outside the open parts, plus what each open part holds.
            /// </summary>
            /// <param name="part">The part.</param>
            /// <param name="attributeName">The declared name of the attribute.</param>
            /// <param name="readPartIds">Receives the key of each open part that is read.</param>
            /// <returns>The <see cref="CollectedValues"/>, or an error when the part is not in the combination.</returns>
            public ErrorOr<CollectedValues> CollectValues(IFeature part, string attributeName, ISet<Guid> readPartIds)
            {
                ArgumentNullException.ThrowIfNull(part);
                ArgumentNullException.ThrowIfNull(readPartIds);

                if (this.CheckOwner(part, readPartIds) is { } replaced)
                {
                    return replaced;
                }

                if (this.space.GetOpenOwner(part) != null)
                {
                    return part.CollectValues(attributeName);
                }

                if (this.space.indexes.TryGetValue(part.Id, out var partIndex))
                {
                    readPartIds.Add(part.Id);

                    return this.GetContent(partIndex).IsRemoved
                        ? NotTaken(part)
                        : this.space.GetUnitCollection(partIndex, this.GetContent(partIndex), attributeName).ToCollectedValues();
                }

                var collection = this.space.GetOpenCollection(part, attributeName);
                var values = new ValueCollection(attributeName);
                values.Add(collection, 1);

                List<int> placedIndexes = [];

                for (var index = 0; index < this.space.openParts.Count; index++)
                {
                    var count = this.space.CountPlaces(index, part, collection);

                    if (count == 0)
                    {
                        continue;
                    }

                    placedIndexes.Add(index);

                    var content = this.GetContent(index);

                    // A part taken 0 times stays in the model and counts for 0; a removed part is not there at all.
                    if (!content.IsRemoved)
                    {
                        values.Add(this.space.GetUnitCollection(index, content, attributeName), count * content.Quantity);
                    }
                }

                // The values only depend on the open parts that one of their options changes.
                foreach (var index in placedIndexes.Where(index => this.space.CanChange(index, attributeName, values.ContributingTypeIds)))
                {
                    readPartIds.Add(this.space.keys[index]);
                }

                return values.ToCollectedValues();
            }

            /// <summary>
            /// Gets the feature whose features stand for those of a part in the combination: the variant that an open part
            /// takes, the variant that another part takes in the model, or the part itself.
            /// </summary>
            /// <param name="part">The part.</param>
            /// <param name="readPartIds">Receives the key of the part when it is open.</param>
            /// <returns>The feature to navigate, or an error when the part is not in the combination.</returns>
            public ErrorOr<IFeature> Resolve(IFeature part, ISet<Guid> readPartIds)
            {
                ArgumentNullException.ThrowIfNull(part);
                ArgumentNullException.ThrowIfNull(readPartIds);

                if (this.CheckOwner(part, readPartIds) is { } replaced)
                {
                    return replaced;
                }

                if (!this.space.indexes.TryGetValue(part.Id, out var partIndex))
                {
                    return ErrorOrFactory.From<IFeature>(part.GetChosenVariant() ?? part);
                }

                readPartIds.Add(part.Id);

                var content = this.GetContent(partIndex);

                if (content.IsRemoved)
                {
                    return NotTaken(part);
                }

                return ErrorOrFactory.From(content.IsVariant ? content.Source : part.GetChosenVariant() ?? part);
            }

            /// <summary>
            /// Gets the value that the combination gives to an attribute of an open part.
            /// </summary>
            /// <param name="part">The part.</param>
            /// <param name="attributeName">The declared name of the attribute.</param>
            /// <param name="readPartIds">Receives the key of the part when it is open.</param>
            /// <returns>The value, or <c>null</c> when the combination gives none.</returns>
            public NumberValue GetGivenValue(IFeature part, string attributeName, ISet<Guid> readPartIds)
            {
                ArgumentNullException.ThrowIfNull(part);
                ArgumentNullException.ThrowIfNull(readPartIds);

                if (!this.space.indexes.TryGetValue(part.Id, out var partIndex))
                {
                    return null;
                }

                readPartIds.Add(part.Id);

                var content = this.GetContent(partIndex);

                return !content.IsRemoved && content.Values != null && content.Values.TryGetValue(attributeName, out var value) ? value : null;
            }

            /// <summary>
            /// Creates the error of a part that the combination does not take.
            /// </summary>
            /// <param name="part">The part.</param>
            /// <returns>The error.</returns>
            private static Error NotTaken(IFeature part)
            {
                return Error.Validation(description: $"'{part.DeclaredName}' is not taken in this combination.");
            }

            /// <summary>
            /// Gets what an open part holds in the combination.
            /// </summary>
            /// <param name="index">The position of the open part.</param>
            /// <returns>The <see cref="TradeOffPartContent"/>.</returns>
            private TradeOffPartContent GetContent(int index)
            {
                return this.space.openParts[index].Contents[this.selection[index]];
            }

            /// <summary>
            /// Checks that the open part that owns a part, when there is one, keeps what it holds in the model: the sub-parts
            /// of a part that the combination replaces or removes are not in the combination.
            /// </summary>
            /// <param name="part">The part.</param>
            /// <param name="readPartIds">Receives the key of the open part that owns the part.</param>
            /// <returns>The error, or <c>null</c> when the part is in the combination.</returns>
            private Error? CheckOwner(IFeature part, ISet<Guid> readPartIds)
            {
                if (this.space.GetOpenOwner(part) is not { } owner)
                {
                    return null;
                }

                readPartIds.Add(owner.Id);

                return this.GetContent(this.space.indexes[owner.Id]).IsCurrent
                    ? null
                    : Error.Validation(description: $"'{part.DeclaredName}' belongs to '{owner.DeclaredName}', which this combination replaces or removes.");
            }
        }
    }
}
