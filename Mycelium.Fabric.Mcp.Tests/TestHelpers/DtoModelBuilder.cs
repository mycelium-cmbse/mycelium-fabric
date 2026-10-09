// ------------------------------------------------------------------------------------------------
//  <copyright file="DtoModelBuilder.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Constraints;
    using SysML2.NET.Core.DTO.Systems.Requirements;
    using SysML2.NET.Core.Systems.Requirements;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoRelationship = SysML2.NET.Core.DTO.Root.Elements.IRelationship;
    using PocoElement = SysML2.NET.Core.POCO.Root.Elements.IElement;

    /// <summary>
    /// Builds a model as DTOs, linked by identifiers as in a JSON file of the Systems Modeling API, and turns it into the POCOs
    /// that the tools read. A test can so build an incomplete model, which the POCOs of SysML2.NET cannot represent.
    /// </summary>
    internal sealed class DtoModelBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DtoModelBuilder"/> class, with an empty root namespace.
        /// </summary>
        public DtoModelBuilder()
        {
            this.RootNamespace = this.Add(new Namespace());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DtoModelBuilder"/> class on the given DTOs.
        /// </summary>
        /// <param name="dtos">The DTOs of the model, whose first root namespace becomes the <see cref="RootNamespace"/>.</param>
        private DtoModelBuilder(List<DtoElement> dtos)
        {
            this.Dtos = dtos;
            this.RootNamespace = dtos.OfType<Namespace>().First(element => element.OwningRelationship == null);
        }

        /// <summary>
        /// Gets the DTOs of the model.
        /// </summary>
        public List<DtoElement> Dtos { get; } = [];

        /// <summary>
        /// Gets the root namespace of the model.
        /// </summary>
        public Namespace RootNamespace { get; }

        /// <summary>
        /// Creates a builder on the DTOs of <c>Data/Satellite.json</c>.
        /// </summary>
        /// <returns>The <see cref="DtoModelBuilder"/> that holds the EOSat-1 model.</returns>
        public static DtoModelBuilder FromSatellite()
        {
            using var stream = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json"));

            var dtos = new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();

            return new DtoModelBuilder(dtos);
        }

        /// <summary>
        /// Gets the element that has the given declared name.
        /// </summary>
        /// <param name="name">The declared name of the element.</param>
        /// <returns>The element.</returns>
        public DtoElement Get(string name)
        {
            return this.Dtos.Single(element => element.DeclaredName == name);
        }

        /// <summary>
        /// Gives a new identifier to an element and adds it to the model, without owner.
        /// </summary>
        /// <typeparam name="T">The type of the element.</typeparam>
        /// <param name="element">The new element.</param>
        /// <returns>The new element.</returns>
        public T Add<T>(T element) where T : DtoElement
        {
            element.Id = Guid.NewGuid();
            element.ElementId = element.Id.ToString();
            this.Dtos.Add(element);

            return element;
        }

        /// <summary>
        /// Adds an element to the model as an owned member of another one, through an <c>OwningMembership</c>, or through the
        /// given membership, for example a <c>FeatureMembership</c> for a feature of a type.
        /// </summary>
        /// <typeparam name="T">The type of the member.</typeparam>
        /// <param name="owner">The owner of the member.</param>
        /// <param name="member">The new member.</param>
        /// <param name="membership">The membership that owns the member, an <c>OwningMembership</c> by default.</param>
        /// <returns>The new member.</returns>
        public T AddMember<T>(DtoElement owner, T member, DtoRelationship membership = null) where T : DtoElement
        {
            this.AddRelationship(owner, membership ?? new OwningMembership(), this.Add(member));

            return member;
        }

        /// <summary>
        /// Adds a relationship to the model, owned by an element and owning another element when one is given.
        /// </summary>
        /// <typeparam name="T">The type of the relationship.</typeparam>
        /// <param name="owner">The element that owns the relationship.</param>
        /// <param name="relationship">The new relationship.</param>
        /// <param name="ownedRelatedElement">The element owned by the relationship, already in the model, or <c>null</c>.</param>
        /// <returns>The new relationship.</returns>
        public T AddRelationship<T>(DtoElement owner, T relationship, DtoElement ownedRelatedElement = null) where T : DtoRelationship
        {
            this.Add(relationship);

            relationship.OwningRelatedElement = owner.Id;
            owner.OwnedRelationship.Add(relationship.Id);

            if (ownedRelatedElement != null)
            {
                relationship.OwnedRelatedElement.Add(ownedRelatedElement.Id);
                ownedRelatedElement.OwningRelationship = relationship.Id;
            }

            return relationship;
        }

        /// <summary>
        /// Gives a requirement, or a requirement definition, a required constraint without expression.
        /// </summary>
        /// <param name="requirement">The requirement or the requirement definition.</param>
        /// <returns>The new constraint.</returns>
        public ConstraintUsage AddRequiredConstraint(DtoElement requirement)
        {
            return this.AddMember(requirement, new ConstraintUsage(), new RequirementConstraintMembership { Kind = RequirementConstraintKind.Requirement });
        }

        /// <summary>
        /// Adds a satisfy link that refers to a requirement, owned by the given namespace.
        /// </summary>
        /// <param name="owner">The owner of the satisfy link.</param>
        /// <param name="requirementId">The <c>Id</c> of the satisfied requirement.</param>
        /// <param name="isNegated">Whether the satisfy link is negated (<c>not satisfy</c>).</param>
        /// <returns>The new satisfy link.</returns>
        public SatisfyRequirementUsage AddSatisfy(DtoElement owner, Guid requirementId, bool isNegated = false)
        {
            var satisfy = this.AddMember(owner, new SatisfyRequirementUsage { IsNegated = isNegated });
            this.AddRelationship(satisfy, new ReferenceSubsetting { ReferencedFeature = requirementId });

            return satisfy;
        }

        /// <summary>
        /// Removes an element from the model, without the relationships that refer to it.
        /// </summary>
        /// <param name="element">The element to remove.</param>
        public void Remove(DtoElement element)
        {
            this.Dtos.Remove(element);
        }

        /// <summary>
        /// Turns the DTOs into POCOs, whose derived properties can then be read.
        /// </summary>
        /// <returns>The POCOs of the model.</returns>
        public List<PocoElement> Build()
        {
            var assembler = new Assembler();
            assembler.Synchronize(this.Dtos);

            return assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .ToList();
        }
    }
}
