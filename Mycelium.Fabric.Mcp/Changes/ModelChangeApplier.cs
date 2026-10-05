// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeApplier.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Dal;

    using PocoRelationship = SysML2.NET.Core.POCO.Root.Elements.IRelationship;

    /// <summary>
    /// Applies a batch of <see cref="ModelChange"/>s, in order, to a working copy of a SysML v2 model made of DTOs. The
    /// working copy is modified in place: the caller keeps it only when the whole batch is applied.
    /// </summary>
    /// <remarks>
    /// The changes build the elements and relationships of the SysML v2 metamodel, as the textual notation does: an owning
    /// membership for each owned element, a <c>FeatureTyping</c> for <c>camera : Camera</c>, a <c>FeatureValue</c> that owns
    /// a <c>LiteralRational</c> for <c>mass = 38</c>, and an owned <c>Documentation</c> for a text. Deletions are checked and
    /// carried out at the end of the batch, once the references to the deleted elements are known. An applier is meant for
    /// a single batch.
    /// </remarks>
    public class ModelChangeApplier
    {
        /// <summary>
        /// The elements of the working copy, indexed by their <c>Id</c>.
        /// </summary>
        private readonly Dictionary<Guid, IElement> elementsById;

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
        /// Initializes a new instance of the <see cref="ModelChangeApplier"/> class.
        /// </summary>
        /// <param name="workingCopy">The DTOs of the model to modify, which must not be shared with the current model.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="workingCopy"/> is <c>null</c>.
        /// </exception>
        public ModelChangeApplier(IEnumerable<IElement> workingCopy)
        {
            ArgumentNullException.ThrowIfNull(workingCopy);

            this.elementsById = workingCopy.ToDictionary(element => element.Id);
        }

        /// <summary>
        /// Gets the elements of the working copy, with the changes applied so far.
        /// </summary>
        public IReadOnlyCollection<IElement> Elements => this.elementsById.Values;

        /// <summary>
        /// Applies the given changes, in order, to the working copy. Every change is checked, so that all the problems of the
        /// batch are reported at once.
        /// </summary>
        /// <param name="changes">The changes to apply.</param>
        /// <returns>
        /// The <see cref="ApplyChangesResult"/>: applied with the created elements, or not applied with the problems. When the
        /// batch is not applied, the working copy is left half-modified and must be discarded.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="changes"/> is <c>null</c>.
        /// </exception>
        public ApplyChangesResult Apply(IReadOnlyList<ModelChange> changes)
        {
            ArgumentNullException.ThrowIfNull(changes);

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

            return problems.Count == 0 ? new ApplyChangesResult(true, this.createdElements, []) : new ApplyChangesResult(false, [], problems);
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

            try
            {
                switch (change.Kind)
                {
                    case ChangeKind.CreatePackage:
                        this.CreatePackage(change);
                        break;
                    case ChangeKind.CreatePartDefinition:
                        this.CreatePartDefinition(change);
                        break;
                    case ChangeKind.CreatePart:
                        this.CreatePart(change);
                        break;
                    case ChangeKind.CreateAttribute:
                        this.CreateAttribute(change);
                        break;
                    case ChangeKind.CreateRequirement:
                        this.CreateRequirement(change);
                        break;
                    case ChangeKind.Rename:
                        this.Rename(change);
                        break;
                    case ChangeKind.SetValue:
                        this.SetValue(change);
                        break;
                    case ChangeKind.SetDefinition:
                        this.SetDefinition(change);
                        break;
                    case ChangeKind.Delete:
                        this.MarkForDeletion(change, changeNumber);
                        break;
                    default:
                        throw new InvalidChangeException($"'{change.Kind}' is not a kind of change.");
                }

                return null;
            }
            catch (InvalidChangeException exception)
            {
                if (!string.IsNullOrWhiteSpace(change.TemporaryName) && !this.temporaryNames.ContainsKey(change.TemporaryName))
                {
                    this.failedTemporaryNames.Add(change.TemporaryName);
                }

                return $"Change {changeNumber} ({change.Kind}): {exception.Message}";
            }
        }

        /// <summary>
        /// Creates a package in a package, or at the top level of the model when the change has no owner.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.CreatePackage"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreatePackage(ModelChange change)
        {
            this.CheckNewElement(change);

            var owner = string.IsNullOrWhiteSpace(change.Owner) ? this.GetOrCreateRootNamespace() : this.Resolve(change.Owner, "owner");
            CheckKind(owner, owner is IPackage or Namespace, "owner", "a package");

            this.AddOwnedMember(owner, new Package(), change);
        }

        /// <summary>
        /// Creates a part definition in a package.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.CreatePartDefinition"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreatePartDefinition(ModelChange change)
        {
            this.CheckNewElement(change);

            var owner = this.Resolve(change.Owner, "owner");
            CheckKind(owner, owner is IPackage, "owner", "a package");

            this.AddOwnedMember(owner, new PartDefinition(), change);
        }

        /// <summary>
        /// Creates a part, typed by a part definition when the change gives one, in a package, a part definition or a part.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.CreatePart"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreatePart(ModelChange change)
        {
            this.CheckNewElement(change);

            var owner = this.Resolve(change.Owner, "owner");
            CheckKind(owner, owner is IPackage or IPartDefinition or IPartUsage, "owner", "a package, a part definition or a part");

            var definition = string.IsNullOrWhiteSpace(change.Definition) ? null : this.ResolvePartDefinition(change.Definition);
            var part = this.AddOwnedMember(owner, new PartUsage { IsComposite = true }, change);

            if (definition != null)
            {
                this.AddTyping(part, definition);
            }
        }

        /// <summary>
        /// Creates an attribute, bound to a numeric value when the change gives one, in a part definition or a part.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.CreateAttribute"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreateAttribute(ModelChange change)
        {
            this.CheckNewElement(change);

            var owner = this.Resolve(change.Owner, "owner");
            CheckKind(owner, owner is IPartDefinition or IPartUsage, "owner", "a part definition or a part");

            var attribute = this.AddOwnedMember(owner, new AttributeUsage(), change);

            if (change.Value != null)
            {
                this.AddValue(attribute, change.Value.Value);
            }
        }

        /// <summary>
        /// Creates a requirement, whose text is its documentation, in a package.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.CreateRequirement"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreateRequirement(ModelChange change)
        {
            this.CheckNewElement(change);

            if (string.IsNullOrWhiteSpace(change.Text))
            {
                throw new InvalidChangeException("The text of the requirement is missing.");
            }

            var owner = this.Resolve(change.Owner, "owner");
            CheckKind(owner, owner is IPackage, "owner", "a package");

            this.AddOwnedMember(owner, new RequirementUsage(), change);
        }

        /// <summary>
        /// Renames an element.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.Rename"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void Rename(ModelChange change)
        {
            var element = this.Resolve(change.Element, "element");
            CheckKind(element, element is not IRelationship, "element", "an element that is not a relationship");
            CheckName(change.Name);
            this.CheckNameIsFree(this.GetOwner(element), change.Name, element);

            element.DeclaredName = change.Name;
        }

        /// <summary>
        /// Replaces the value of an attribute: its current <c>FeatureValue</c>s are removed and a new one is added.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.SetValue"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void SetValue(ModelChange change)
        {
            var attribute = this.Resolve(change.Element, "element");
            CheckKind(attribute, attribute is IAttributeUsage, "element", "an attribute");

            if (change.Value == null)
            {
                throw new InvalidChangeException("The value is missing.");
            }

            this.RemoveOwnedRelationships<IFeatureValue>(attribute);
            this.AddValue(attribute, change.Value.Value);
        }

        /// <summary>
        /// Replaces the definition of a part: its current <c>FeatureTyping</c>s are removed and a new one is added.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.SetDefinition"/> change.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void SetDefinition(ModelChange change)
        {
            var part = this.Resolve(change.Element, "element");
            CheckKind(part, part is IPartUsage, "element", "a part");

            var definition = this.ResolvePartDefinition(change.Definition);

            this.RemoveOwnedRelationships<IFeatureTyping>(part);
            this.AddTyping(part, definition);
        }

        /// <summary>
        /// Marks an element for deletion. It is deleted at the end of the batch, by <see cref="RemoveDeletedElements"/>.
        /// </summary>
        /// <param name="change">The <see cref="ChangeKind.Delete"/> change.</param>
        /// <param name="changeNumber">The position of the change in the batch, starting at 1.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void MarkForDeletion(ModelChange change, int changeNumber)
        {
            var element = this.Resolve(change.Element, "element");
            CheckKind(element, element is not IRelationship, "element", "an element that is not a relationship");

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
                : $"Change {changeNumber} (Delete): the {Describe(deletedElement)} is still referenced by {string.Join(", ", referencingElements)}. Change or delete these elements first.";
        }

        /// <summary>
        /// Checks the name and the temporary name of an element to create.
        /// </summary>
        /// <param name="change">The change that creates the element.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the name is missing, or when the temporary name is an identifier or is already used.
        /// </exception>
        private void CheckNewElement(ModelChange change)
        {
            CheckName(change.Name);

            if (string.IsNullOrWhiteSpace(change.TemporaryName))
            {
                return;
            }

            if (Guid.TryParse(change.TemporaryName, out _))
            {
                throw new InvalidChangeException($"The temporary name '{change.TemporaryName}' must not be an identifier.");
            }

            if (this.temporaryNames.ContainsKey(change.TemporaryName))
            {
                throw new InvalidChangeException($"The temporary name '{change.TemporaryName}' is already used by a previous change.");
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
        /// Gets the part definition designated by an identifier or a temporary name.
        /// </summary>
        /// <param name="reference">The <c>Id</c> or temporary name of the part definition.</param>
        /// <returns>The designated part definition.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the reference designates no element (see <see cref="Resolve"/>) or an element that is not a part
        /// definition.
        /// </exception>
        private IElement ResolvePartDefinition(string reference)
        {
            var definition = this.Resolve(reference, "definition");
            CheckKind(definition, definition is IPartDefinition, "definition", "a part definition");

            return definition;
        }

        /// <summary>
        /// Gets the root namespace of the model, that owns its top-level elements, and creates it when the model has none.
        /// </summary>
        /// <returns>The root namespace of the model.</returns>
        private IElement GetOrCreateRootNamespace()
        {
            var rootNamespace = this.elementsById.Values
                .OfType<Namespace>()
                .FirstOrDefault(candidate => candidate.OwningRelationship == null && !this.IsMarkedForDeletion(candidate));

            return rootNamespace ?? this.Add(new Namespace());
        }

        /// <summary>
        /// Adds a new element to the working copy as a member of its owner, with its documentation, and records it as created.
        /// </summary>
        /// <typeparam name="T">The type of the new element.</typeparam>
        /// <param name="owner">The owner of the new element.</param>
        /// <param name="member">The new element.</param>
        /// <param name="change">The change that creates the element, which gives its name, temporary name and text.</param>
        /// <returns>The new element.</returns>
        /// <exception cref="InvalidChangeException">Thrown when another member of the owner has the name.</exception>
        private T AddOwnedMember<T>(IElement owner, T member, ModelChange change) where T : class, IElement
        {
            this.CheckNameIsFree(owner, change.Name);

            member.DeclaredName = change.Name;

            // A feature (part, attribute...) of a type (part definition, part) is owned through a FeatureMembership; any other
            // member, for example an element of a package, through an OwningMembership.
            IMembership membership = member is IFeature && owner is IType
                ? new FeatureMembership { Visibility = VisibilityKind.Public }
                : new OwningMembership { Visibility = VisibilityKind.Public };

            this.AddOwnedRelationship(owner, membership, this.Add(member));

            if (!string.IsNullOrWhiteSpace(change.Text))
            {
                this.AddOwnedRelationship(member, new OwningMembership { Visibility = VisibilityKind.Public }, this.Add(new Documentation { Body = change.Text }));
            }

            if (!string.IsNullOrWhiteSpace(change.TemporaryName))
            {
                this.temporaryNames.Add(change.TemporaryName, member.Id);
            }

            this.createdElements.Add(new CreatedElement(change.TemporaryName, member.Id, member.DeclaredName, member.GetType().Name));

            return member;
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
        /// Types a part by a part definition, as <c>camera : Camera</c> does: a <c>FeatureTyping</c> owned by the part.
        /// </summary>
        /// <param name="part">The typed part.</param>
        /// <param name="definition">The part definition.</param>
        private void AddTyping(IElement part, IElement definition)
        {
            this.AddOwnedRelationship(part, new FeatureTyping { TypedFeature = part.Id, Type = definition.Id });
        }

        /// <summary>
        /// Adds a relationship to the working copy, owned by an element and owning another element when one is given. Both
        /// sides of each link are set, since a DTO only holds identifiers.
        /// </summary>
        /// <param name="owner">The element that owns the relationship.</param>
        /// <param name="relationship">The new relationship.</param>
        /// <param name="ownedRelatedElement">The element owned by the relationship, or <c>null</c> when it owns none.</param>
        private void AddOwnedRelationship(IElement owner, IRelationship relationship, IElement ownedRelatedElement = null)
        {
            this.Add(relationship);

            relationship.OwningRelatedElement = owner.Id;
            owner.OwnedRelationship.Add(relationship.Id);

            if (ownedRelatedElement != null)
            {
                relationship.OwnedRelatedElement.Add(ownedRelatedElement.Id);
                ownedRelatedElement.OwningRelationship = relationship.Id;
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

            return element;
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
        /// Removes an element and everything it owns from the working copy, and detaches it from its owner.
        /// </summary>
        /// <param name="root">The element to remove: a relationship owned by an element, or a root element.</param>
        private void RemoveTree(IElement root)
        {
            if (root is IRelationship { OwningRelatedElement: { } ownerId } && this.elementsById.TryGetValue(ownerId, out var owner))
            {
                owner.OwnedRelationship.Remove(root.Id);
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
