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

    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.DTO.Core.Classifiers;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Expressions;
    using SysML2.NET.Core.DTO.Kernel.FeatureValues;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Actions;
    using SysML2.NET.Core.DTO.Systems.Allocations;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Connections;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Enumerations;
    using SysML2.NET.Core.DTO.Systems.Flows;
    using SysML2.NET.Core.DTO.Systems.Interfaces;
    using SysML2.NET.Core.DTO.Systems.Occurrences;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Ports;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.DTO.Systems.States;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Dal;
    using SysML2.NET.PIM;
    using SysML2.NET.PSM.DTO;

    using PocoConnector = SysML2.NET.Core.POCO.Kernel.Connectors.IConnector;
    using PocoDependency = SysML2.NET.Core.POCO.Root.Dependencies.IDependency;
    using PocoElement = SysML2.NET.Core.POCO.Root.Elements.IElement;
    using PocoRelationship = SysML2.NET.Core.POCO.Root.Elements.IRelationship;
    using PocoSatisfyRequirementUsage = SysML2.NET.Core.POCO.Systems.Requirements.ISatisfyRequirementUsage;
    using PocoTransitionUsage = SysML2.NET.Core.POCO.Systems.States.ITransitionUsage;

    /// <summary>
    /// A batch of <see cref="ModelChange"/>s applied, in order, to a working copy of a SysML v2 model made of DTOs. The batch
    /// records the elements it creates, modifies and removes, so that it can describe its result as the change of a commit.
    /// </summary>
    /// <remarks>
    /// The changes build the elements and relationships of the SysML v2 metamodel, as the textual notation does: an owning
    /// membership for each owned element, a <c>FeatureTyping</c> for <c>camera : Camera</c>, a <c>FeatureValue</c> that owns
    /// a <c>LiteralRational</c> for <c>mass = 38</c>, and an owned <c>Documentation</c> for a text. Deletions are checked and
    /// carried out at the end of the batch, once the references to the deleted elements are known. A batch is applied once.
    /// The properties that make requirements verifiable are in <c>ModelChangeBatch.Requirements.cs</c>.
    /// </remarks>
    internal sealed partial class ModelChangeBatch
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
        /// The interfaces of the connection definitions and connectors that a change can create, although they are
        /// relationships: the server relates them to the features of their ends, which are created in a definition, or built
        /// from paths for a connector. Interface definitions and usages are connection definitions and usages, and so are
        /// allocation usages.
        /// </summary>
        private static readonly System.Type[] ConnectionMetaclasses = [typeof(IConnectionDefinition), typeof(IConnectionUsage), typeof(IBindingConnectorAsUsage), typeof(ISuccessionAsUsage), typeof(IFlowUsage)];

        /// <summary>
        /// The interfaces of the connection definitions that a change cannot create yet, because the server does not build
        /// their ends: allocation definitions.
        /// </summary>
        private static readonly System.Type[] UnsupportedConnectionMetaclasses = [typeof(IAllocationDefinition)];

        /// <summary>
        /// The concrete DTO classes of SysML2.NET indexed by their name, which is the name of their metaclass and of the POCO
        /// class of the same metaclass, to build the stand-in of an element of the standard library. Declared before
        /// <see cref="CreatableMetaclasses"/>, which is built from it.
        /// </summary>
        private static readonly Dictionary<string, System.Type> ElementMetaclasses = typeof(IElement).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IElement).IsAssignableFrom(type))
            .ToDictionary(type => type.Name);

        /// <summary>
        /// The metaclasses that a change can create, indexed by their name: the concrete DTO classes of SysML2.NET that are
        /// packages, definitions or usages, but not relationships, whose related elements only the server can build, and the
        /// <see cref="ConnectionMetaclasses"/>.
        /// </summary>
        private static readonly Dictionary<string, System.Type> CreatableMetaclasses = ElementMetaclasses.Values
            .Where(IsCreatable)
            .ToDictionary(type => type.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The names of the <see cref="CreatableMetaclasses"/>, in alphabetical order, as the schema of apply_changes lists them.
        /// </summary>
        internal static readonly IReadOnlyList<string> CreatableMetaclassNames = [.. CreatableMetaclasses.Keys.Order(StringComparer.Ordinal)];

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
        /// The <c>Id</c> of the elements created by the previous batches, indexed by their temporary name. A name of the batch
        /// designates the element of the batch first.
        /// </summary>
        private readonly IReadOnlyDictionary<string, Guid> earlierTemporaryNames;

        /// <summary>
        /// The temporary names of the elements whose creation failed, to explain why later changes cannot use them.
        /// </summary>
        private readonly HashSet<string> failedTemporaryNames = [];

        /// <summary>
        /// The number of the change that created each element of the batch, indexed by the <c>Id</c> of the element.
        /// </summary>
        private readonly Dictionary<Guid, int> creationChangeNumbers = [];

        /// <summary>
        /// The elements to delete at the end of the batch, with the number of the change that deletes them.
        /// </summary>
        private readonly List<(int ChangeNumber, IElement Element)> deletions = [];

        /// <summary>
        /// The <c>Id</c> of the elements added or modified since the POCOs of the working copy were last brought up to date.
        /// </summary>
        private readonly HashSet<Guid> unassembledIds = [];

        /// <summary>
        /// The <see cref="IStandardLibrary"/> whose elements the working copy can reference.
        /// </summary>
        private readonly IStandardLibrary standardLibrary;

        /// <summary>
        /// The stand-ins of the elements of the standard library that the changes reference, indexed by their <c>Id</c>.
        /// </summary>
        private readonly Dictionary<Guid, IElement> libraryStandIns = [];

        /// <summary>
        /// The SysML2.NET <see cref="Assembler"/> that holds the POCOs of the working copy, or <c>null</c> until they are first
        /// read and after a removal.
        /// </summary>
        private Assembler assembler;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelChangeBatch"/> class.
        /// </summary>
        /// <param name="workingCopy">The DTOs of the model to modify, which must not be shared with the current model.</param>
        /// <param name="earlierTemporaryNames">
        /// The <c>Id</c> of the elements created by the previous batches, indexed by their temporary name.
        /// </param>
        /// <param name="standardLibrary">The <see cref="IStandardLibrary"/> whose elements the working copy can reference.</param>
        public ModelChangeBatch(IEnumerable<IElement> workingCopy, IReadOnlyDictionary<string, Guid> earlierTemporaryNames, IStandardLibrary standardLibrary)
        {
            this.elementsById = workingCopy.ToDictionary(element => element.Id);
            this.originalIds = [.. this.elementsById.Keys];
            this.earlierTemporaryNames = earlierTemporaryNames;
            this.standardLibrary = standardLibrary;
        }

        /// <summary>
        /// Adds the temporary names that the batch has given to its elements to the given names, replacing the names that were
        /// already there: a name designates the last element that has received it.
        /// </summary>
        /// <param name="temporaryNames">The temporary names to complete, indexed by name.</param>
        public void AddTemporaryNamesTo(Dictionary<string, Guid> temporaryNames)
        {
            foreach (var (temporaryName, elementId) in this.temporaryNames)
            {
                temporaryNames[temporaryName] = elementId;
            }
        }

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
        /// not a relationship, one of the <see cref="ConnectionMetaclasses"/>, or a dependency. A conjugated port definition is
        /// built by the server with its port definition.
        /// </summary>
        /// <param name="type">The DTO class.</param>
        /// <returns><c>true</c> when a change can create an element of this class.</returns>
        private static bool IsCreatable(System.Type type)
        {
            var isPackageDefinitionOrUsage = typeof(IPackage).IsAssignableFrom(type) || typeof(IDefinition).IsAssignableFrom(type) || typeof(IUsage).IsAssignableFrom(type);

            return (isPackageDefinitionOrUsage && !typeof(IRelationship).IsAssignableFrom(type) && !typeof(IConjugatedPortDefinition).IsAssignableFrom(type))
                || (ConnectionMetaclasses.Any(metaclass => metaclass.IsAssignableFrom(type)) && !UnsupportedConnectionMetaclasses.Any(metaclass => metaclass.IsAssignableFrom(type)))
                || typeof(IDependency).IsAssignableFrom(type);
        }

        /// <summary>
        /// Tells whether a change can update or delete an element: any element that is not a relationship, a connection
        /// definition or a connector.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns><c>true</c> when a change can update or delete the element.</returns>
        private static bool IsChangeable(IElement element)
        {
            return element is not IRelationship or IConnectionDefinition or IConnectorAsUsage;
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
            var addedCount = this.addedIds.Count;

            try
            {
                switch (changeType)
                {
                    case ChangeType.CREATED:
                        this.Create(change);

                        foreach (var addedId in this.addedIds.Skip(addedCount))
                        {
                            this.creationChangeNumbers[addedId] = changeNumber;
                        }

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
        /// model when the payload gives no owner, with its documentation, definition and value. An element that redefines a
        /// feature that its owner inherits, given by <c>redefines</c> or by its name, is a redefinition (<c>:&gt;&gt; mass</c>).
        /// </summary>
        /// <param name="change">The change that creates the element, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void Create(ModelChange change)
        {
            var payload = change.Payload;

            this.CheckTemporaryName(change.Identity);

            var member = CreateElement(payload.Type);

            if (member is IDependency)
            {
                this.CreateDependency(member, change);
                return;
            }

            CheckDependencyProperties(payload);
            CheckRedefinitionProperties(member, payload);

            if (payload.StateAction != null)
            {
                this.CreateStateAction(member, change);
                return;
            }

            if (member is ITransitionUsage)
            {
                this.CreateTransition(member, change);
                return;
            }

            if (member is ISatisfyRequirementUsage)
            {
                this.CreateSatisfy(member, change);
                return;
            }

            if (member is IPerformActionUsage)
            {
                this.CreatePerform(member, change);
                return;
            }

            if (payload.Performed != null)
            {
                throw new InvalidChangeException("Only a PerformActionUsage performs an action.");
            }

            if (member is IConnectorAsUsage)
            {
                this.CreateConnector(member, change);
                return;
            }

            if (payload.Redefines == null || payload.Name != null)
            {
                CheckName(payload.Name);
            }

            var owner = string.IsNullOrWhiteSpace(payload.Owner) ? this.GetOrCreateRootNamespace() : this.Resolve(payload.Owner, OwnerRole);
            CheckKind(owner, owner is INamespace, OwnerRole, "a namespace, for example a package, a definition or a usage");

            // A redefinition takes the name of the feature that it redefines, as :>> mass does, unless it is given another one.
            var redefined = this.ResolveRedefinedFeature(member, owner, payload);
            var redefinedName = redefined == null ? null : this.GetEffectiveName(redefined);
            member.DeclaredName = payload.Name == redefinedName ? null : payload.Name;
            this.CheckNameIsFree(owner, payload.Name ?? redefinedName);

            if (member is IRequirementUsage && string.IsNullOrWhiteSpace(payload.Text))
            {
                throw new InvalidChangeException("The text of the requirement is missing.");
            }

            var definition = payload.Definition == null ? null : this.ResolveDefinition(member, payload.Definition);
            var general = payload.Specializes == null ? null : this.ResolveGeneral(member, payload.Specializes);

            if (member is IEnumerationUsage)
            {
                definition = GetEnumerationDefinition(owner, definition);
            }

            this.CheckValue(payload.Value, payload.Unit);

            if (payload.Value != null)
            {
                CheckCanHaveValue(member);
            }

            if (redefined != null && payload.Value != null)
            {
                this.CheckValueCanBeRedefined(redefined);
            }

            this.CheckRequirementProperties(member, payload);
            CheckFeatureProperties(member, owner, payload);
            CheckBehaviourProperties(member, payload);
            CheckVariationProperties(member, owner, payload);

            var variant = payload.Variant == null ? null : this.ResolveVariant(member, definition, payload.Variant);

            SetFeatureFlags(member, owner, payload);
            SetVariation(member, payload);
            this.AddOwnedMember(owner, member, change.Identity);

            if (redefined != null)
            {
                this.AddOwnedRelationship(member, new Redefinition { RedefiningFeature = member.Id, RedefinedFeature = redefined.Id });
            }

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(member, payload.Text);
            }

            if (definition != null)
            {
                this.AddTyping(member, definition, payload.Conjugated == true);
            }

            this.AddCatalogueTyping(member, owner, definition);

            if (general != null)
            {
                this.SetSpecialization(member, general);
            }

            if (payload.Value != null)
            {
                // A value of a definition is the typical value of its usages, which they can redefine.
                this.AddValue(member, payload.Value.Value, payload.Unit, payload.IsDefault ?? owner is IDefinition);
            }

            if (variant != null)
            {
                this.SetVariant(member, variant);
            }

            if (payload.Multiplicity != null)
            {
                this.SetMultiplicity(member, payload.Multiplicity);
            }

            if (member is IPortDefinition)
            {
                this.GetOrCreateConjugatedPortDefinition(member);
            }

            this.SetRequirementProperties(member, payload);
        }

        /// <summary>
        /// Updates the properties of an element given by the payload: its name, its definition, its value and the kind of its
        /// value (default or fixed), its documentation, its direction, its multiplicity, the conjugation of a port and, for a
        /// requirement, its <c>ReqId</c> and its constraints. A new definition, value, documentation, multiplicity or
        /// constraint replaces the current one; a new value keeps the kind of the current one unless the payload gives it.
        /// </summary>
        /// <param name="change">The change that updates the element designated by its identity.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void Update(ModelChange change)
        {
            var payload = change.Payload;
            var element = this.Resolve(change.Identity, ElementRole);
            CheckKind(element, IsChangeable(element), ElementRole, "an element that is not a relationship");

            var definition = this.CheckUpdate(element, payload);
            var general = payload.Specializes == null ? null : this.ResolveGeneral(element, payload.Specializes);
            var variantDefinition = payload.Definition == null ? null : definition;
            var variant = payload.Variant == null ? null : this.ResolveVariant(element, variantDefinition, payload.Variant);

            if (general != null)
            {
                this.CheckNoSpecializationCycle(element, general);
            }

            if (payload.Name != null)
            {
                element.DeclaredName = payload.Name;
                this.MarkModified(element);
            }

            if (general != null)
            {
                this.SetSpecialization(element, general);
            }

            if (definition != null)
            {
                this.RemoveOwnedRelationships<IFeatureTyping>(element);
                this.AddTyping(element, definition, payload.Conjugated == true);
            }

            if (payload.Direction != null)
            {
                SetDirection((IFeature)element, payload.Direction);
                this.MarkModified(element);
            }

            if (payload.Value != null)
            {
                var isDefault = payload.IsDefault ?? this.GetValueKind(element) ?? this.GetOwner(element) is IDefinition;
                this.RemoveOwnedRelationships<IFeatureValue>(element);
                this.AddValue(element, payload.Value.Value, payload.Unit, isDefault);
            }
            else if (payload.IsDefault != null)
            {
                this.SetValueKind(element, payload.IsDefault.Value);
            }

            if (variant != null)
            {
                this.SetVariant(element, variant);
            }

            if (payload.Text != null)
            {
                this.RemoveDocumentation(element);
                this.AddDocumentation(element, payload.Text);
            }

            if (payload.Multiplicity != null)
            {
                this.SetMultiplicity(element, payload.Multiplicity);
            }

            this.SetRequirementProperties(element, payload);
        }

        /// <summary>
        /// Checks the payload of an update before any modification, so that a refused update leaves the element unchanged.
        /// </summary>
        /// <param name="element">The updated element.</param>
        /// <param name="payload">The properties to change.</param>
        /// <returns>
        /// The new definition of the element, or <c>null</c> when the payload does not change it. When only the conjugation
        /// of a port changes, it is the current port definition of the port.
        /// </returns>
        /// <exception cref="InvalidChangeException">Thrown when the payload cannot be applied to the element.</exception>
        private IElement CheckUpdate(IElement element, ElementPayload payload)
        {
            if (payload.Owner != null)
            {
                throw new InvalidChangeException("An update cannot change the owner of an element: delete the element and create it again in its new owner.");
            }

            this.CheckValue(payload.Value, payload.Unit);

            if (payload.Performed != null)
            {
                throw new InvalidChangeException("The performed action of a PerformActionUsage cannot change: delete the element and create it again.");
            }

            CheckNoBehaviourUpdate(element, payload);

            if (payload.IsVariation != null)
            {
                throw new InvalidChangeException("isVariation only applies to the creation of a PartDefinition: delete the definition and create it again.");
            }

            if (payload.Redefines != null)
            {
                throw new InvalidChangeException("redefines only applies to the creation of a feature: to give a usage its own value, definition or multiplicity, "
                    + "create a new element under it that redefines the inherited feature.");
            }

            if (payload is { Name: null, Definition: null, Value: null, Text: null, Constraint: null, Constraints: null, Direction: null, Conjugated: null, Ends: null, IsEnd: null, Specializes: null }
                && payload is { Multiplicity: null, Variant: null, IsDefault: null } && string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException("The payload changes nothing: give the name, definition, specializes, value, isDefault, variant, text, direction, conjugated, multiplicity, reqId or constraint to change, "
                    + "or no payload to delete the element.");
            }

            this.CheckFeatureUpdate(element, payload);
            this.CheckValueKindUpdate(element, payload);

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

            this.CheckRequirementProperties(element, payload);
            CheckDependencyProperties(payload);

            return payload switch
            {
                { Definition: not null } when element is IEnumerationUsage => GetEnumerationDefinition(this.GetOwner(element), this.ResolveDefinition(element, payload.Definition)),
                { Definition: not null } => this.ResolveDefinition(element, payload.Definition),
                { Conjugated: not null } => this.GetPortDefinition(element),
                _ => null
            };
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
            CheckKind(element, IsChangeable(element) || element is IDependency, ElementRole, "an element that is not a relationship, or a dependency");

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
            return this.AssembleElements()
                .OfType<PocoRelationship>()
                .ToList();
        }

        /// <summary>
        /// Gets the POCOs of the working copy, built by SysML2.NET, which computes their derived properties, such as
        /// <c>qualifiedName</c> or the <c>feature</c> of a type, from the whole working copy and the standard library.
        /// </summary>
        /// <returns>The POCOs of the working copy, without the elements of the standard library.</returns>
        private IEnumerable<PocoElement> AssembleElements()
        {
            var assembledElements = this.GetAssembler().Cache;

            return this.elementsById.Keys.Select(elementId => assembledElements[elementId].Value);
        }

        /// <summary>
        /// Gets the POCOs of some elements of the working copy, to read the derived properties that SysML2.NET computes, such
        /// as <c>qualifiedName</c>.
        /// </summary>
        /// <param name="elements">The elements of the working copy.</param>
        /// <returns>The POCOs of these elements, in their order, without duplicates.</returns>
        private List<PocoElement> AssembleElements(IEnumerable<IElement> elements)
        {
            return [.. elements.Select(element => element.Id).Distinct().Select(this.GetAssembledElement).Where(element => element != null)];
        }

        /// <summary>
        /// Gets the POCO of an element of the working copy, to read the derived properties that SysML2.NET computes.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <returns>The POCO of the element, or <c>null</c> when the element is not in the working copy.</returns>
        private PocoElement GetAssembledElement(Guid elementId)
        {
            return this.GetAssembler().Cache.TryGetValue(elementId, out var lazyElement) ? lazyElement.Value : null;
        }

        /// <summary>
        /// Gets the SysML2.NET <see cref="Assembler"/> that holds the POCOs of the working copy. It builds them all the first
        /// time and after a removal, and otherwise only brings up to date the POCOs of the elements that the batch has added or
        /// modified since, which is much faster on a large model. Its cache also holds the named elements of the standard
        /// library, which the working copy can reference.
        /// </summary>
        /// <returns>The assembler, whose cache holds the POCOs of the working copy.</returns>
        private Assembler GetAssembler()
        {
            if (this.assembler == null)
            {
                this.assembler = new Assembler();
                this.assembler.AddElements(this.standardLibrary.NamedElements.Values, this.elementsById.Values);
                this.assembler.Synchronize(this.elementsById.Values);
            }
            else if (this.unassembledIds.Count > 0)
            {
                this.assembler.Synchronize([.. this.GetElements(this.unassembledIds)]);
            }

            this.unassembledIds.Clear();

            return this.assembler;
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
                .Select(DescribeReferencingElement)
                .Distinct()
                .ToList();

            return referencingElements.Count == 0
                ? null
                : $"Change {changeNumber} (delete): the {Describe(deletedElement)} is still referenced by {string.Join(", ", referencingElements)}. Change or delete these elements first.";
        }

        /// <summary>
        /// Describes, in a message, the element that holds a reference: the transition that contains the referencing
        /// relationship, since its source, target, trigger, guard and effect are deleted with it, or else the nearest owner
        /// that has a qualified name (for example the part typed by a definition), or the satisfy link, the unnamed connector
        /// or the unnamed dependency that contains it. An unnamed element is designated by its identifier, so that it can be
        /// deleted. A connector or a dependency, owned through a membership, is itself the referencing relationship.
        /// </summary>
        /// <param name="relationship">The referencing relationship.</param>
        /// <returns>The description of the referencing element.</returns>
        private static string DescribeReferencingElement(PocoRelationship relationship)
        {
            var owners = new List<PocoElement>();

            for (var element = relationship.OwningRelatedElement ?? relationship; element != null; element = element.owner)
            {
                owners.Add(element);
            }

            var holder = owners.OfType<PocoTransitionUsage>().FirstOrDefault<PocoElement>()
                ?? owners.FirstOrDefault(element => element is PocoSatisfyRequirementUsage or PocoConnector { qualifiedName: null } or PocoDependency { qualifiedName: null } || element.qualifiedName != null);

            return holder switch
            {
                null => $"'{relationship.Id}'",
                { qualifiedName: { } qualifiedName } and not PocoSatisfyRequirementUsage => $"'{qualifiedName}'",
                _ => $"the {holder.GetType().Name} {holder.Id}"
            };
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
        /// Checks that no other member of the owner, except the elements marked for deletion, has the given name: its declared
        /// name, or the name of the feature that it redefines when it declares none (<c>:&gt;&gt; mass</c> is named <c>mass</c>).
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
                .FirstOrDefault(member => member != renamedElement && this.GetEffectiveName(member) == name && !this.IsMarkedForDeletion(member));

            if (existingMember != null)
            {
                throw new InvalidChangeException(this.creationChangeNumbers.TryGetValue(existingMember.Id, out var creationChangeNumber)
                    ? $"The {Describe(owner)} already has a member named '{name}', created by change {creationChangeNumber} of this batch: give one of them another name."
                    : $"The {Describe(owner)} already has a member named '{name}', whose identifier is {existingMember.Id}.");
            }
        }

        /// <summary>
        /// Gets the element designated by an identifier, by the temporary name of an element created by a previous change, or
        /// by the qualified name of an element of the standard library, such as <c>ISQ::MassValue</c>. An element of the
        /// library is read-only: it can be referenced, for example as a definition, but neither owns an element nor is updated
        /// or deleted.
        /// </summary>
        /// <param name="reference">The <c>Id</c>, temporary name or qualified name of the element.</param>
        /// <param name="role">The role of the element in the change, for example <c>owner</c>, used in the messages.</param>
        /// <returns>The designated element, a stand-in when it is an element of the standard library.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the reference is missing, designates no element, designates an element marked for deletion, or
        /// designates an element of the standard library as the owner or the updated or deleted element.
        /// </exception>
        private IElement Resolve(string reference, string role)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new InvalidChangeException($"The {role} is missing.");
            }

            var isEarlierName = false;

            if (!Guid.TryParse(reference, out var elementId) && !this.temporaryNames.TryGetValue(reference, out elementId))
            {
                isEarlierName = this.earlierTemporaryNames.TryGetValue(reference, out elementId);

                if (!isEarlierName)
                {
                    if (!this.failedTemporaryNames.Contains(reference) && this.standardLibrary.Resolve(reference) is { } namedLibraryElement)
                    {
                        return this.GetLibraryStandIn(namedLibraryElement, reference, role);
                    }

                    if (reference.Contains("::", StringComparison.Ordinal) && this.standardLibrary.NamedElements.Count == 0 && this.standardLibrary.Problems.Count > 0)
                    {
                        throw new InvalidChangeException($"The {role} '{reference}' is not an element of the model, and the standard library, where it may be, could not be loaded: validate_model tells why.");
                    }

                    throw new InvalidChangeException(this.failedTemporaryNames.Contains(reference)
                        ? $"The {role} '{reference}' has not been created, because the change that creates it has failed."
                        : $"The {role} '{reference}' is neither the identifier of an element nor the temporary name of an element created so far, nor the qualified name of an element of the standard library, such as ISQ::MassValue. Use the identifier of the element: find_elements_by_name or the createdElements of a previous batch give it.");
                }
            }

            if (!this.elementsById.TryGetValue(elementId, out var element))
            {
                if (!isEarlierName && this.standardLibrary.Elements.TryGetValue(elementId, out var libraryElement))
                {
                    return this.GetLibraryStandIn(libraryElement, reference, role);
                }

                throw new InvalidChangeException(isEarlierName
                    ? $"The {role} '{reference}' is the temporary name of an element that no longer exists. Use find_elements_by_name or list_children to get the identifier of the element."
                    : $"No element has the identifier '{reference}'. Use find_elements_by_name or list_children to get a valid identifier.");
            }

            if (this.IsMarkedForDeletion(element))
            {
                throw new InvalidChangeException($"The {role} '{reference}' is deleted by a previous change.");
            }

            return element;
        }

        /// <summary>
        /// Gets the stand-in of an element of the standard library in the batch: a DTO of the same metaclass, with its
        /// identifier and names, that the changes can reference but that is never added to the working copy. The same element
        /// always has the same stand-in.
        /// </summary>
        /// <param name="libraryElement">The element of the standard library.</param>
        /// <param name="reference">The reference of the change to the element, used in the messages.</param>
        /// <param name="role">The role of the element in the change, used in the messages.</param>
        /// <returns>The stand-in of the element.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the element has no name, since the POCOs of the model only see the named elements of the library, or
        /// when it is designated as the owner of an element or as the element to update or delete.
        /// </exception>
        private IElement GetLibraryStandIn(PocoElement libraryElement, string reference, string role)
        {
            if (!this.standardLibrary.NamedElements.ContainsKey(libraryElement.Id))
            {
                throw new InvalidChangeException($"The {role} '{reference}' is an element of the standard library that has no name, which a model cannot reference: use a named element of the library, such as ISQ::MassValue.");
            }

            if (role == OwnerRole)
            {
                throw new InvalidChangeException($"The owner '{reference}' is {libraryElement.qualifiedName}, an element of the standard library, which is read-only: create the element in a package of the model.");
            }

            if (role == ElementRole)
            {
                throw new InvalidChangeException($"The element '{reference}' is {libraryElement.qualifiedName}, an element of the standard library, which is read-only: it cannot be updated or deleted.");
            }

            if (!this.libraryStandIns.TryGetValue(libraryElement.Id, out var standIn))
            {
                standIn = (IElement)Activator.CreateInstance(ElementMetaclasses[libraryElement.GetType().Name]);
                standIn.Id = libraryElement.Id;
                standIn.ElementId = libraryElement.ElementId;
                standIn.DeclaredName = libraryElement.DeclaredName;
                standIn.DeclaredShortName = libraryElement.DeclaredShortName;
                this.libraryStandIns[libraryElement.Id] = standIn;
            }

            return standIn;
        }

        /// <summary>
        /// Gets the definition of a feature, designated by an identifier or a temporary name.
        /// </summary>
        /// <param name="feature">The element to type, which must be a feature.</param>
        /// <param name="reference">The <c>Id</c> or temporary name of the definition.</param>
        /// <returns>The designated definition.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the element is not a feature or is a binding, or when the reference designates no element (see
        /// <see cref="Resolve"/>) or an element that is not a classifier of the kind of the feature (see
        /// <see cref="CheckDefinitionKind"/>).
        /// </exception>
        private IElement ResolveDefinition(IElement feature, string reference)
        {
            if (feature is not IFeature)
            {
                throw new InvalidChangeException($"Only a feature, for example a part or an attribute, has a definition, not the {Describe(feature)}.");
            }

            if (feature is IBindingConnectorAsUsage)
            {
                throw new InvalidChangeException("A BindingConnectorAsUsage has no definition: it states that its two ends are the same thing.");
            }

            if (feature is ISuccessionAsUsage)
            {
                throw new InvalidChangeException("A SuccessionAsUsage has no definition: it only orders its two ends.");
            }

            var definition = this.Resolve(reference, DefinitionRole);
            CheckKind(definition, definition is IClassifier, DefinitionRole, "a classifier, for example a part definition");
            CheckDefinitionKind(feature, definition);

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
        /// Adds a new element to the working copy as a member of its owner, and records its temporary name.
        /// </summary>
        /// <param name="owner">The owner of the new element.</param>
        /// <param name="member">The new element, whose name is set.</param>
        /// <param name="temporaryName">The temporary name of the new element, or <c>null</c> when it has none.</param>
        /// <param name="membership">
        /// The membership through which the owner owns the element, or <c>null</c> for the one of <see cref="CreateMembership"/>.
        /// </param>
        private void AddOwnedMember(IElement owner, IElement member, string temporaryName, IMembership membership = null)
        {
            this.AddOwnedRelationship(owner, membership ?? CreateMembership(owner, member), this.Add(member));

            if (!string.IsNullOrWhiteSpace(temporaryName))
            {
                this.temporaryNames.Add(temporaryName, member.Id);
            }
        }

        /// <summary>
        /// Creates the public membership through which an element owns a member: a <c>FeatureMembership</c> for a feature
        /// (part, attribute...) of a type (part definition, part), an <c>EndFeatureMembership</c> for an end feature, a
        /// <c>VariantMembership</c> for a value of an enumeration definition or a variant of a catalogue, and an
        /// <c>OwningMembership</c> for any other member, for example an element of a package.
        /// </summary>
        /// <param name="owner">The element that owns the member.</param>
        /// <param name="member">The owned member.</param>
        /// <returns>The new membership, not added to the working copy yet.</returns>
        private static IMembership CreateMembership(IElement owner, IElement member)
        {
            return (member, owner) switch
            {
                (IEnumerationUsage, IEnumerationDefinition) => new VariantMembership { Visibility = VisibilityKind.Public },
                (IPartUsage, IPartDefinition { IsVariation: true }) => new VariantMembership { Visibility = VisibilityKind.Public },
                (IFeature { IsEnd: true }, IType) => new EndFeatureMembership { Visibility = VisibilityKind.Public },
                (IFeature, IType) => new FeatureMembership { Visibility = VisibilityKind.Public },
                _ => new OwningMembership { Visibility = VisibilityKind.Public }
            };
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
        /// Types a feature by a definition, as <c>camera : Camera</c> does: a <c>FeatureTyping</c> owned by the feature, or,
        /// for <c>port cmd : ~CommandPort</c>, a <c>ConjugatedPortTyping</c> by the conjugate of the port definition.
        /// </summary>
        /// <param name="feature">The typed feature.</param>
        /// <param name="definition">The definition.</param>
        /// <param name="isConjugated">Whether the feature is a port typed by the conjugate of its port definition.</param>
        private void AddTyping(IElement feature, IElement definition, bool isConjugated = false)
        {
            if (isConjugated)
            {
                var conjugatedPortDefinition = this.GetOrCreateConjugatedPortDefinition(definition);
                this.AddOwnedRelationship(feature, new ConjugatedPortTyping { TypedFeature = feature.Id, ConjugatedPortDefinition = conjugatedPortDefinition.Id });

                return;
            }

            this.AddOwnedRelationship(feature, new FeatureTyping { TypedFeature = feature.Id, Type = definition.Id });
        }

        /// <summary>
        /// Adds a relationship to the working copy, owned by an element and owning another element when one is given. Both
        /// sides of each link are set, since a DTO only holds identifiers, and the modified elements are recorded.
        /// </summary>
        /// <param name="owner">The element that owns the relationship.</param>
        /// <param name="relationship">The new relationship.</param>
        /// <param name="ownedRelatedElement">The element owned by the relationship, or <c>null</c> when it owns none.</param>
        /// <exception cref="InvalidChangeException">Thrown when the owner is an element of the standard library.</exception>
        private void AddOwnedRelationship(IElement owner, IRelationship relationship, IElement ownedRelatedElement = null)
        {
            // Marked first, so that an owner of the standard library is refused before anything is added.
            this.MarkModified(owner);
            this.Add(relationship);

            relationship.OwningRelatedElement = owner.Id;
            owner.OwnedRelationship.Add(relationship.Id);

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
            this.unassembledIds.Add(element.Id);

            return element;
        }

        /// <summary>
        /// Records that the DTO of an element has been modified, so that the change of the commit holds its new version and
        /// its POCO is brought up to date before it is read again.
        /// </summary>
        /// <param name="element">The modified element.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the element is an element of the standard library, which is read-only, for example the port definition
        /// of the library that a conjugated port would give a conjugate.
        /// </exception>
        private void MarkModified(IElement element)
        {
            if (this.libraryStandIns.ContainsKey(element.Id))
            {
                throw new InvalidChangeException($"The change would modify {this.standardLibrary.NamedElements[element.Id].qualifiedName}, an element of the standard library, which is read-only: use an element of the model instead, for example one that specializes it.");
            }

            this.modifiedIds.Add(element.Id);
            this.unassembledIds.Add(element.Id);
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

            // The POCOs are built again: brought up to date, SysML2.NET would drop the POCO of the removed relationship but keep
            // the ones of the elements that it owns, such as the literal of a removed value.
            this.assembler = null;
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
        /// declared without <c>ref</c>, except an event occurrence, such as a <c>PerformActionUsage</c>, which is always
        /// referential.
        /// </summary>
        /// <param name="type">The name of the metaclass, for example <c>PartUsage</c>.</param>
        /// <returns>The new element, which has no identifier yet.</returns>
        /// <exception cref="InvalidChangeException">Thrown when a change cannot create an element of this metaclass.</exception>
        private static IElement CreateElement(string type)
        {
            if (!CreatableMetaclasses.TryGetValue(type, out var metaclass))
            {
                throw new InvalidChangeException($"'{type}' is not the metaclass of a package, a definition, a usage or a connector, for example Package, PartDefinition, PartUsage, PortUsage, AttributeUsage, RequirementUsage or InterfaceUsage. The other relationships are built by the server.");
            }

            var element = (IElement)Activator.CreateInstance(metaclass);

            if (element is IOccurrenceUsage occurrenceUsage and not IEventOccurrenceUsage)
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
