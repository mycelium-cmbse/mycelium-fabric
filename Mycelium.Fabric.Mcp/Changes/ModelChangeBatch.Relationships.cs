// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Relationships.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Core.Classifiers;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Actions;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Occurrences;
    using SysML2.NET.Core.DTO.Systems.Ports;

    /// <content>
    /// The relationships between elements that already exist: the specialization of a definition or a usage
    /// (<c>part def Camera :> Equipment</c>, <c>part camera :> sensor</c>) and the action that an element performs
    /// (<c>perform providePower;</c>). The successions and allocations are connectors, built with the ends of
    /// <c>ModelChangeBatch.Connections.cs</c>.
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The role, in the messages, of the element that a created or updated element specializes.
        /// </summary>
        private const string GeneralRole = "specialized element";

        /// <summary>
        /// The role, in the messages, of the action that a <c>PerformActionUsage</c> performs.
        /// </summary>
        private const string PerformedRole = "performed action";

        /// <summary>
        /// Gets the element that a definition or a usage specializes, designated by an identifier or a temporary name, and
        /// checks that it can be specialized by the element: it is a different element, of the same metaclass or of a more
        /// general one (a part definition specializes an item definition, a part specializes an item).
        /// </summary>
        /// <param name="element">The specializing element, which may not have its identifier yet.</param>
        /// <param name="reference">The <c>Id</c> or temporary name of the specialized element.</param>
        /// <returns>The specialized element.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the element is not a definition or a usage, when the reference designates no element (see
        /// <see cref="Resolve"/>), or when the specialized element is not of a compatible kind.
        /// </exception>
        private IElement ResolveGeneral(IElement element, string reference)
        {
            if (element is not (IDefinition or IUsage) || element is IConjugatedPortDefinition)
            {
                throw new InvalidChangeException($"Only a definition or a usage specializes another element, not the {Describe(element)}.");
            }

            var general = this.Resolve(reference, GeneralRole);

            if (general.Id == element.Id)
            {
                throw new InvalidChangeException($"The {Describe(element)} cannot specialize itself.");
            }

            var generalInterface = "I" + general.GetType().Name;

            if (general is IConjugatedPortDefinition || element.GetType().GetInterfaces().All(type => type.Name != generalInterface))
            {
                throw new InvalidChangeException(
                    $"The {Describe(element)} cannot specialize the {Describe(general)}: a definition or a usage specializes an element of the same metaclass or of a more general one, for example a PartDefinition can specialize an ItemDefinition.");
            }

            return general;
        }

        /// <summary>
        /// Checks that an existing element can specialize an element, before any modification: it must not already
        /// specialize it, directly or not, and the element must not specialize it.
        /// </summary>
        /// <param name="element">The updated element.</param>
        /// <param name="general">The specialized element, already checked by <see cref="ResolveGeneral"/>.</param>
        /// <exception cref="InvalidChangeException">Thrown when the specialization would be a cycle.</exception>
        private void CheckNoSpecializationCycle(IElement element, IElement general)
        {
            if (this.GetGenerals(general).Contains(element.Id))
            {
                throw new InvalidChangeException($"The {Describe(general)} already specializes the {Describe(element)}: the specialization would be a cycle.");
            }
        }

        /// <summary>
        /// Gets the identifiers of the elements that an element specializes, directly or not.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The identifiers of its generals.</returns>
        private HashSet<Guid> GetGenerals(IElement element)
        {
            var generals = new HashSet<Guid>();
            var pending = new Stack<IElement>([element]);

            while (pending.Count > 0)
            {
                foreach (var generalId in this.GetElements(pending.Pop().OwnedRelationship).Select(GetGeneralId).OfType<Guid>())
                {
                    if (generals.Add(generalId) && this.elementsById.TryGetValue(generalId, out var general))
                    {
                        pending.Push(general);
                    }
                }
            }

            return generals;
        }

        /// <summary>
        /// Gets the identifier of the element that a relationship specializes.
        /// </summary>
        /// <param name="relationship">The owned relationship of the specializing element.</param>
        /// <returns>The identifier of the general, or <c>null</c> when the relationship is not a specialization.</returns>
        private static Guid? GetGeneralId(IElement relationship)
        {
            return relationship switch
            {
                ISubclassification subclassification => subclassification.Superclassifier,
                ISubsetting subsetting => subsetting.SubsettedFeature,
                _ => null
            };
        }

        /// <summary>
        /// Tells whether a relationship is the specialization that <c>Specializes</c> sets: a <c>Subclassification</c> of a
        /// definition or a <c>Subsetting</c> of a usage, but not a reference subsetting, a redefinition or a cross
        /// subsetting, which other changes build.
        /// </summary>
        /// <param name="relationship">The owned relationship of an element.</param>
        /// <returns><c>true</c> when the relationship is a plain specialization.</returns>
        private static bool IsPlainSpecialization(IElement relationship)
        {
            return relationship is ISubclassification || relationship is ISubsetting and not (IReferenceSubsetting or IRedefinition or ICrossSubsetting);
        }

        /// <summary>
        /// Makes an element specialize another one, as <c>:></c> does: a <c>Subclassification</c> for a definition, a
        /// <c>Subsetting</c> for a usage. The current specialization of the element is replaced.
        /// </summary>
        /// <param name="element">The specializing element, already added to the working copy.</param>
        /// <param name="general">The specialized element, already checked by <see cref="ResolveGeneral"/>.</param>
        private void SetSpecialization(IElement element, IElement general)
        {
            foreach (var specialization in this.GetElements(element.OwnedRelationship).Where(IsPlainSpecialization).ToList())
            {
                this.RemoveTree(specialization);
            }

            IRelationship relationship = element is IDefinition
                ? new Subclassification { Subclassifier = element.Id, Superclassifier = general.Id }
                : new Subsetting { SubsettingFeature = element.Id, SubsettedFeature = general.Id };

            this.AddOwnedRelationship(element, relationship);
        }

        /// <summary>
        /// Creates an action performed by an element, as <c>perform providePower;</c> does: a <c>PerformActionUsage</c> owned
        /// by the performer, whose <c>ReferenceSubsetting</c> designates the performed action.
        /// </summary>
        /// <param name="perform">The new <c>PerformActionUsage</c>, which has no identifier yet.</param>
        /// <param name="change">The change that creates it, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreatePerform(IElement perform, ModelChange change)
        {
            var payload = change.Payload;

            if (payload is not { Definition: null, Value: null, Unit: null, Constraint: null, SatisfiedRequirement: null, SatisfyingPart: null, Ends: null, Direction: null, Conjugated: null, IsEnd: null, Specializes: null }
                || !string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException("A PerformActionUsage takes an owner and a performed action, and optionally a name and a text.");
            }

            var owner = this.Resolve(payload.Owner, OwnerRole);
            CheckKind(owner, owner is IOccurrenceDefinition or IOccurrenceUsage, OwnerRole, "an occurrence that performs the action, for example a part, a part definition or an action");

            var action = this.Resolve(payload.Performed, PerformedRole);
            CheckKind(action, action is IActionUsage, PerformedRole, "an action usage");

            if (this.GetElements(owner.OwnedRelationship).Any(relationship => this.IsPerformedBy(relationship, action)))
            {
                throw new InvalidChangeException($"The {Describe(owner)} already performs the {Describe(action)}.");
            }

            if (!string.IsNullOrWhiteSpace(payload.Name))
            {
                CheckName(payload.Name);
                this.CheckNameIsFree(owner, payload.Name);
                perform.DeclaredName = payload.Name;
            }

            this.AddOwnedMember(owner, perform, change.Identity);

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(perform, payload.Text);
            }

            this.AddOwnedRelationship(perform, new ReferenceSubsetting { ReferencedFeature = action.Id });
        }

        /// <summary>
        /// Tells whether a membership of a performer owns a <c>PerformActionUsage</c> of an action.
        /// </summary>
        /// <param name="membership">An owned relationship of the performer.</param>
        /// <param name="action">The action.</param>
        /// <returns><c>true</c> when the membership owns a <c>PerformActionUsage</c> that performs the action.</returns>
        private bool IsPerformedBy(IElement membership, IElement action)
        {
            return this.GetElements(((IRelationship)membership).OwnedRelatedElement)
                .OfType<IPerformActionUsage>()
                .Any(perform => !this.IsMarkedForDeletion(perform)
                    && this.GetElements(perform.OwnedRelationship).OfType<IReferenceSubsetting>().Any(subsetting => subsetting.ReferencedFeature == action.Id));
        }
    }
}
