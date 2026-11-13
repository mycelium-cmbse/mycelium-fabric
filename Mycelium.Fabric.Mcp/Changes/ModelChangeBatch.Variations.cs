// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Variations.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.Root.Namespaces;

    /// <content>
    /// The catalogues of variants, which build, as the textual notation does, <c>variation part def BatteryOptions :> Battery
    /// { variant part liIon40; }</c> and <c>part battery : BatteryOptions = BatteryOptions::liIon40;</c>: a catalogue is a
    /// variation definition, abstract, that owns its variants through <c>VariantMembership</c>s; a part typed by a catalogue
    /// takes a variant through a <c>FeatureValue</c> that references it.
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The role of the variant that a part takes, in the messages.
        /// </summary>
        private const string VariantRole = "variant";

        /// <summary>
        /// Tells whether an element is a catalogue of variants: a part definition that is a variation.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns><c>true</c> when the element is a catalogue.</returns>
        private static bool IsCatalogue(IElement element)
        {
            return element is IPartDefinition { IsVariation: true };
        }

        /// <summary>
        /// Checks, before an element is created, what its payload says about catalogues: only a part definition is a
        /// catalogue, and a catalogue only owns its variants, which are parts.
        /// </summary>
        /// <param name="member">The element to create.</param>
        /// <param name="owner">The element that owns it.</param>
        /// <param name="payload">The properties of the element.</param>
        /// <exception cref="InvalidChangeException">Thrown when the payload cannot be applied.</exception>
        private static void CheckVariationProperties(IElement member, IElement owner, ElementPayload payload)
        {
            if (payload.IsVariation == true && member is not IPartDefinition)
            {
                throw new InvalidChangeException($"Only a PartDefinition is a catalogue of variants (isVariation), not the {Describe(member)}.");
            }

            if (IsCatalogue(owner) && member is not IPartUsage)
            {
                throw new InvalidChangeException($"The {Describe(owner)} is a catalogue: it only owns its variants, which are parts (PartUsage), not the {Describe(member)}. "
                    + "Give the attributes to the definition that the catalogue specializes, and their values to each variant.");
            }
        }

        /// <summary>
        /// Makes a created part definition a catalogue: a variation, which the specification requires to be abstract.
        /// </summary>
        /// <param name="member">The created element.</param>
        /// <param name="payload">The properties of the element.</param>
        private static void SetVariation(IElement member, ElementPayload payload)
        {
            if (payload.IsVariation == true && member is IPartDefinition catalogue)
            {
                catalogue.IsVariation = true;
                catalogue.IsAbstract = true;
            }
        }

        /// <summary>
        /// Types a variant by its catalogue, as the specification requires (a variant specializes the variation that owns
        /// it), unless its definition is already the catalogue.
        /// </summary>
        /// <param name="member">The created element.</param>
        /// <param name="owner">The element that owns it.</param>
        /// <param name="definition">The definition that the payload gives to the element, or <c>null</c>.</param>
        private void AddCatalogueTyping(IElement member, IElement owner, IElement definition)
        {
            if (IsCatalogue(owner) && definition?.Id != owner.Id)
            {
                this.AddTyping(member, owner);
            }
        }

        /// <summary>
        /// Finds the variant that a part takes: a variant of a catalogue that types the part, given by its name in this
        /// catalogue, its identifier or its temporary name.
        /// </summary>
        /// <param name="element">The part.</param>
        /// <param name="newDefinition">The definition that the same change gives to the part, or <c>null</c>.</param>
        /// <param name="reference">The name, the identifier or the temporary name of the variant.</param>
        /// <returns>The variant.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the part has no catalogue, or the variant is not one of its variants.</exception>
        private IElement ResolveVariant(IElement element, IElement newDefinition, string reference)
        {
            if (element is not IPartUsage)
            {
                throw new InvalidChangeException($"Only a part takes a variant, not the {Describe(element)}.");
            }

            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new InvalidChangeException($"The {VariantRole} is missing.");
            }

            var definitions = newDefinition != null
                ? [newDefinition]
                : this.GetElements(element.OwnedRelationship).OfType<IFeatureTyping>().Select(typing => this.elementsById.GetValueOrDefault(typing.Type)).ToList();

            var catalogues = definitions.Where(IsCatalogue).ToList();

            if (catalogues.Count == 0)
            {
                throw new InvalidChangeException($"The {Describe(element)} is not typed by a catalogue (a PartDefinition with isVariation): give it the catalogue as definition, then the variant.");
            }

            var variants = catalogues.SelectMany(this.GetVariants).ToList();
            var named = variants.Where(variant => variant.DeclaredName == reference).ToList();

            if (named.Count == 1)
            {
                return named[0];
            }

            var names = string.Join(", ", variants.Select(variant => $"'{variant.DeclaredName}'"));

            if (!Guid.TryParse(reference, out _) && !this.temporaryNames.ContainsKey(reference) && !this.earlierTemporaryNames.ContainsKey(reference))
            {
                throw new InvalidChangeException($"'{reference}' is not a variant of the {Describe(catalogues[0])}: its variants are {names}.");
            }

            var resolved = this.Resolve(reference, VariantRole);

            return variants.Exists(variant => variant.Id == resolved.Id)
                ? resolved
                : throw new InvalidChangeException($"The {Describe(resolved)} is not a variant of the {Describe(catalogues[0])}: its variants are {names}.");
        }

        /// <summary>
        /// Gets the variants that a catalogue owns.
        /// </summary>
        /// <param name="catalogue">The catalogue.</param>
        /// <returns>The variants.</returns>
        private IEnumerable<IElement> GetVariants(IElement catalogue)
        {
            return this.GetElements(catalogue.OwnedRelationship)
                .OfType<IVariantMembership>()
                .SelectMany(membership => this.GetElements(membership.OwnedRelatedElement))
                .Where(variant => !this.IsMarkedForDeletion(variant));
        }

        /// <summary>
        /// Makes a part take a variant, as <c>= BatteryOptions::liIon40</c> does: a <c>FeatureValue</c> owned by the part,
        /// whose expression references the variant. It replaces the variant that the part takes.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="variant">The variant.</param>
        private void SetVariant(IElement part, IElement variant)
        {
            this.RemoveOwnedRelationships<IFeatureValue>(part);
            this.AddOwnedRelationship(part, new FeatureValue { Visibility = VisibilityKind.Public }, this.CreateReferenceOperand(variant));
        }
    }
}
