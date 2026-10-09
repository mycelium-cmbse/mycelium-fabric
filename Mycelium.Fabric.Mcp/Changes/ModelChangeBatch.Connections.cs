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
    using System.Collections.Generic;
    using System.Linq;

    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Connections;
    using SysML2.NET.Core.DTO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.DTO.Systems.Interfaces;
    using SysML2.NET.Core.DTO.Systems.Ports;
    using SysML2.NET.Core.Root.Namespaces;

    /// <content>
    /// The ports and connectors: the direction of a feature (<c>out item image</c>), the conjugated typing of a port
    /// (<c>port cmd : ~CommandPort</c>), the ends of a connection or interface definition (<c>end port supplier</c>), and the
    /// creation of a connector between two features, which builds, as the textual notation does,
    /// <c>interface connect payloadSubsystem.camera.dataOut to dataHandlingSubsystem.obc.dataIn;</c>.
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The number of ends of a connector.
        /// </summary>
        private const int ConnectorEndCount = 2;

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
                    : $"Only a connector (ConnectionUsage, InterfaceUsage or BindingConnectorAsUsage) has ends, not the {Describe(element)}.");
            }

            if (payload.Direction != null && element is not IFeature)
            {
                throw new InvalidChangeException($"Only a feature, for example a port or an attribute, has a direction, not the {Describe(element)}.");
            }

            if (payload.Conjugated != null && element is not IPortUsage)
            {
                throw new InvalidChangeException($"Only a port can be conjugated, not the {Describe(element)}.");
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

            feature.Direction = payload.Direction;

            if (payload.IsEnd == true && owner is IConnectionDefinition)
            {
                feature.IsEnd = true;
                feature.IsConstant = true;
            }
        }

        /// <summary>
        /// Checks that a definition has the kind that a feature expects: a port definition for a port, an interface definition
        /// for an interface, and a connection definition for a connection. A conjugated port definition is never a
        /// definition of its own: a port is typed by it through <c>conjugated</c>.
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

            var (hasExpectedKind, expectedKind) = feature switch
            {
                IPortUsage => (definition is IPortDefinition, "a port definition"),
                IInterfaceUsage => (definition is IInterfaceDefinition, "an interface definition"),
                IConnectionUsage => (definition is IConnectionDefinition, "a connection definition"),
                _ => (true, null)
            };

            CheckKind(definition, hasExpectedKind, DefinitionRole, expectedKind);
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
        /// that subsets the connected feature, or a chain of features for a path such as <c>camera.dataOut</c>.
        /// </summary>
        /// <param name="connector">The new connector, which has no identifier yet.</param>
        /// <param name="change">The change that creates it, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">Thrown when the change cannot be applied.</exception>
        private void CreateConnector(IElement connector, ModelChange change)
        {
            var payload = change.Payload;

            if (payload is not { Value: null, Unit: null, Constraint: null, SatisfiedRequirement: null, SatisfyingPart: null, Direction: null, Conjugated: null, IsEnd: null }
                || !string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException("A connector takes two ends, and optionally an owner, a name, a definition and a text.");
            }

            if (payload.Ends is not { Count: ConnectorEndCount })
            {
                throw new InvalidChangeException("A connector connects two ends: give them as paths of names from its owner, for example ['camera.dataOut', 'obc.dataIn'].");
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

            if (connector is IInterfaceUsage)
            {
                for (var endIndex = 0; endIndex < ConnectorEndCount; endIndex++)
                {
                    var connectedFeature = ends[endIndex][^1];
                    CheckKind(connectedFeature, connectedFeature is IPortUsage, $"end '{payload.Ends[endIndex]}' of an interface", "a port");
                }
            }

            this.AddOwnedMember(owner, connector, change.Identity);

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddDocumentation(connector, payload.Text);
            }

            if (definition != null)
            {
                this.AddTyping(connector, definition);
            }

            foreach (var end in ends)
            {
                this.AddEnd(connector, end);
            }
        }

        /// <summary>
        /// Gets the features of a path of names from the owner of a connector, for example <c>camera</c> then <c>dataOut</c>
        /// for <c>camera.dataOut</c>. Each name is looked for among the features of the previous element and of its
        /// definitions.
        /// </summary>
        /// <param name="owner">The owner of the connector, where the path starts.</param>
        /// <param name="path">The path.</param>
        /// <returns>The features of the path, in order.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the path is empty or a name designates no feature.</exception>
        private List<IElement> ResolveEnd(IElement owner, string path)
        {
            CheckPath(path, "An end of the connector is empty.");

            var features = new List<IElement>();
            var context = owner;

            foreach (var name in path.Split(PathSeparator).Select(name => name.Trim()))
            {
                var feature = this.GetFeatures(context).FirstOrDefault(candidate => candidate.DeclaredName == name && !this.IsMarkedForDeletion(candidate))
                    ?? throw new InvalidChangeException($"The end '{path}' is not valid: the {Describe(context)} and its definitions have no feature named '{name}'. Use list_children to read their features.");

                features.Add(feature);
                context = feature;
            }

            return features;
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
            var end = this.Add(new ReferenceUsage { IsEnd = true, IsConstant = connector is not IBindingConnectorAsUsage });
            this.AddOwnedRelationship(connector, new EndFeatureMembership { Visibility = VisibilityKind.Public }, end);

            if (features.Count == 1)
            {
                this.AddOwnedRelationship(end, new ReferenceSubsetting { ReferencedFeature = features[0].Id });
                return;
            }

            var chain = this.Add(new Feature());
            this.AddOwnedRelationship(end, new ReferenceSubsetting { ReferencedFeature = chain.Id }, chain);

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
