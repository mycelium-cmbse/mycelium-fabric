// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeBatch.Dependencies.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Changes
{
    using System.Linq;

    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Root.Annotations;
    using SysML2.NET.Core.DTO.Root.Dependencies;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Metadata;

    using PocoElement = SysML2.NET.Core.POCO.Root.Elements.IElement;

    /// <content>
    /// The creation of a <c>Dependency</c> from client elements to supplier elements, which can be a refinement marked with the
    /// <c>Refinement</c> metadata of the standard library, as <c>#refinement dependency 'FLR-R001' to 'HLR-R001';</c> does: the
    /// dependency owns an <c>Annotation</c>, which owns a <c>MetadataUsage</c> typed by <c>ModelingMetadata::Refinement</c>.
    /// </content>
    internal sealed partial class ModelChangeBatch
    {
        /// <summary>
        /// The role, in the messages, of an element that depends on the suppliers of a dependency.
        /// </summary>
        private const string ClientRole = "client";

        /// <summary>
        /// The role, in the messages, of an element on which the clients of a dependency depend.
        /// </summary>
        private const string SupplierRole = "supplier";

        /// <summary>
        /// Refuses the properties that only apply to the creation of a dependency.
        /// </summary>
        /// <param name="payload">The payload of a change that does not create a dependency.</param>
        /// <exception cref="InvalidChangeException">Thrown when the payload gives clients, suppliers or isRefinement.</exception>
        private static void CheckDependencyProperties(ElementPayload payload)
        {
            if (payload.Clients != null || payload.Suppliers != null || payload.IsRefinement != null)
            {
                throw new InvalidChangeException("The clients, the suppliers and isRefinement only apply to the creation of a Dependency.");
            }
        }

        /// <summary>
        /// Creates a dependency from its clients to its suppliers, as a member of its owner or, without owner, of the owner of
        /// its first client. With <c>isRefinement</c>, the dependency is marked with the <c>Refinement</c> metadata of the
        /// standard library.
        /// </summary>
        /// <param name="element">The new dependency.</param>
        /// <param name="change">The change that creates the dependency, whose identity is an optional temporary name.</param>
        /// <exception cref="InvalidChangeException">
        /// Thrown when the payload gives a property that a dependency does not take, gives no client or no supplier, designates
        /// an element that does not exist, or asks for a refinement while the standard library is not loaded.
        /// </exception>
        private void CreateDependency(IElement element, ModelChange change)
        {
            var payload = change.Payload;
            var dependency = (Dependency)element;

            if (payload is not { Definition: null, Value: null, Unit: null, Constraint: null, Constraints: null, Multiplicity: null, Ends: null, Direction: null, Conjugated: null }
                || payload is not { IsEnd: null, Specializes: null, Performed: null, Trigger: null, Guard: null, Effect: null, Item: null, SatisfiedRequirement: null, SatisfyingPart: null }
                || payload is not { Redefines: null, IsDefault: null, IsVariation: null, Variant: null, StateAction: null } || !string.IsNullOrWhiteSpace(payload.ReqId))
            {
                throw new InvalidChangeException("A Dependency takes clients and suppliers, and optionally isRefinement, an owner, a name and a text.");
            }

            if (payload.Clients is not { Count: > 0 } || payload.Suppliers is not { Count: > 0 })
            {
                throw new InvalidChangeException("A Dependency needs at least one client and one supplier. For a refinement, the clients refine the suppliers: 'FLR-R001 refines HLR-R001' gives the clients ['FLR-R001'] and the suppliers ['HLR-R001'].");
            }

            var clients = payload.Clients.Select(client => this.Resolve(client, ClientRole)).ToList();
            var suppliers = payload.Suppliers.Select(supplier => this.Resolve(supplier, SupplierRole)).ToList();
            var refinement = payload.IsRefinement == true ? this.GetRefinementDefinition() : null;

            var owner = string.IsNullOrWhiteSpace(payload.Owner) ? this.GetOwner(clients[0]) ?? this.GetOrCreateRootNamespace() : this.Resolve(payload.Owner, OwnerRole);
            CheckKind(owner, owner is INamespace, OwnerRole, "a namespace, for example a package, a definition or a usage");

            if (!string.IsNullOrWhiteSpace(payload.Name))
            {
                this.CheckNameIsFree(owner, payload.Name);
                dependency.DeclaredName = payload.Name;
            }

            dependency.Client = [.. clients.Select(client => client.Id)];
            dependency.Supplier = [.. suppliers.Select(supplier => supplier.Id)];
            this.AddOwnedMember(owner, dependency, change.Identity);

            // A relationship is not a namespace: its body owns annotations, not memberships ({ doc /* ... */ }).
            if (refinement != null)
            {
                var metadata = this.Add(new MetadataUsage());
                this.AddOwnedRelationship(dependency, new Annotation { AnnotatedElement = dependency.Id }, metadata);
                this.AddOwnedRelationship(metadata, new FeatureTyping { TypedFeature = metadata.Id, Type = refinement.Id });
            }

            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                this.AddOwnedRelationship(dependency, new Annotation { AnnotatedElement = dependency.Id }, this.Add(new Documentation { Body = payload.Text }));
            }
        }

        /// <summary>
        /// Gets the <c>Refinement</c> metadata definition of the standard library.
        /// </summary>
        /// <returns>The metadata definition.</returns>
        /// <exception cref="InvalidChangeException">Thrown when the standard library is not loaded.</exception>
        private PocoElement GetRefinementDefinition()
        {
            return this.standardLibrary.Resolve(DependencyExtensions.RefinementName)
                ?? throw new InvalidChangeException($"A refinement is marked with {DependencyExtensions.RefinementName}, which the standard library defines, but the standard library could not be loaded: validate_model tells why. Create the Dependency without isRefinement.");
        }
    }
}
