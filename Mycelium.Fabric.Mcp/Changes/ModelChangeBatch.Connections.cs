// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Connections.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Kernel.Connectors;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Connections;
    using SysML2.NET.Core.DTO.Systems.Allocations;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Enumerations;
    using SysML2.NET.Core.DTO.Systems.Flows;
    using SysML2.NET.Core.DTO.Systems.Interfaces;
    using SysML2.NET.Core.DTO.Systems.Occurrences;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Ports;
    using SysML2.NET.Core.Root.Namespaces;

    /// <content>
    /// The ports and connectors: the direction of a feature (<c>out item image</c>), the conjugated typing of a port
    /// (<c>port cmd : ~CommandPort</c>), the ends of a connection or interface definition (<c>end port supplier</c>), and the
    /// creation of a connector between two features, which builds, as the textual notation does,
    /// <c>interface connect payloadSubsystem.camera.dataOut to dataHandlingSubsystem.obc.dataIn;</c>. The messages of the
    /// mistakes that an AI assistant often makes (a direction on a port definition, a part instead of a port, an end that
    /// is not a feature of the owner) say what to send instead.
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The number of ends of a connector.
        /// </summary>
        private const int ConnectorEndCount = 2;

        /// <summary>
        /// The number of features named in a message that lists the features that have a name elsewhere in the model.
        /// </summary>
        private const int NamedFeatureCount = 3;

        /// <summary>
        /// Checks the properties of a payload that only apply to features, ports and connectors, when an element that is not
        /// a connector is created.
        /// </summary>
        /// <param name="member">The created element, whose name is set.</param>
        /// <param name="owner">The owner of the created element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">Thrown when a property does not apply to the element.</exception>
        private static void CheckFeatureProperties(IElement member, IElement owner, ElementPayload payload)
        {
            CheckFeatureProperties(member, payload);

            if (payload.Conjugated == true && payload.Definition == null)
            {
                throw new InvalidChangeException("A conjugated port needs a definition: the port definition whose conjugate types it.");
            }

            if (payload.IsEnd != true)
            {
                return;
            }

            if (member is not IFeature || owner is not IConnectionDefinition)
            {
                throw new InvalidChangeException($"Only a feature of a connection or interface definition can be an end, not the {Describe(member)} of the {Describe(owner)}.");
            }

            if (owner is IInterfaceDefinition && member is not IPortUsage)
            {
                throw new InvalidChangeException($"The ends of an interface definition are ports, not the {Describe(member)}.");
            }
        }

        /// <summary>
        /// Checks the properties of a payload that only apply to features, ports and connectors, for an element that is
        /// created or updated.
        /// </summary>
        /// <param name="element">The created or updated element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">Thrown when a property does not apply to the element.</exception>
        private static void CheckFeatureProperties(IElement element, ElementPayload payload)
        {
            if (payload.Ends != null)
            {
                throw new InvalidChangeException(element is IConnectorAsUsage
                    ? "The ends of a connector cannot change: delete the connector and create it again."
                    : $"Only a connector (ConnectionUsage, InterfaceUsage, BindingConnectorAsUsage, AllocationUsage or SuccessionAsUsage) has ends, not the {Describe(element)}.");
            }

            if (payload.Direction != null && element is IPortDefinition)
            {
                throw new InvalidChangeException($"A port definition has no direction, so the {Describe(element)} has none: give the direction to what flows through the port, "
                    + "an ItemUsage or an AttributeUsage that the port definition owns (port def FuelPort { out item fuel : Fuel; }), and type the port of the other side with conjugated true.");
            }

            if (payload.Direction != null && element is not IFeature)
            {
                throw new InvalidChangeException($"Only a feature, for example a port or an attribute, has a direction, not the {Describe(element)}.");
            }

            if (payload.Conjugated != null && element is not IPortUsage)
            {
                throw new InvalidChangeException($"Only a port can be conjugated, not the {Describe(element)}. A port is a PortUsage, typed by a PortDefinition.");
            }

            if (payload.Multiplicity != null)
            {
                CheckMultiplicity(element, payload.Multiplicity);
            }
        }

        /// <summary>
        /// Sets the direction of a new feature, and makes it an end when the payload asks for it: an end of a connection or
        /// interface definition is constant, as the textual notation makes it.
        /// </summary>
        /// <param name="member">The new element, already checked by <see cref="CheckFeatureProperties(IElement, IElement, ElementPayload)"/>.</param>
        /// <param name="owner">The owner of the new element.</param>
        /// <param name="payload">The payload of the change.</param>
        private static void SetFeatureFlags(IElement member, IElement owner, ElementPayload payload)
        {
            if (member is not IFeature feature)
            {
                return;
            }

            SetDirection(feature, payload.Direction);

            if (payload.IsEnd == true && owner is IConnectionDefinition)
            {
                feature.IsEnd = true;
                feature.IsConstant = true;
            }
        }

        /// <summary>
        /// Sets the direction of a feature. A usage with a direction, such as a parameter of an action
        /// (<c>in currentStatus : VehicleStatus;</c>), is referential, as the textual notation makes it.
        /// </summary>
        /// <param name="feature">The feature.</param>
        /// <param name="direction">The direction, or <c>null</c> for none.</param>
        private static void SetDirection(IFeature feature, FeatureDirectionKind? direction)
        {
            feature.Direction = direction;

            if (direction != null && feature is IUsage)
            {
                feature.IsComposite = false;
            }
        }

        /// <summary>
        /// Checks that a definition has the kind that a feature expects: a port definition for a port, an interface definition
        /// for an interface, a connection definition for a connection, an enumeration definition for an enumeration usage,
        /// and a definition that is not a port definition for a part. A conjugated port definition is never a definition of
        /// its own: a port is typed by it through <c>conjugated</c>.
        /// </summary>
        /// <param name="feature">The typed feature.</param>
        /// <param name="definition">The definition, which is a classifier.</param>
        /// <exception cref="InvalidChangeException">Thrown when the definition does not have the expected kind.</exception>
        private static void CheckDefinitionKind(IElement feature, IElement definition)
        {
            if (definition is IConjugatedPortDefinition)
            {
                throw new InvalidChangeException("A conjugated port definition cannot be given as a definition: give its port definition with conjugated true.");
            }

            if (feature is IPartUsage && definition is IPortDefinition)
            {
                throw new InvalidChangeException($"A part is not typed by the {Describe(definition)}: create a PortUsage for a port.");
            }

            var (hasExpectedKind, expectedKind, expectedMetaclass) = feature switch
            {
                IPortUsage => (definition is IPortDefinition, "a port definition", nameof(PortDefinition)),
                IInterfaceUsage => (definition is IInterfaceDefinition, "an interface definition", nameof(InterfaceDefinition)),
                IConnectionUsage => (definition is IConnectionDefinition, "a connection definition", nameof(ConnectionDefinition)),
                IFlowUsage => (definition is IFlowDefinition, "a flow definition", nameof(FlowDefinition)),
                IEnumerationUsage => (definition is IEnumerationDefinition, "an enumeration definition", nameof(EnumerationDefinition)),
                _ => (true, null, null)
            };

            if (!hasExpectedKind)
            {
                throw new InvalidChangeException($"The definition must be {expectedKind}, not the {Describe(definition)}. "
                    + $"A change cannot change the metaclass of an element: if it is meant to be one, delete it and create it again with the type {expectedMetaclass}.");
            }
        }

        /// <summary>
        /// Checks the properties of a payload that only apply to features, ports and connectors, when an element is updated.
        /// </summary>
        /// <param name="element">The updated element.</param>
        /// <param name="payload">The payload of the change.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when a property does not apply to the element, or when a port to conjugate has no definition.
        /// </exception>
        private void CheckFeatureUpdate(IElement element, ElementPayload payload)
        {
            CheckFeatureProperties(element, payload);

            if (payload.IsEnd != null)
            {
                throw new InvalidChangeException("isEnd only applies to the creation of a feature: delete the feature and create it again.");
            }

            if (payload is { Conjugated: not null, Definition: null } && this.GetPortDefinition(element) == null)
            {
                throw new InvalidChangeException($"The {Describe(element)} has no port definition to conjugate: give its definition.");
            }
        }

        /// <summary>
        /// Creates a connector between two features: a <c>ConnectionUsage</c>, an <c>InterfaceUsage</c> between two ports, or
        /// a <c>BindingConnectorAsUsage</c>. Each end is a reference feature, owned through an <c>EndFeatureMembership</c>,
        /// that subsets the connected feature, or a chain of features for a path such as <c>camera.dataOut</c>. The ends of a
        /// <c>FlowUsage</c> are <c>FlowEnd</c>s, built by <see cref="AddFlowEnd"/>, and its item is a payload feature.
        /// </summary>
        /// <param name="connector">The new connector, which has no identifier yet.</param>
        /// <param name="change">The change that creates it, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreateConnector(IElement connector, ModelChange change)
        {
            var payload = change.Payload;

            if (payload is not { Value: null, Unit: null, Constraint: null, Constraints: null, Multiplicity: null, SatisfiedRequirement: null, SatisfyingPart: null, Direction: null, Conjugated: null }
                || payload is not { IsEnd: null, Specializes: null, Performed: null, Trigger: null, Guard: null, Effect: null } || !string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException(connector is IFlowUsage
                    ? "A FlowUsage takes two ends, and optionally an owner, a name, a definition, an item and a text."
                    : "A connector takes two ends, and optionally an owner, a name, a definition and a text.");
            }

            CheckBehaviourProperties(connector, payload);

            if (payload.Ends is not { Count: ConnectorEndCount })
            {
                throw new InvalidChangeException(connector switch
                {
                    ISuccessionAsUsage => "A succession orders two actions or other occurrences: give the one that comes first, then the one that follows, as paths of names from its owner "
                        + "or as identifiers, for example ['prepare', 'launch']. An action alone in its owner needs no succession.",
                    IInterfaceUsage => "An interface connects two ports: give them as paths of names from its owner or as identifiers, for example ['camera.dataOut', 'obc.dataIn'].",
                    _ => "A connector connects two ends: give them as paths of names from its owner, for example ['camera.dataOut', 'obc.dataIn']."
                });
            }

            var owner = string.IsNullOrWhiteSpace(payload.Owner) ? this.GetOrCreateRootNamespace() : this.Resolve(payload.Owner, OwnerRole);
            CheckKind(owner, owner is INamespace, OwnerRole, "a namespace, for example a part or a part definition");

            if (!string.IsNullOrWhiteSpace(payload.Name))
            {
                this.CheckNameIsFree(owner, payload.Name);
                connector.DeclaredName = payload.Name;
            }

            var definition = payload.Definition == null ? null : this.ResolveDefinition(connector, payload.Definition);
            var ends = payload.Ends.Select(path => this.ResolveEnd(owner, path)).ToList();

            if (ends[0].SequenceEqual(ends[1]))
            {
                throw new InvalidChangeException($"The two ends are the same feature '{payload.Ends[0]}'.");
            }

            var isSuccession = connector is ISuccessionAsUsage;

            if (isSuccession || connector is IAllocationUsage)
            {
                var (expectedKind, isExpectedKind) = isSuccession
                    ? ("an action or another occurrence, for example a state", (Func<IElement, bool>)(feature => feature is IOccurrenceUsage))
                    : ("a usage", feature => feature is IUsage);

                for (var endIndex = 0; endIndex < ConnectorEndCount; endIndex++)
                {
                    var connectedFeature = ends[endIndex][^1];
                    CheckKind(connectedFeature, isExpectedKind(connectedFeature), $"end '{payload.Ends[endIndex]}' of a {connector.GetType().Name}", expectedKind);
                }
            }

            if (connector is IInterfaceUsage)
            {
                for (var endIndex = 0; endIndex < ConnectorEndCount; endIndex++)
                {
                    this.CheckInterfaceEnd(payload.Ends[endIndex], ends[endIndex][^1]);
                }
            }

            var isFlow = connector is IFlowUsage;
            var item = isFlow ? this.CheckFlow(payload, ends) : null;

            this.AddOwnedMember(owner, connector, change.Identity);

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(connector, payload.Text);
            }

            if (definition != null)
            {
                this.AddTyping(connector, definition);
            }

            if (item != null)
            {
                this.AddFlowItem(connector, item);
            }

            foreach (var end in ends)
            {
                if (isFlow)
                {
                    this.AddFlowEnd(connector, end);
                }
                else
                {
                    this.AddEnd(connector, end);
                }
            }
        }

        /// <summary>
        /// Gets the features of a path of names from the owner of a connector, for example <c>camera</c> then <c>dataOut</c>
        /// for <c>camera.dataOut</c>. Each name is looked for among the features of the previous element and of its
        /// definitions. The first name, when the owner has no feature with this name, can also be the identifier, the
        /// temporary name or the qualified name of a feature elsewhere in the model, which has a single meaning.
        /// </summary>
        /// <param name="owner">The owner of the connector, where the path starts.</param>
        /// <param name="path">The path.</param>
        /// <returns>The features of the path, in order.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the path is empty or a name designates no feature.</exception>
        private List<IElement> ResolveEnd(IElement owner, string path)
        {
            CheckPath(path, "An end of the connector is empty.");

            var names = path.Split(PathSeparator).Select(name => name.Trim()).ToList();
            var features = new List<IElement> { this.FindFeature(owner, names[0]) ?? this.ResolveEndStart(owner, path, names[0]) };

            foreach (var name in names.Skip(1))
            {
                features.Add(this.FindFeature(features[^1], name) ?? throw new InvalidChangeException(
                    $"The end '{path}' is not valid: the {Describe(features[^1])} and its definitions have no feature named '{name}'. Use list_children to read their features."));
            }

            return features;
        }

        /// <summary>
        /// Gets the feature at the start of the path of an end when the owner of the connector has no feature with this name:
        /// the feature designated by an identifier, a temporary name or a qualified name.
        /// </summary>
        /// <param name="owner">The owner of the connector.</param>
        /// <param name="path">The path of the end, for the messages.</param>
        /// <param name="name">The first name of the path.</param>
        /// <returns>The designated feature.</returns>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the name designates no feature: the message then names the features that have this name elsewhere in
        /// the model, with their identifier and qualified name.
        /// </exception>
        private IElement ResolveEndStart(IElement owner, string path, string name)
        {
            var isReference = Guid.TryParse(name, out _) || this.temporaryNames.ContainsKey(name) || this.earlierTemporaryNames.ContainsKey(name) || this.failedTemporaryNames.Contains(name);
            var role = $"end '{path}'";

            var element = isReference ? this.Resolve(name, "end") : this.FindFeatureByQualifiedName(name, role);

            if (element == null)
            {
                throw new InvalidChangeException($"The end '{path}' is not valid: the {Describe(owner)} and its definitions have no feature named '{name}'.{this.DescribeFeaturesNamed(name)} "
                    + "Use list_children to read their features.");
            }

            CheckKind(element, element is IFeature, role, "a feature, for example a part, a port or an action");

            return element;
        }

        /// <summary>
        /// Finds the feature that has a qualified name, such as <c>Mission::Operations::launch</c>, with or without the quotes
        /// of the names that are not identifiers (<c>'Apollo 11'::Operations::launch</c>).
        /// </summary>
        /// <param name="name">The qualified name, or a single name.</param>
        /// <param name="role">The role of the feature in the change, for example <c>end 'x'</c>, used in the messages.</param>
        /// <returns>The feature, or <c>null</c> when the name is not qualified.</returns>
        /// <exception cref="InvalidChangeException">Thrown when no feature, or several, have this qualified name.</exception>
        private IElement FindFeatureByQualifiedName(string name, string role)
        {
            if (!name.Contains(QualifiedNameSeparator, StringComparison.Ordinal))
            {
                return null;
            }

            var qualifiedName = NormalizeQualifiedName(name);
            var shortName = qualifiedName[(qualifiedName.LastIndexOf(QualifiedNameSeparator, StringComparison.Ordinal) + QualifiedNameSeparator.Length)..];

            var candidates = this.elementsById.Values
                .OfType<IFeature>()
                .Where(candidate => candidate.DeclaredName == shortName && !this.IsMarkedForDeletion(candidate));

            var features = this.AssembleElements(candidates)
                .Where(candidate => candidate.qualifiedName != null && NormalizeQualifiedName(candidate.qualifiedName) == qualifiedName)
                .ToList();

            return features.Count switch
            {
                1 => this.elementsById[features[0].Id],
                0 => throw new InvalidChangeException($"The {role} is not valid: no feature has the qualified name '{name}'. Use find_elements_by_name to read the qualified names."),
                _ => throw new InvalidChangeException($"The {role} is not valid: several features have the qualified name '{name}'. Give the identifier of one of them.")
            };
        }

        /// <summary>
        /// Writes a qualified name without the quotes of its names and the spaces around them, to compare it with another.
        /// </summary>
        /// <param name="qualifiedName">The qualified name.</param>
        /// <returns>The qualified name, for example <c>Apollo 11::Operations::launch</c>.</returns>
        private static string NormalizeQualifiedName(string qualifiedName)
        {
            var names = qualifiedName.Split(QualifiedNameSeparator).Select(name => name.Trim().Trim('\''));

            return string.Join(QualifiedNameSeparator, names);
        }

        /// <summary>
        /// Describes, for a message, the features that have a name elsewhere in the model, so that the AI assistant can give
        /// one of them by its identifier or its qualified name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The sentence that names the first features, or an empty text when no feature has this name.</returns>
        private string DescribeFeaturesNamed(string name)
        {
            var candidates = this.elementsById.Values
                .OfType<IFeature>()
                .Where(candidate => candidate.DeclaredName == name && !this.IsMarkedForDeletion(candidate));

            var features = this.AssembleElements(candidates)
                .Where(feature => feature.qualifiedName != null)
                .OrderBy(feature => feature.qualifiedName, StringComparer.Ordinal)
                .Take(NamedFeatureCount)
                .Select(feature => $"the {feature.GetType().Name} '{feature.qualifiedName}' (Id {feature.Id})")
                .ToList();

            return features.Count == 0
                ? string.Empty
                : $" The model has {string.Join(", ", features)}: give its identifier or its qualified name as the end, or a path from the owner to a feature that holds it.";
        }

        /// <summary>
        /// Checks that the end of an interface is a port. When it is a part, the message names its ports, one of which is
        /// the expected end.
        /// </summary>
        /// <param name="path">The path of the end.</param>
        /// <param name="connectedFeature">The feature that the end connects.</param>
        /// <exception cref="InvalidChangeException">Thrown when the end is not a port.</exception>
        private void CheckInterfaceEnd(string path, IElement connectedFeature)
        {
            if (connectedFeature is IPortUsage)
            {
                return;
            }

            var ports = this.GetFeatures(connectedFeature)
                .OfType<IPortUsage>()
                .Where(port => port.DeclaredName != null && !this.IsMarkedForDeletion(port))
                .Select(port => $"'{path}.{port.DeclaredName}'")
                .ToList();

            var hint = ports.Count == 0
                ? " It has no port: create a PortUsage in it or in its definition first."
                : $" Give one of its ports: {string.Join(", ", ports)}.";

            throw new InvalidChangeException($"The end '{path}' of an interface must be a port, not the {Describe(connectedFeature)}.{(connectedFeature is IUsage ? hint : string.Empty)}");
        }

        /// <summary>
        /// Adds an end to a connector: a reference feature, owned through an <c>EndFeatureMembership</c>, whose
        /// <c>ReferenceSubsetting</c> designates the connected feature, or owns a feature chained through the features of a
        /// path. The ends of a connection or an interface are constant, as the textual notation makes them; the ends of a
        /// binding are not.
        /// </summary>
        /// <param name="connector">The connector.</param>
        /// <param name="features">The features of the path of the end, the connected feature being the last one.</param>
        private void AddEnd(IElement connector, List<IElement> features)
        {
            var end = this.Add(new ReferenceUsage { IsEnd = true, IsConstant = connector is not (IBindingConnectorAsUsage or ISuccession) });
            this.AddOwnedRelationship(connector, new EndFeatureMembership { Visibility = VisibilityKind.Public }, end);
            this.AddReferenceSubsetting(end, features);
        }

        /// <summary>
        /// Makes a feature reference the feature of a path, as an end of a connector does: a <c>ReferenceSubsetting</c> that
        /// designates the feature, or that owns a feature chained through the features of a path such as
        /// <c>camera.dataOut</c>.
        /// </summary>
        /// <param name="feature">The referencing feature.</param>
        /// <param name="features">The features of the path, the referenced feature being the last one.</param>
        private void AddReferenceSubsetting(IElement feature, List<IElement> features)
        {
            if (features.Count == 1)
            {
                this.AddOwnedRelationship(feature, new ReferenceSubsetting { ReferencedFeature = features[0].Id });
                return;
            }

            var chain = this.Add(new Feature());
            this.AddOwnedRelationship(feature, new ReferenceSubsetting { ReferencedFeature = chain.Id }, chain);
            this.AddChainings(chain, features);
        }

        /// <summary>
        /// Chains a feature through the features of a path, as <c>camera.dataOut</c> does: a <c>FeatureChaining</c> owned by
        /// the chain for each feature, in order.
        /// </summary>
        /// <param name="chain">The chained feature.</param>
        /// <param name="features">The features of the path.</param>
        private void AddChainings(IElement chain, IEnumerable<IElement> features)
        {
            foreach (var feature in features)
            {
                this.AddOwnedRelationship(chain, new FeatureChaining { ChainingFeature = feature.Id });
            }
        }

        /// <summary>
        /// Gets the conjugate of a port definition, which types the ports of <c>port cmd : ~CommandPort</c>, and creates it
        /// when the port definition has none, as the textual notation does for each port definition: a
        /// <c>ConjugatedPortDefinition</c> owned by the port definition, whose <c>PortConjugation</c> designates it.
        /// </summary>
        /// <param name="portDefinition">The port definition.</param>
        /// <returns>The conjugated port definition.</returns>
        private IElement GetOrCreateConjugatedPortDefinition(IElement portDefinition)
        {
            var conjugatedPortDefinition = this.GetOwnedMembers(portDefinition).OfType<IConjugatedPortDefinition>().FirstOrDefault();

            if (conjugatedPortDefinition != null)
            {
                return conjugatedPortDefinition;
            }

            var newConjugatedPortDefinition = this.Add(new ConjugatedPortDefinition());
            this.AddOwnedRelationship(portDefinition, new OwningMembership { Visibility = VisibilityKind.Public }, newConjugatedPortDefinition);
            this.AddOwnedRelationship(newConjugatedPortDefinition, new PortConjugation { ConjugatedType = newConjugatedPortDefinition.Id, OriginalPortDefinition = portDefinition.Id });

            return newConjugatedPortDefinition;
        }

        /// <summary>
        /// Gets the port definition of a port: the definition of its typing, or the original port definition of its
        /// conjugated typing.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <returns>The port definition, or <c>null</c> when the element is not typed by a port definition.</returns>
        private IElement GetPortDefinition(IElement port)
        {
            var typings = this.GetElements(port.OwnedRelationship).OfType<IFeatureTyping>().ToList();

            var conjugatedPortDefinitions = this.GetElements(typings.OfType<IConjugatedPortTyping>().Select(typing => typing.ConjugatedPortDefinition))
                .SelectMany(conjugatedPortDefinition => this.GetElements(conjugatedPortDefinition.OwnedRelationship))
                .OfType<IPortConjugation>()
                .Select(conjugation => conjugation.OriginalPortDefinition);

            var definitions = typings.Where(typing => typing is not IConjugatedPortTyping).Select(typing => typing.Type);

            return this.GetElements(definitions.Concat(conjugatedPortDefinitions)).FirstOrDefault(definition => definition is IPortDefinition);
        }

        /// <summary>
        /// Gets the features of an element: the features it owns and the features owned by its definitions.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The features of the element, then the ones of its definitions.</returns>
        private IEnumerable<IFeature> GetFeatures(IElement element)
        {
            var definitions = this.GetElements(element.OwnedRelationship)
                .OfType<IFeatureTyping>()
                .Where(typing => typing is not IConjugatedPortTyping)
                .Select(typing => this.elementsById.GetValueOrDefault(typing.Type))
                .Where(definition => definition != null);

            return definitions
                .Prepend(element)
                .SelectMany(this.GetOwnedMembers)
                .OfType<IFeature>();
        }
    }
}
