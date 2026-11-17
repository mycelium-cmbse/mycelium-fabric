// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Redefinitions.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Actions;
    using SysML2.NET.Core.DTO.Systems.Connections;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Enumerations;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.DTO.Systems.States;

    using PocoFeature = SysML2.NET.Core.POCO.Core.Features.IFeature;
    using PocoType = SysML2.NET.Core.POCO.Core.Types.IType;

    /// <content>
    /// The redefinitions, which give a usage its own value, definition or multiplicity without changing its definition, as
    /// the textual notation writes <c>part camera : Camera { :&gt;&gt; mass = 35 [kg]; }</c>: a feature owned by the usage,
    /// without declared name, whose <c>Redefinition</c> designates the feature that the usage inherits. Also the features
    /// that a type has, owned or inherited, and their name, which the changes look up, read on the POCOs that SysML2.NET
    /// builds from the working copy, and the kind of a value: only a <c>default</c> value can be redefined (SysML rule
    /// <c>validateFeatureValueOverriding</c>).
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The role, in the messages, of the feature that a created feature redefines.
        /// </summary>
        private const string RedefinedRole = "redefined feature";

        /// <summary>
        /// The number of inherited features named in a message that lists them.
        /// </summary>
        private const int InheritedFeatureCount = 10;

        /// <summary>
        /// Checks the properties of a payload that only apply to a redefinition or a value, when an element is created.
        /// </summary>
        /// <param name="member">The created element, which has no name yet.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">Thrown when a property does not apply to the element.</exception>
        private static void CheckRedefinitionProperties(IElement member, ElementPayload payload)
        {
            if (payload.Redefines != null && (member is not IUsage || member is IConnectorAsUsage or ITransitionUsage or ISatisfyRequirementUsage or IPerformActionUsage || payload.StateAction != null))
            {
                throw new InvalidChangeException($"Only a usage, for example an attribute, a part, a port or an item, redefines a feature that its owner inherits, not a {member.GetType().Name}.");
            }

            if (payload.IsDefault != null && payload.Value == null)
            {
                throw new InvalidChangeException("isDefault only applies with a value: give the value of the attribute.");
            }
        }

        /// <summary>
        /// Gets the inherited feature that a created element redefines: the one that <c>redefines</c> designates, or else the
        /// one that has the name of the element, since a usage cannot have two features with the same name.
        /// </summary>
        /// <param name="member">The created element, which has no name yet.</param>
        /// <param name="owner">The owner of the created element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <returns>The redefined feature, or <c>null</c> when the element redefines none.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the redefined feature is not a feature that the owner inherits, or when the element cannot redefine it.
        /// </exception>
        private IElement ResolveRedefinedFeature(IElement member, IElement owner, ElementPayload payload)
        {
            if (payload.Redefines != null)
            {
                return this.ResolveGivenRedefinedFeature(member, owner, payload.Redefines);
            }

            if (member is not IUsage || !CanInherit(owner))
            {
                return null;
            }

            var inherited = this.GetInheritedFeatures(owner).FirstOrDefault(feature => this.GetEffectiveName(feature) == payload.Name && !this.IsMarkedForDeletion(feature));

            if (inherited != null && !HasMetaclassOf(member, inherited))
            {
                throw new InvalidChangeException($"The {Describe(owner)} inherits the {this.DescribeFeature(inherited)}, so a new element named '{payload.Name}' under it redefines it, "
                    + $"and must have the type {inherited.GetType().Name}, not {member.GetType().Name}. Give it another name for a new feature.");
            }

            return inherited;
        }

        /// <summary>
        /// Gets the inherited feature that <c>redefines</c> designates by an identifier, a temporary name or its name. When the
        /// owner inherits it through a feature that already redefines it, that feature is the one redefined.
        /// </summary>
        /// <param name="member">The created element, which has no name yet.</param>
        /// <param name="owner">The owner of the created element.</param>
        /// <param name="reference">The <c>Id</c>, temporary name or name of the redefined feature.</param>
        /// <returns>The redefined feature.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the owner inherits no such feature, when it already redefines it, or when the element cannot redefine it.
        /// </exception>
        private IElement ResolveGivenRedefinedFeature(IElement member, IElement owner, string reference)
        {
            if (!CanInherit(owner))
            {
                throw new InvalidChangeException($"Only an element owned by a definition or a usage redefines a feature that its owner inherits, and the {Describe(owner)} inherits none.");
            }

            var inheritedFeatures = this.GetInheritedFeatures(owner);

            // The name of an inherited feature comes before a temporary name, which can designate an element anywhere.
            IElement redefined = Guid.TryParse(reference, out _)
                ? null
                : inheritedFeatures.FirstOrDefault(feature => this.GetEffectiveName(feature) == reference && !this.IsMarkedForDeletion(feature));

            if (redefined == null)
            {
                var isReference = Guid.TryParse(reference, out _) || this.temporaryNames.ContainsKey(reference) || this.earlierTemporaryNames.ContainsKey(reference) || this.failedTemporaryNames.Contains(reference);

                redefined = isReference
                    ? this.Resolve(reference, RedefinedRole)
                    : throw new InvalidChangeException($"The {Describe(owner)} inherits no feature named '{reference}'.{this.DescribeInheritedFeatures(owner)}");

                CheckKind(redefined, redefined is IFeature, RedefinedRole, "a feature, for example an attribute or a part");
            }

            if (this.GetOwner(redefined) == owner)
            {
                throw new InvalidChangeException($"The {this.DescribeFeature(redefined)} belongs to the {Describe(owner)} itself: update it instead of redefining it.");
            }

            if (!inheritedFeatures.Contains(redefined))
            {
                redefined = this.FindRedefiningFeature(owner, redefined);
            }

            if (!HasMetaclassOf(member, redefined))
            {
                throw new InvalidChangeException($"A {member.GetType().Name} cannot redefine the {this.DescribeFeature(redefined)}: a feature redefines a feature of the same metaclass or of a more general one, "
                    + $"for example a PartUsage can redefine an ItemUsage. Create it with the type {redefined.GetType().Name}.");
            }

            return redefined;
        }

        /// <summary>
        /// Gets the feature through which an owner inherits a feature that is redefined on the way, for example the
        /// <c>:&gt;&gt; mass</c> of <c>Camera</c> when <c>camera : Camera</c> is asked to redefine the <c>mass</c> of
        /// <c>Instrument</c>, which <c>Camera</c> specializes.
        /// </summary>
        /// <param name="owner">The owner of the created element.</param>
        /// <param name="redefined">The feature to redefine, which the owner does not inherit as it is.</param>
        /// <returns>The inherited feature that redefines it, directly or not.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the owner does not inherit the feature, or when one of its own features already redefines it.
        /// </exception>
        private IElement FindRedefiningFeature(IElement owner, IElement redefined)
        {
            var redefining = this.GetFeatures(owner).FirstOrDefault(feature => this.GetAllRedefinedFeatures(feature).Contains(redefined))
                ?? throw new InvalidChangeException($"The {Describe(owner)} does not inherit the {this.DescribeFeature(redefined)}: a usage redefines a feature of its definition or of what it specializes."
                    + this.DescribeInheritedFeatures(owner));

            return this.GetOwner(redefining) == owner
                ? throw new InvalidChangeException($"The {Describe(owner)} already redefines the {this.DescribeFeature(redefined)}, with the element whose identifier is {redefining.Id}: update it instead.")
                : redefining;
        }

        /// <summary>
        /// Checks that a feature can be redefined with a value: its value, and the ones of the features that it redefines,
        /// directly or not, are <c>default</c> values or missing.
        /// </summary>
        /// <param name="redefined">The redefined feature.</param>
        /// <exception cref="InvalidChangeException">Thrown when one of these values is fixed.</exception>
        private void CheckValueCanBeRedefined(IElement redefined)
        {
            var fixedFeature = this.GetAllRedefinedFeatures(redefined).FirstOrDefault(feature => this.GetValueKind(feature) == false);

            if (fixedFeature != null)
            {
                throw new InvalidChangeException($"The value of the {this.DescribeFeature(fixedFeature)} is fixed, and only a default value can be redefined. If each usage may have its own value, "
                    + $"make it a default value first, in this batch: a change before this one with the identity {fixedFeature.Id} and the payload {{\"isDefault\": true}}.");
            }
        }

        /// <summary>
        /// Checks an update of the kind of a value, before any modification: the element has a value, given by the update or
        /// already there, and no feature redefines it with a value of its own when it becomes fixed.
        /// </summary>
        /// <param name="element">The updated element.</param>
        /// <param name="payload">The properties to change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the kind of the value cannot change.</exception>
        private void CheckValueKindUpdate(IElement element, ElementPayload payload)
        {
            if (payload.IsDefault == null)
            {
                return;
            }

            CheckCanHaveValue(element);

            if (payload.Value == null && this.GetValueKind(element) == null)
            {
                throw new InvalidChangeException($"The {Describe(element)} has no value: give its value with isDefault.");
            }

            if (payload.IsDefault == true)
            {
                return;
            }

            var redefiningFeatures = this.elementsById.Values
                .OfType<IRedefinition>()
                .Where(redefinition => redefinition.RedefinedFeature == element.Id && redefinition.OwningRelatedElement != null)
                .Select(redefinition => this.elementsById.GetValueOrDefault(redefinition.OwningRelatedElement.Value))
                .Where(feature => feature != null && this.GetValueKind(feature) != null && !this.IsMarkedForDeletion(feature))
                .ToList();

            if (redefiningFeatures.Count > 0)
            {
                var others = redefiningFeatures.Count > 1 ? $", as {redefiningFeatures.Count - 1} other features do" : string.Empty;

                throw new InvalidChangeException($"The value of the {Describe(element)} cannot become fixed: only a default value can be redefined, "
                    + $"and the {this.DescribeFeature(redefiningFeatures[0])} redefines it with a value of its own{others}.");
            }
        }

        /// <summary>
        /// Tells whether the value of a feature is a <c>default</c> value.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <returns><c>true</c> for a default value, <c>false</c> for a fixed one, <c>null</c> when the feature has no value.</returns>
        private bool? GetValueKind(IElement feature)
        {
            return this.GetElements(feature.OwnedRelationship).OfType<IFeatureValue>().Select(featureValue => (bool?)featureValue.IsDefault).FirstOrDefault();
        }

        /// <summary>
        /// Makes the value of a feature a <c>default</c> value or a fixed one.
        /// </summary>
        /// <param name="feature">The feature, which has a value.</param>
        /// <param name="isDefault">Whether the value becomes a default value.</param>
        private void SetValueKind(IElement feature, bool isDefault)
        {
            foreach (var featureValue in this.GetElements(feature.OwnedRelationship).OfType<IFeatureValue>())
            {
                featureValue.IsDefault = isDefault;
                this.MarkModified(featureValue);
            }
        }

        /// <summary>
        /// Gets the features of a type, as SysML2.NET computes them (<c>feature</c>): the features that it owns, then the ones
        /// that it inherits, except the features that another feature redefines (<c>:&gt;&gt; mass</c> replaces the inherited
        /// <c>mass</c>).
        /// </summary>
        /// <param name="type">The type, for example a part or a part definition.</param>
        /// <returns>The features of the type, its own first, or none when the element is not a type.</returns>
        private List<IFeature> GetFeatures(IElement type)
        {
            return this.GetAssembledElement(type.Id) is PocoType assembledType ? this.GetFeatureElements(assembledType.feature) : [];
        }

        /// <summary>
        /// Gets the features that a type inherits and does not redefine, as SysML2.NET computes them (<c>inheritedFeature</c>).
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The inherited features, or none when the element is not a type.</returns>
        private List<IFeature> GetInheritedFeatures(IElement type)
        {
            return this.GetAssembledElement(type.Id) is PocoType assembledType ? this.GetFeatureElements(assembledType.inheritedFeature) : [];
        }

        /// <summary>
        /// Gets a feature and the features that it redefines, directly or not, as SysML2.NET computes them
        /// (<c>allRedefinedFeatures()</c>).
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <returns>The feature, then the features that it redefines, or none when the element is not a feature.</returns>
        private List<IFeature> GetAllRedefinedFeatures(IElement feature)
        {
            return this.GetAssembledElement(feature.Id) is PocoFeature assembledFeature ? this.GetFeatureElements(assembledFeature.AllRedefinedFeatures()) : [];
        }

        /// <summary>
        /// Gets the features of the working copy that the given POCOs stand for.
        /// </summary>
        /// <param name="assembledFeatures">The POCOs of the features.</param>
        /// <returns>The features, in the same order.</returns>
        private List<IFeature> GetFeatureElements(IEnumerable<PocoFeature> assembledFeatures)
        {
            return [.. this.GetElements(assembledFeatures.Select(feature => feature.Id)).OfType<IFeature>()];
        }

        /// <summary>
        /// Gets the name of an element as SysML2.NET computes it (<c>name</c>, the <c>effectiveName</c>): its declared name or,
        /// for a feature that declares none, the name of the feature that it redefines (<c>:&gt;&gt; mass</c> is named
        /// <c>mass</c>).
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The name, or <c>null</c> when the element has none.</returns>
        private string GetEffectiveName(IElement element)
        {
            return this.GetAssembledElement(element.Id)?.name ?? element.DeclaredName;
        }

        /// <summary>
        /// Tells whether an element inherits features that its members can redefine: a definition or a usage, but not a
        /// catalogue of variants or an enumeration definition, whose members are variants.
        /// </summary>
        /// <param name="owner">The element.</param>
        /// <returns><c>true</c> when the members of the element can redefine its inherited features.</returns>
        private static bool CanInherit(IElement owner)
        {
            return owner is IType and not (IPartDefinition { IsVariation: true } or IEnumerationDefinition);
        }

        /// <summary>
        /// Describes a feature in a message, by its metaclass, the name of its owner and its name, for example
        /// <c>AttributeUsage 'OpticalCamera::mass'</c>.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <returns>The description of the feature.</returns>
        private string DescribeFeature(IElement feature)
        {
            var ownerName = this.GetOwner(feature) is { } owner ? this.GetEffectiveName(owner) : null;
            var name = this.GetEffectiveName(feature) ?? feature.Id.ToString();

            return $"{feature.GetType().Name} '{(ownerName == null ? name : $"{ownerName}::{name}")}'";
        }

        /// <summary>
        /// Describes, for a message, the names of the first features that an element inherits, so that the AI assistant
        /// can give one of them to redefine.
        /// </summary>
        /// <param name="owner">The element.</param>
        /// <returns>The sentence that names the inherited features.</returns>
        private string DescribeInheritedFeatures(IElement owner)
        {
            var names = this.GetInheritedFeatures(owner)
                .Select(this.GetEffectiveName)
                .Where(name => name != null)
                .Distinct()
                .Take(InheritedFeatureCount)
                .Select(name => $"'{name}'")
                .ToList();

            return names.Count == 0
                ? " It inherits no feature: give it a definition first."
                : $" It inherits {string.Join(", ", names)}.";
        }
    }
}
