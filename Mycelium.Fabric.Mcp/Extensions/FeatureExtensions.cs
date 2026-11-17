// ------------------------------------------------------------------------------------------------
//  <copyright file="FeatureExtensions.cs" company="Starion Group S.A.">
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
    using System.Globalization;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Expressions;

    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.Multiplicities;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Enumerations;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// Extension methods that read, from the <see cref="IFeature"/>s of a SysML v2 model, the information used by the MCP
    /// tools.
    /// </summary>
    public static class FeatureExtensions
    {
        /// <param name="feature">The <see cref="IFeature"/> to read.</param>
        extension(IFeature feature)
        {
            /// <summary>
            /// Gets the parts next to the feature, outside it: the other parts that its package, or the root namespace, owns
            /// directly. A feature of a type, such as a part inside the system, has none, since the parts next to it belong
            /// to the same owner.
            /// </summary>
            /// <returns>The parts next to the feature, empty when its owner is a type or when it has no owner.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<IPartUsage> GetPartsBeside()
            {
                ArgumentNullException.ThrowIfNull(feature);

                return feature.owningNamespace is { } owner and not IType
                    ? [.. (owner.ownedElement ?? []).OfType<IPartUsage>().Where(part => part.Id != feature.Id)]
                    : [];
            }

            /// <summary>
            /// Gets the documentation of the feature or, when it has none, the one of the feature that it redefines: the value
            /// <c>:&gt;&gt; mass = 35;</c> of a part keeps the documentation of the <c>mass</c> of its definition, such as
            /// <c>Dry mass of the unit [kg].</c>
            /// </summary>
            /// <returns>The documentation bodies, joined in a single text, or <c>null</c> when none of these features has any.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public string GetOwnOrRedefinedDocumentation()
            {
                ArgumentNullException.ThrowIfNull(feature);

                return feature.AllRedefinedFeatures()
                    .Select(redefinedFeature => redefinedFeature.GetDocumentationBodies())
                    .FirstOrDefault(documentation => documentation != null);
            }

            /// <summary>
            /// Gets the usages of a model that have the feature among their features (<c>feature</c>): the usages typed by the type
            /// that owns it, directly or through what specializes this type, that do not redefine it. For the <c>mass</c> of
            /// <c>Camera</c>, they are the parts typed by <c>Camera</c> that have no <c>:&gt;&gt; mass</c> of their own.
            /// </summary>
            /// <param name="elements">The elements of the model.</param>
            /// <returns>The usages, in the order of the model; empty when the feature has no owning type.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> or <paramref name="elements"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<IUsage> GetInheritingUsages(IEnumerable<IElement> elements)
            {
                ArgumentNullException.ThrowIfNull(feature);
                ArgumentNullException.ThrowIfNull(elements);

                if (feature.owningType is not { } owningType)
                {
                    return [];
                }

                return [.. elements.OfType<IUsage>().Where(usage => usage.Id != owningType.Id && usage.Specializes(owningType) && (usage.feature ?? []).Any(candidate => candidate.Id == feature.Id))];
            }

            /// <summary>
            /// Gets the types that the feature adds, through its own typings, to the types of the features that it redefines,
            /// without specializing them: <c>part :&gt;&gt; engine : Battery;</c>, when <c>Battery</c> does not specialize the
            /// <c>Engine</c> of the redefined <c>engine</c>, makes the part both an <c>Engine</c> and a <c>Battery</c>.
            /// </summary>
            /// <returns>Each added type with the type of the redefined feature that it does not specialize.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<(IType Added, IType Redefined)> GetTypesAddedByRedefinition()
            {
                ArgumentNullException.ThrowIfNull(feature);

                var ownTypes = (feature.ownedTyping ?? []).Select(typing => typing.Type).Where(type => type != null).ToList();

                return [.. from redefinition in feature.ownedRedefinition ?? []
                    from redefinedType in redefinition.RedefinedFeature?.type ?? []
                    from ownType in ownTypes
                    where ownType.Id != redefinedType.Id && !ownType.Specializes(redefinedType)
                    select (ownType, redefinedType)];
            }

            /// <summary>
            /// Describes the multiplicity of the feature as the textual notation writes it between brackets. It is the derived
            /// <c>multiplicity</c> of SysML2.NET: the multiplicity that the feature owns, or else the one of what it
            /// specializes.
            /// </summary>
            /// <returns>
            /// The multiplicity, for example <c>4</c>, <c>1..4</c> or <c>0..*</c>, with <c>?</c> for a bound that is not a
            /// number, or <c>null</c> when the feature has none.
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public string DescribeMultiplicity()
            {
                ArgumentNullException.ThrowIfNull(feature);

                if (feature.multiplicity is not IMultiplicityRange range || (range.bound ?? []).Count == 0)
                {
                    return null;
                }

                var bounds = range.bound.Select(bound => bound switch
                {
                    ILiteralInteger integer => integer.Value.ToString(CultureInfo.InvariantCulture),
                    ILiteralInfinity => "*",
                    _ => "?"
                });

                return string.Join("..", bounds);
            }

            /// <summary>
            /// Gets the variants among which the feature takes one: the variants of the catalogues that type it. A catalogue
            /// is a variation definition (<c>variation part def BatteryOptions</c>), which only owns its variants; an
            /// enumeration definition, whose values are variants too, is not a catalogue. A variant is typed by its catalogue
            /// but takes no variant itself.
            /// </summary>
            /// <returns>The variants, empty when no catalogue types the feature or when it is itself a variant.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public IReadOnlyList<IUsage> GetVariants()
            {
                ArgumentNullException.ThrowIfNull(feature);

                if (feature.owningMembership is IVariantMembership)
                {
                    return [];
                }

                try
                {
                    return [.. (feature.type ?? []).OfType<IDefinition>().Where(definition => definition.IsVariation && definition is not IEnumerationDefinition).SelectMany(definition => definition.variant ?? [])];
                }
                catch (IncompleteModelException)
                {
                    return [];
                }
            }

            /// <summary>
            /// Gets the variant that the feature takes: the variant of one of its catalogues that its value names
            /// (<c>part battery : BatteryOptions = BatteryOptions::liIon40</c>).
            /// </summary>
            /// <returns>The variant, or <c>null</c> when the feature takes none.</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public IUsage GetChosenVariant()
            {
                var variants = feature.GetVariants();

                return variants.Count > 0 && TermReader.GetValueExpression(feature) is IFeatureReferenceExpression { referent: IUsage referent }
                    ? variants.FirstOrDefault(variant => variant.Id == referent.Id)
                    : null;
            }

            /// <summary>
            /// Gets the number of units of the feature that its multiplicity fixes: <c>4</c> for <c>[4]</c> or
            /// <c>[4..4]</c>, and <c>1</c> when it has no multiplicity, as a usage declared without one.
            /// </summary>
            /// <returns>
            /// The number of units, or <c>null</c> when the multiplicity does not fix it (<c>[1..4]</c>, <c>[0..*]</c>, or a
            /// bound that is not a number).
            /// </returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when <paramref name="feature"/> is <c>null</c>.
            /// </exception>
            public int? GetUnitCount()
            {
                ArgumentNullException.ThrowIfNull(feature);

                return feature.multiplicity switch
                {
                    null => 1,
                    IMultiplicityRange { lowerBound: null, upperBound: ILiteralInteger upper } => upper.Value,
                    IMultiplicityRange { lowerBound: ILiteralInteger lower, upperBound: ILiteralInteger upper } when lower.Value == upper.Value => upper.Value,
                    _ => null
                };
            }
        }
    }
}
