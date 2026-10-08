// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Occurrences;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Dal;
    using SysML2.NET.PIM;
    using SysML2.NET.PSM.DTO;

    using PocoRelationship = SysML2.NET.Core.POCO.Root.Elements.IRelationship;

    /// <summary>
    /// A batch of <see cref="ModelChange"/>s applied, in order, to a working copy of a SysML v2 model made of DTOs. The batch
    /// records the elements it creates, modifies and removes, so that it can describe its result as the change of a commit.
    /// </summary>
    /// <remarks>
    /// The changes build the elements and relationships of the SysML v2 metamodel, as the textual notation does: an owning
    /// membership for each owned element, a <c>FeatureTyping</c> for <c>camera : Camera</c>, a <c>FeatureValue</c> that owns
    /// a <c>LiteralRational</c> for <c>mass = 38</c>, and an owned <c>Documentation</c> for a text. Deletions are checked and
    /// carried out at the end of the batch, once the references to the deleted elements are known. A batch is applied once.
    /// </remarks>
    internal sealed class ModelChangeBatch
    {
        /// <summary>
        /// The role, in the messages, of the element that owns a created element.
        /// </summary>
        private const string OwnerRole = "owner";

        /// <summary>
        /// The role, in the messages, of the element that a change updates or deletes.
        /// </summary>
        private const string ElementRole = "element";

        /// <summary>
        /// The role, in the messages, of the definition that types a feature.
        /// </summary>
        private const string DefinitionRole = "definition";

        /// <summary>
        /// The metaclasses that a change can create, indexed by their name: the concrete DTO classes of SysML2.NET that are
        /// packages, definitions or usages, but not relationships, whose related elements only the server can build.
        /// </summary>
        private static readonly Dictionary<string, System.Type> CreatableMetaclasses = typeof(IElement).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && IsCreatable(type))
            .ToDictionary(type => type.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The elements of the working copy, indexed by their <c>Id</c>.
        /// </summary>
        private readonly Dictionary<Guid, IElement> elementsById;

        /// <summary>
        /// The <c>Id</c> of the elements of the model before the batch, in their original order.
        /// </summary>
        private readonly List<Guid> originalIds;

        /// <summary>
        /// The <c>Id</c> of the elements added by the batch, in the order of their creation.
        /// </summary>
        private readonly List<Guid> addedIds = [];

        /// <summary>
        /// The <c>Id</c> of the elements whose DTO the batch has modified.
        /// </summary>
        private readonly HashSet<Guid> modifiedIds = [];

        /// <summary>
        /// The <c>Id</c> of the elements created by the batch, indexed by their temporary name.
        /// </summary>
        private readonly Dictionary<string, Guid> temporaryNames = [];

        /// <summary>
        /// The temporary names of the elements whose creation failed, to explain why later changes cannot use them.
        /// </summary>
        private readonly HashSet<string> failedTemporaryNames = [];

        /// <summary>
        /// The elements to delete at the end of the batch, with the number of the change that deletes them.
        /// </summary>
        private readonly List<(int ChangeNumber, IElement Element)> deletions = [];

        /// <summary>
        /// The elements created by the batch.
        /// </summary>
        private readonly List<CreatedElement> createdElements = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelChangeBatch"/> class.
        /// </summary>
        /// <param name="workingCopy">The DTOs of the model to modify, which must not be shared with the current model.</param>
        public ModelChangeBatch(IEnumerable<IElement> workingCopy)
        {
            this.elementsById = workingCopy.ToDictionary(element => element.Id);
            this.originalIds = [.. this.elementsById.Keys];
        }

        /// <summary>
        /// Gets the elements created by the batch that are still in the working copy.
        /// </summary>
        public IReadOnlyList<CreatedElement> CreatedElements => [.. this.createdElements.Where(created => this.elementsById.ContainsKey(created.Id))];

        /// <summary>
        /// Applies the given changes, in order, to the working copy. Every change is checked, so that all the problems of the
        /// batch are reported at once.
        /// </summary>
        /// <param name="changes">The changes to apply.</param>
        /// <returns>
        /// The problems that prevent the batch from being applied, empty when it is applied. When there are problems, the
        /// working copy is left half-modified and must be discarded.
        /// </returns>
        public IReadOnlyList<string> Apply(IReadOnlyList<ModelChange> changes)
        {
            var problems = new List<string>();

            for (var changeIndex = 0; changeIndex < changes.Count; changeIndex++)
            {
                var problem = this.TryApply(changes[changeIndex], changeIndex + 1);

                if (problem != null)
                {
                    problems.Add(problem);
                }
            }

            if (problems.Count == 0)
            {
                problems.AddRange(this.RemoveDeletedElements());
            }

            return problems;
        }

        /// <summary>
        /// Describes the result of the batch as a <see cref="CommitRequest"/>, the body of <c>POST /projects/{projectId}/commits</c>
        /// (Systems Modeling API and Services 1.0 §7.2.3 and §8.1.3): a <see cref="DataVersionRequest"/> with a payload for
        /// each created or updated element, and one without payload for each deleted element. An element created and deleted
        /// by the same batch does not appear.
        /// </summary>
        /// <returns>
        /// The <see cref="CommitRequest"/>, whose change holds the creations, then the updates, then the deletions.
        /// </returns>
        public CommitRequest CreateCommitRequest()
        {
            var creations = this.addedIds
                .Where(this.elementsById.ContainsKey)
                .Select(elementId => CreateDataVersionRequest(elementId, this.elementsById[elementId]));

            var updates = this.originalIds
                .Where(elementId => this.modifiedIds.Contains(elementId) && this.elementsById.ContainsKey(elementId))
                .Select(elementId => CreateDataVersionRequest(elementId, this.elementsById[elementId]));

            var removals = this.originalIds
                .Where(elementId => !this.elementsById.ContainsKey(elementId))
                .Select(elementId => CreateDataVersionRequest(elementId, null));

            return new CommitRequest { Change = [.. creations, .. updates, .. removals] };
        }

        /// <summary>
        /// Tells whether a change creates, updates or deletes an element, as the <c>DataVersion</c> records of a commit do: a
        /// payload with a type creates an element, a payload without type updates it, and no payload deletes it.
        /// </summary>
        /// <param name="change">The change.</param>
        /// <returns>The <see cref="ChangeType"/> of the change.</returns>
        private static ChangeType GetChangeType(ModelChange change)
        {
            return change.Payload switch
            {
                null => ChangeType.DELETED,
                { Type: not null } => ChangeType.CREATED,
                _ => ChangeType.UPDATED
            };
        }

        /// <summary>
        /// Describes a <see cref="ChangeType"/> in a message.
        /// </summary>
        /// <param name="changeType">The <see cref="ChangeType"/>.</param>
        /// <returns>The verb of the change, for example <c>create</c>.</returns>
        private static string DescribeChangeType(ChangeType changeType)
        {
            return changeType switch
            {
                ChangeType.CREATED => "create",
                ChangeType.UPDATED => "update",
                _ => "delete"
            };
        }

        /// <summary>
        /// Tells whether a change can create an element of the given DTO class: a package, a definition or a usage that is
        /// not a relationship.
        /// </summary>
        /// <param name="type">The DTO class.</param>
        /// <returns><c>true</c> when a change can create an element of this class.</returns>
        private static bool IsCreatable(System.Type type)
        {
            return (typeof(IPackage).IsAssignableFrom(type) || typeof(IDefinition).IsAssignableFrom(type) || typeof(IUsage).IsAssignableFrom(type))
                && !typeof(IRelationship).IsAssignableFrom(type);
        }

        /// <summary>
        /// Creates a <see cref="DataVersionRequest"/> of an element.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element, which is the <c>Id</c> of its <see cref="DataIdentityRequest"/>.</param>
        /// <param name="payload">The DTO of the element, or <c>null</c> when the element is deleted.</param>
        /// <returns>The <see cref="DataVersionRequest"/>.</returns>
        private static DataVersionRequest CreateDataVersionRequest(Guid elementId, IElement payload)
        {
            return new DataVersionRequest { Identity = new DataIdentityRequest { Id = elementId }, Payload = payload };
        }

        /// <summary>
        /// Applies one change to the working copy.
        /// </summary>
        /// <param name="change">The change to apply.</param>
        /// <param name="changeNumber">The position of the change in the batch, starting at 1.</param>
        /// <returns>The problem that prevents the change from being applied, or <c>null</c> when it is applied.</returns>
        private string TryApply(ModelChange change, int changeNumber)
        {
            if (change == null)
            {
                return $"Change {changeNumber}: the change is empty.";
            }

            var changeType = GetChangeType(change);

            try
            {
                switch (changeType)
                {
                    case ChangeType.CREATED:
                        this.Create(change);
                        break;
                    case ChangeType.UPDATED:
                        this.Update(change);
                        break;
                    default:
                        this.MarkForDeletion(change, changeNumber);
                        break;
                }

                return null;
            }
            catch (InvalidChangeException exception)
            {
                if (changeType == ChangeType.CREATED && !string.IsNullOrWhiteSpace(change.Identity) && !this.temporaryNames.ContainsKey(change.Identity))
                {
                    this.failedTemporaryNames.Add(change.Identity);
                }

                return $"Change {changeNumber} ({DescribeChangeType(changeType)}): {exception.Message}";
            }
        }

        /// <summary>
        /// Creates an element of the metaclass given by the payload, as a member of its owner, or of the root namespace of the
        /// model when the payload gives no owner, with its documentation, definition and value.
        /// </summary>
        /// <param name="change">The change that creates the element, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void Create(ModelChange change)
        {
            var payload = change.Payload;

            this.CheckTemporaryName(change.Identity);
            CheckName(payload.Name);

            var member = CreateElement(payload.Type);
            member.DeclaredName = payload.Name;

            var owner = string.IsNullOrWhiteSpace(payload.Owner) ? this.GetOrCreateRootNamespace() : this.Resolve(payload.Owner, OwnerRole);
            CheckKind(owner, owner is INamespace, OwnerRole, "a namespace, for example a package, a definition or a usage");
            this.CheckNameIsFree(owner, payload.Name);

            if (member is IRequirementUsage && string.IsNullOrWhiteSpace(payload.Text))
            {
                throw new InvalidChangeException("The text of the requirement is missing.");
            }

            var definition = payload.Definition == null ? null : this.ResolveDefinition(member, payload.Definition);

            if (payload.Value != null)
            {
                CheckCanHaveValue(member);
            }

            this.AddOwnedMember(owner, member, change.Identity);

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(member, payload.Text);
            }

            if (definition != null)
            {
                this.AddTyping(member, definition);
            }

            if (payload.Value != null)
            {
                this.AddValue(member, payload.Value.Value);
            }
        }

        /// <summary>
        /// Updates the properties of an element given by the payload: its name, its definition, its value or its
        /// documentation. A new definition, value or documentation replaces the current one.
        /// </summary>
        /// <param name="change">The change that updates the element designated by its identity.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void Update(ModelChange change)
        {
            var payload = change.Payload;
            var element = this.Resolve(change.Identity, ElementRole);
            CheckKind(element, element is not IRelationship, ElementRole, "an element that is not a relationship");

            var definition = this.CheckUpdate(element, payload);

            if (payload.Name != null)
            {
                element.DeclaredName = payload.Name;
                this.MarkModified(element);
            }

            if (definition != null)
            {
                this.RemoveOwnedRelationships<IFeatureTyping>(element);
                this.AddTyping(element, definition);
            }

            if (payload.Value != null)
            {
                this.RemoveOwnedRelationships<IFeatureValue>(element);
                this.AddValue(element, payload.Value.Value);
            }

            if (payload.Text != null)
            {
                this.RemoveDocumentation(element);
                this.AddDocumentation(element, payload.Text);
            }
        }

        /// <summary>
        /// Checks the payload of an update before any modification, so that a refused update leaves the element unchanged.
        /// </summary>
        /// <param name="element">The updated element.</param>
        /// <param name="payload">The properties to change.</param>
        /// <returns>The new definition of the element, or <c>null</c> when the payload does not change it.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the payload cannot be applied to the element.</exception>
        private IElement CheckUpdate(IElement element, ElementPayload payload)
        {
            if (payload.Owner != null)
            {
                throw new InvalidChangeException("An update cannot change the owner of an element: delete the element and create it again in its new owner.");
            }

            if (payload is { Name: null, Definition: null, Value: null, Text: null })
            {
                throw new InvalidChangeException("The payload changes nothing: give the name, definition, value or text to change, or no payload to delete the element.");
            }

            if (payload.Name != null)
            {
                CheckName(payload.Name);
                this.CheckNameIsFree(this.GetOwner(element), payload.Name, element);
            }

            if (payload.Value != null)
            {
                CheckCanHaveValue(element);
            }

            if (payload.Text != null && string.IsNullOrWhiteSpace(payload.Text))
            {
                throw new InvalidChangeException("The text is empty.");
            }

            return payload.Definition == null ? null : this.ResolveDefinition(element, payload.Definition);
        }

        /// <summary>
        /// Marks an element for deletion. It is deleted at the end of the batch, by <see cref="RemoveDeletedElements"/>.
        /// </summary>
        /// <param name="change">The change that deletes the element designated by its identity.</param>
        /// <param name="changeNumber">The position of the change in the batch, starting at 1.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void MarkForDeletion(ModelChange change, int changeNumber)
        {
            var element = this.Resolve(change.Identity, ElementRole);
            CheckKind(element, element is not IRelationship, ElementRole, "an element that is not a relationship");

            this.deletions.Add((changeNumber, element));
        }

        /// <summary>
        /// Deletes the elements marked for deletion, with their owning membership and everything they own, unless an element
        /// that stays in the model still references one of them (for example a part typed by a deleted part definition).
        /// </summary>
        /// <returns>The problems that prevent the deletions, empty when they are carried out.</returns>
        private List<string> RemoveDeletedElements()
        {
            if (this.deletions.Count == 0)
            {
                return [];
            }

            var removedIdsByChange = this.deletions.ToDictionary(deletion => deletion.ChangeNumber, deletion => this.GetRemovedIds(deletion.Element));
            var allRemovedIds = removedIdsByChange.Values.SelectMany(removedIds => removedIds).ToHashSet();

            var remainingRelationships = this.AssembleRelationships()
                .Where(relationship => !allRemovedIds.Contains(relationship.Id))
                .ToList();

            var problems = this.deletions
                .Select(deletion => DescribeReferences(deletion.ChangeNumber, deletion.Element, removedIdsByChange[deletion.ChangeNumber], remainingRelationships))
                .Where(problem => problem != null)
                .ToList();

            if (problems.Count == 0)
            {
                foreach (var deletion in this.deletions)
                {
                    this.RemoveTree(this.GetRemovalRoot(deletion.Element));
                }
            }

            return problems;
        }

        /// <summary>
        /// Builds the POCOs of the working copy with SysML2.NET and gets its relationships. Unlike a DTO, the POCO of a
        /// relationship gives the elements it relates, whatever its kind, through its derived <c>relatedElement</c>.
        /// </summary>
        /// <returns>The relationships of the working copy.</returns>
        private List<PocoRelationship> AssembleRelationships()
        {
            var assembler = new Assembler();
            assembler.Synchronize(this.elementsById.Values);

            return assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .OfType<PocoRelationship>()
                .ToList();
        }

        /// <summary>
        /// Describes the references that prevent a deletion.
        /// </summary>
        /// <param name="changeNumber">The position of the deleting change in the batch.</param>
        /// <param name="deletedElement">The deleted element.</param>
        /// <param name="removedIds">The <c>Id</c> of the elements that the deletion removes.</param>
        /// <param name="remainingRelationships">The relationships that stay in the model.</param>
        /// <returns>The problem that prevents the deletion, or <c>null</c> when nothing references the removed elements.</returns>
        private static string DescribeReferences(int changeNumber, IElement deletedElement, HashSet<Guid> removedIds, IEnumerable<PocoRelationship> remainingRelationships)
        {
            var referencingElements = remainingRelationships
                .Where(relationship => relationship.relatedElement.Any(relatedElement => relatedElement != null && removedIds.Contains(relatedElement.Id)))
                .Select(relationship => $"'{relationship.OwningRelatedElement?.qualifiedName ?? relationship.Id.ToString()}'")
                .Distinct()
                .ToList();

            return referencingElements.Count == 0
                ? null
                : $"Change {changeNumber} (delete): the {Describe(deletedElement)} is still referenced by {string.Join(", ", referencingElements)}. Change or delete these elements first.";
        }

        /// <summary>
        /// Checks the temporary name of an element to create.
        /// </summary>
        /// <param name="temporaryName">The temporary name, or <c>null</c> when the creation has none.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the temporary name is an identifier or is already used.
        /// </exception>
        private void CheckTemporaryName(string temporaryName)
        {
            if (string.IsNullOrWhiteSpace(temporaryName))
            {
                return;
            }

            if (Guid.TryParse(temporaryName, out _))
            {
                throw new InvalidChangeException($"The identity '{temporaryName}' of a new element must be a temporary name, not an identifier: the server gives the identifier.");
            }

            if (this.temporaryNames.ContainsKey(temporaryName))
            {
                throw new InvalidChangeException($"The temporary name '{temporaryName}' is already used by a previous change.");
            }
        }

        /// <summary>
        /// Checks that no other member of the owner, except the elements marked for deletion, has the given name.
        /// </summary>
        /// <param name="owner">The owner of the named element, or <c>null</c> for a root element.</param>
        /// <param name="name">The name of the element.</param>
        /// <param name="renamedElement">The renamed element, which may keep its own name, or <c>null</c> for a new element.</param>
        /// <exception cref="InvalidChangeException">Thrown when another member of the owner has the name.</exception>
        private void CheckNameIsFree(IElement owner, string name, IElement renamedElement = null)
        {
            if (owner == null)
            {
                return;
            }

            var existingMember = this.GetOwnedMembers(owner)
                .FirstOrDefault(member => member != renamedElement && member.DeclaredName == name && !this.IsMarkedForDeletion(member));

            if (existingMember != null)
            {
                throw new InvalidChangeException($"The {Describe(owner)} already has a member named '{name}', whose identifier is {existingMember.Id}.");
            }
        }

        /// <summary>
        /// Gets the element designated by an identifier or by the temporary name of an element created by a previous change.
        /// </summary>
        /// <param name="reference">The <c>Id</c> or temporary name of the element.</param>
        /// <param name="role">The role of the element in the change, for example <c>owner</c>, used in the messages.</param>
        /// <returns>The designated element.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the reference is missing, designates no element, or designates an element marked for deletion.
        /// </exception>
        private IElement Resolve(string reference, string role)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new InvalidChangeException($"The {role} is missing.");
            }

            if (!Guid.TryParse(reference, out var elementId) && !this.temporaryNames.TryGetValue(reference, out elementId))
            {
                throw new InvalidChangeException(this.failedTemporaryNames.Contains(reference)
                    ? $"The {role} '{reference}' has not been created, because the change that creates it has failed."
                    : $"The {role} '{reference}' is neither the identifier of an element nor the temporary name of an element created by a previous change.");
            }

            if (!this.elementsById.TryGetValue(elementId, out var element))
            {
                throw new InvalidChangeException($"No element has the identifier '{reference}'. Use find_elements_by_name or list_children to get a valid identifier.");
            }

            if (this.IsMarkedForDeletion(element))
            {
                throw new InvalidChangeException($"The {role} '{reference}' is deleted by a previous change.");
            }

            return element;
        }

        /// <summary>
        /// Gets the definition of a feature, designated by an identifier or a temporary name.
        /// </summary>
        /// <param name="feature">The element to type, which must be a feature.</param>
        /// <param name="reference">The <c>Id</c> or temporary name of the definition.</param>
        /// <returns>The designated definition.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the element is not a feature, or when the reference designates no element (see <see cref="Resolve"/>)
        /// or an element that is not a classifier.
        /// </exception>
        private IElement ResolveDefinition(IElement feature, string reference)
        {
            if (feature is not IFeature)
            {
                throw new InvalidChangeException($"Only a feature, for example a part or an attribute, has a definition, not the {Describe(feature)}.");
            }

            var definition = this.Resolve(reference, DefinitionRole);
            CheckKind(definition, definition is IClassifier, DefinitionRole, "a classifier, for example a part definition");

            return definition;
        }

        /// <summary>
        /// Gets the root namespace of the model, that owns its top-level elements, and creates it when the model has none.
        /// </summary>
        /// <returns>The root namespace of the model.</returns>
        private Namespace GetOrCreateRootNamespace()
        {
            var rootNamespace = this.elementsById.Values
                .OfType<Namespace>()
                .FirstOrDefault(candidate => candidate.OwningRelationship == null && !this.IsMarkedForDeletion(candidate));

            return rootNamespace ?? this.Add(new Namespace());
        }

        /// <summary>
        /// Adds a new element to the working copy as a member of its owner, and records it as created.
        /// </summary>
        /// <param name="owner">The owner of the new element.</param>
        /// <param name="member">The new element, whose name is set.</param>
        /// <param name="temporaryName">The temporary name of the new element, or <c>null</c> when it has none.</param>
        private void AddOwnedMember(IElement owner, IElement member, string temporaryName)
        {
            // A feature (part, attribute...) of a type (part definition, part) is owned through a FeatureMembership; any other
            // member, for example an element of a package, through an OwningMembership.
            IMembership membership = member is IFeature && owner is IType
                ? new FeatureMembership { Visibility = VisibilityKind.Public }
                : new OwningMembership { Visibility = VisibilityKind.Public };

            this.AddOwnedRelationship(owner, membership, this.Add(member));

            if (!string.IsNullOrWhiteSpace(temporaryName))
            {
                this.temporaryNames.Add(temporaryName, member.Id);
            }

            this.createdElements.Add(new CreatedElement(temporaryName, member.Id, member.DeclaredName, member.GetType().Name));
        }

        /// <summary>
        /// Documents an element, as <c>doc /* ... */</c> does: an owned <c>Documentation</c> whose body is the text.
        /// </summary>
        /// <param name="element">The documented element.</param>
        /// <param name="text">The text of the documentation.</param>
        private void AddDocumentation(IElement element, string text)
        {
            this.AddOwnedRelationship(element, new OwningMembership { Visibility = VisibilityKind.Public }, this.Add(new Documentation { Body = text }));
        }

        /// <summary>
        /// Binds an attribute to a numeric value, as <c>mass = 38</c> does: a <c>FeatureValue</c> that owns a
        /// <c>LiteralRational</c>.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <param name="value">The value of the attribute.</param>
        private void AddValue(IElement attribute, double value)
        {
            this.AddOwnedRelationship(attribute, new FeatureValue { Visibility = VisibilityKind.Public }, this.Add(new LiteralRational { Value = value }));
        }

        /// <summary>
        /// Types a feature by a definition, as <c>camera : Camera</c> does: a <c>FeatureTyping</c> owned by the feature.
        /// </summary>
        /// <param name="feature">The typed feature.</param>
        /// <param name="definition">The definition.</param>
        private void AddTyping(IElement feature, IElement definition)
        {
            this.AddOwnedRelationship(feature, new FeatureTyping { TypedFeature = feature.Id, Type = definition.Id });
        }

        /// <summary>
        /// Adds a relationship to the working copy, owned by an element and owning another element when one is given. Both
        /// sides of each link are set, since a DTO only holds identifiers, and the modified elements are recorded.
        /// </summary>
        /// <param name="owner">The element that owns the relationship.</param>
        /// <param name="relationship">The new relationship.</param>
        /// <param name="ownedRelatedElement">The element owned by the relationship, or <c>null</c> when it owns none.</param>
        private void AddOwnedRelationship(IElement owner, IRelationship relationship, IElement ownedRelatedElement = null)
        {
            this.Add(relationship);

            relationship.OwningRelatedElement = owner.Id;
            owner.OwnedRelationship.Add(relationship.Id);
            this.MarkModified(owner);

            if (ownedRelatedElement != null)
            {
                relationship.OwnedRelatedElement.Add(ownedRelatedElement.Id);
                ownedRelatedElement.OwningRelationship = relationship.Id;
                this.MarkModified(ownedRelatedElement);
            }
        }

        /// <summary>
        /// Gives a new identifier to an element and adds it to the working copy.
        /// </summary>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <param name="element">The new element.</param>
        /// <returns>The new element.</returns>
        private T Add<T>(T element) where T : class, IElement
        {
            element.Id = Guid.NewGuid();
            element.ElementId = element.Id.ToString();
            this.elementsById.Add(element.Id, element);
            this.addedIds.Add(element.Id);

            return element;
        }

        /// <summary>
        /// Records that the DTO of an element has been modified, so that the change of the commit holds its new version.
        /// </summary>
        /// <param name="element">The modified element.</param>
        private void MarkModified(IElement element)
        {
            this.modifiedIds.Add(element.Id);
        }

        /// <summary>
        /// Removes the relationships of a given kind owned by an element, with everything they own.
        /// </summary>
        /// <typeparam name="T">The kind of relationship to remove.</typeparam>
        /// <param name="owner">The element that owns the relationships.</param>
        private void RemoveOwnedRelationships<T>(IElement owner) where T : IRelationship
        {
            var relationships = this.GetElements(owner.OwnedRelationship).OfType<T>().ToList();

            foreach (var relationship in relationships)
            {
                this.RemoveTree(relationship);
            }
        }

        /// <summary>
        /// Removes the documentation of an element: the memberships that own a <c>Documentation</c>, with their documentation.
        /// </summary>
        /// <param name="element">The documented element.</param>
        private void RemoveDocumentation(IElement element)
        {
            var documentationMemberships = this.GetElements(element.OwnedRelationship)
                .OfType<IRelationship>()
                .Where(relationship => this.GetElements(relationship.OwnedRelatedElement).OfType<IDocumentation>().Any())
                .ToList();

            foreach (var membership in documentationMemberships)
            {
                this.RemoveTree(membership);
            }
        }

        /// <summary>
        /// Removes an element and everything it owns from the working copy, and detaches it from its owner.
        /// </summary>
        /// <param name="root">The element to remove: a relationship owned by an element, or a root element.</param>
        private void RemoveTree(IElement root)
        {
            if (root is IRelationship { OwningRelatedElement: { } ownerId } && this.elementsById.TryGetValue(ownerId, out var owner))
            {
                owner.OwnedRelationship.Remove(root.Id);
                this.MarkModified(owner);
            }

            foreach (var element in this.CollectTree(root))
            {
                this.elementsById.Remove(element.Id);
            }
        }

        /// <summary>
        /// Gets the <c>Id</c> of the elements removed by the deletion of an element: its owning membership, the element and
        /// everything it owns.
        /// </summary>
        /// <param name="element">The deleted element.</param>
        /// <returns>The <c>Id</c> of the removed elements.</returns>
        private HashSet<Guid> GetRemovedIds(IElement element)
        {
            return this.CollectTree(this.GetRemovalRoot(element))
                .Select(removedElement => removedElement.Id)
                .ToHashSet();
        }

        /// <summary>
        /// Gets the element whose removal deletes the given element: its owning membership, which owns it, or the element
        /// itself when it is a root element.
        /// </summary>
        /// <param name="element">The deleted element.</param>
        /// <returns>The element to remove.</returns>
        private IElement GetRemovalRoot(IElement element)
        {
            return element.OwningRelationship is { } owningRelationshipId ? this.elementsById.GetValueOrDefault(owningRelationshipId) ?? element : element;
        }

        /// <summary>
        /// Collects an element and everything it owns, directly or not: its owned relationships and, for a relationship, its
        /// owned related elements.
        /// </summary>
        /// <param name="root">The element at the top of the tree.</param>
        /// <returns>The elements of the tree, starting with <paramref name="root"/>.</returns>
        private List<IElement> CollectTree(IElement root)
        {
            var ownedIds = root is IRelationship relationship ? root.OwnedRelationship.Concat(relationship.OwnedRelatedElement) : root.OwnedRelationship;

            return [root, .. this.GetElements(ownedIds).SelectMany(this.CollectTree)];
        }

        /// <summary>
        /// Gets the members of an element: the elements owned by its owned relationships.
        /// </summary>
        /// <param name="owner">The element whose members are read.</param>
        /// <returns>The members of the element.</returns>
        private IEnumerable<IElement> GetOwnedMembers(IElement owner)
        {
            return this.GetElements(owner.OwnedRelationship)
                .OfType<IRelationship>()
                .SelectMany(relationship => this.GetElements(relationship.OwnedRelatedElement));
        }

        /// <summary>
        /// Gets the owner of an element: the element that owns it, directly for a relationship, or through its owning
        /// membership otherwise.
        /// </summary>
        /// <param name="element">The element whose owner is read.</param>
        /// <returns>The owner of the element, or <c>null</c> for a root element.</returns>
        private IElement GetOwner(IElement element)
        {
            Guid? ownerId = element switch
            {
                IRelationship { OwningRelatedElement: { } owningRelatedElementId } => owningRelatedElementId,
                { OwningRelationship: { } owningRelationshipId } => (this.elementsById.GetValueOrDefault(owningRelationshipId) as IRelationship)?.OwningRelatedElement,
                _ => null
            };

            return ownerId == null ? null : this.elementsById.GetValueOrDefault(ownerId.Value);
        }

        /// <summary>
        /// Tells whether an element is marked for deletion, itself or through one of its owners.
        /// </summary>
        /// <param name="element">The element to check.</param>
        /// <returns><c>true</c> when the element is deleted at the end of the batch.</returns>
        private bool IsMarkedForDeletion(IElement element)
        {
            for (var current = element; current != null; current = this.GetOwner(current))
            {
                if (this.deletions.Exists(deletion => deletion.Element.Id == current.Id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the elements of the working copy that have the given identifiers, skipping the unknown ones.
        /// </summary>
        /// <param name="elementIds">The <c>Id</c> of the elements.</param>
        /// <returns>The elements that have these identifiers.</returns>
        private IEnumerable<IElement> GetElements(IEnumerable<Guid> elementIds)
        {
            return elementIds
                .Select(this.elementsById.GetValueOrDefault)
                .Where(element => element != null);
        }

        /// <summary>
        /// Creates an element of the given metaclass. An occurrence usage, such as a part, is composite, as it is when it is
        /// declared without <c>ref</c>.
        /// </summary>
        /// <param name="type">The name of the metaclass, for example <c>PartUsage</c>.</param>
        /// <returns>The new element, which has no identifier yet.</returns>
        /// <exception cref="InvalidChangeException">Thrown when a change cannot create an element of this metaclass.</exception>
        private static IElement CreateElement(string type)
        {
            if (!CreatableMetaclasses.TryGetValue(type, out var metaclass))
            {
                throw new InvalidChangeException($"'{type}' is not the metaclass of a package, a definition or a usage, for example Package, PartDefinition, PartUsage, AttributeUsage or RequirementUsage. Relationships are built by the server.");
            }

            var element = (IElement)Activator.CreateInstance(metaclass);

            if (element is IOccurrenceUsage occurrenceUsage)
            {
                occurrenceUsage.IsComposite = true;
            }

            return element;
        }

        /// <summary>
        /// Checks that an element can have a numeric value.
        /// </summary>
        /// <param name="element">The element to check.</param>
        /// <exception cref="InvalidChangeException">Thrown when the element is not an attribute.</exception>
        private static void CheckCanHaveValue(IElement element)
        {
            if (element is not IAttributeUsage)
            {
                throw new InvalidChangeException($"Only an attribute has a value, not the {Describe(element)}.");
            }
        }

        /// <summary>
        /// Checks that an element has the kind expected by its role in a change.
        /// </summary>
        /// <param name="element">The element to check.</param>
        /// <param name="hasExpectedKind">Whether the element has the expected kind.</param>
        /// <param name="role">The role of the element in the change, for example <c>owner</c>.</param>
        /// <param name="expectedKind">The expected kind, for example <c>a package</c>, used in the message.</param>
        /// <exception cref="InvalidChangeException">Thrown when the element does not have the expected kind.</exception>
        private static void CheckKind(IElement element, bool hasExpectedKind, string role, string expectedKind)
        {
            if (!hasExpectedKind)
            {
                throw new InvalidChangeException($"The {role} must be {expectedKind}, not the {Describe(element)}.");
            }
        }

        /// <summary>
        /// Checks that a name is given.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <exception cref="InvalidChangeException">Thrown when the name is <c>null</c>, empty or white space.</exception>
        private static void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidChangeException("The name is missing.");
            }
        }

        /// <summary>
        /// Describes an element in a message, by its metaclass and its name, or its identifier when it has no name.
        /// </summary>
        /// <param name="element">The element to describe.</param>
        /// <returns>The description of the element, for example <c>PartUsage 'camera'</c>.</returns>
        private static string Describe(IElement element)
        {
            return element.DeclaredName == null ? $"{element.GetType().Name} {element.Id}" : $"{element.GetType().Name} '{element.DeclaredName}'";
        }
    }
}
