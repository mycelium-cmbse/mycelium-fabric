// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelChangeApplierTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Changes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoRelationship = SysML2.NET.Core.DTO.Root.Elements.IRelationship;

    /// <summary>
    /// Suite of tests for the <see cref="ModelChangeApplier"/> class.
    /// </summary>
    [TestFixture]
    public class ModelChangeApplierTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the root namespace in <c>Satellite.json</c>, which has no owner.
        /// </summary>
        private static readonly Guid RootNamespaceId = Guid.Parse("f6fabf5a-0c31-3fff-6155-66366c69c6a0");

        /// <summary>
        /// The <c>Id</c> of the <c>EOSat1</c> package in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PackageId = Guid.Parse("9d4b1232-b67b-5023-5163-7bdc6de8ff50");

        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        private ModelChangeApplier applier;

        [SetUp]
        public void SetUp()
        {
            this.applier = new ModelChangeApplier();
        }

        [Test]
        public void VerifyApply()
        {
            var dtos = ReadSatelliteDtos();
            var camera = dtos.Single(dto => dto.Id == CameraId);
            var cameraRelationshipCount = camera.OwnedRelationship.Count;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.applier.Apply(null, []), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.applier.Apply(dtos, null), Throws.TypeOf<ArgumentNullException>());
            }

            var pendingCommit = this.applier.Apply(dtos,
            [
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Name = "mainCamera" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = CameraId.ToString(), Name = "lens" } },
                new ModelChange { Identity = RootNamespaceId.ToString(), Payload = new ElementPayload { Name = "Root" } }
            ]);

            var cameraVersion = pendingCommit.CommitRequest.Change.Single(dataVersion => dataVersion.Identity.Id == CameraId);
            var creations = pendingCommit.CommitRequest.Change.Where(dataVersion => dtos.TrueForAll(dto => dto.Id != dataVersion.Identity.Id)).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(pendingCommit.Problems, Has.Count.EqualTo(0));
                Assert.That(pendingCommit.CommitRequest.Change, Has.Count.EqualTo(4));
                Assert.That(((DtoElement)cameraVersion.Payload).DeclaredName, Is.EqualTo("mainCamera"));
                Assert.That(cameraVersion.Payload, Is.Not.SameAs(camera));
                Assert.That(camera.DeclaredName, Is.EqualTo("camera"));
                Assert.That(camera.OwnedRelationship, Has.Count.EqualTo(cameraRelationshipCount));
                Assert.That(creations.Select(dataVersion => dataVersion.Payload.GetType().Name), Is.EquivalentTo(["FeatureMembership", "PartUsage"]));
                Assert.That(creations.Select(dataVersion => ((DtoElement)dataVersion.Payload).Id), Is.EqualTo(creations.Select(dataVersion => dataVersion.Identity.Id)));
                Assert.That(pendingCommit.CreatedElements.Select(created => created.Name), Is.EqualTo(["lens"]));
            }
        }

        [Test]
        public void Apply_WithCreations_BuildsTheElementsAndTheirRelationships()
        {
            var pendingCommit = this.applier.Apply([],
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "requirements", Payload = new ElementPayload { Type = "Package", Owner = "spacecraft", Name = "Requirements" } },
                new ModelChange { Identity = "Camera", Payload = new ElementPayload { Type = "PartDefinition", Owner = "spacecraft", Name = "Camera", Text = "Optical camera." } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Camera", Name = "mass", Value = 38, Text = "Dry mass of the unit [kg]." } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "partUsage", Owner = "spacecraft", Name = "camera", Definition = "Camera" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "camera", Name = "lens" } },
                new ModelChange { Payload = new ElementPayload { Type = "RequirementUsage", Owner = "requirements", Name = "cameraMass", Text = "The camera shall weigh less than 40 kg." } }
            ]);

            var elements = Assemble(ApplyChange([], pendingCommit));
            var createdIds = pendingCommit.CreatedElements.ToDictionary(created => created.Name, created => created.Id);
            var package = elements[createdIds["Spacecraft"]];
            var definition = (IType)elements[createdIds["Camera"]];
            var mass = (IAttributeUsage)elements[createdIds["mass"]];
            var camera = (IPartUsage)elements[createdIds["camera"]];
            var lens = elements[createdIds["lens"]];
            var requirement = elements[createdIds["cameraMass"]];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(pendingCommit.Problems, Has.Count.EqualTo(0));
                Assert.That(pendingCommit.CommitRequest.Change, Has.Count.EqualTo(elements.Count));
                Assert.That(pendingCommit.CommitRequest.Change.Select(dataVersion => ((DtoElement)dataVersion.Payload).Id), Is.EqualTo(pendingCommit.CommitRequest.Change.Select(dataVersion => dataVersion.Identity.Id)));
                Assert.That(pendingCommit.CreatedElements.Select(created => created.Type),
                    Is.EqualTo(["Package", "Package", "PartDefinition", "AttributeUsage", "PartUsage", "PartUsage", "RequirementUsage"]));
                Assert.That(pendingCommit.CreatedElements[0].TemporaryName, Is.EqualTo("spacecraft"));
                Assert.That(pendingCommit.CreatedElements[3].TemporaryName, Is.Null);
                Assert.That(package.owner, Is.TypeOf<Namespace>());
                Assert.That(package.owner.owner, Is.Null);
                Assert.That(package.qualifiedName, Is.EqualTo("Spacecraft"));
                Assert.That(definition.GetDocumentationBodies(), Is.EqualTo("Optical camera."));
                Assert.That(definition.feature, Is.EqualTo([mass]));
                Assert.That(mass.GetNumericValue(), Is.EqualTo(38));
                Assert.That(mass.GetDocumentationBodies(), Is.EqualTo("Dry mass of the unit [kg]."));
                Assert.That(mass.IsComposite, Is.False);
                Assert.That(camera.qualifiedName, Is.EqualTo("Spacecraft::camera"));
                Assert.That(camera.GetTypeNames(), Is.EqualTo(["Camera"]));
                Assert.That(camera.IsComposite, Is.True);
                Assert.That(camera.OwningRelationship, Is.Not.InstanceOf<IFeatureMembership>());
                Assert.That(((IMembership)camera.OwningRelationship).Visibility, Is.EqualTo(VisibilityKind.Public));
                Assert.That(lens.OwningRelationship, Is.InstanceOf<IFeatureMembership>());
                Assert.That(lens.GetTypeNames(), Has.Count.EqualTo(0));
                Assert.That(requirement.qualifiedName, Is.EqualTo("Spacecraft::Requirements::cameraMass"));
                Assert.That(requirement.GetDocumentationBodies(), Is.EqualTo("The camera shall weigh less than 40 kg."));
            }
        }

        [Test]
        public void Apply_WithUpdates_ReplacesTheChangedProperties()
        {
            var creation = this.applier.Apply([],
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "Camera", Payload = new ElementPayload { Type = "PartDefinition", Owner = "spacecraft", Name = "Camera" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Camera", Name = "mass", Value = 38, Text = "Mass [kg]." } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "camera", Definition = "Camera" } }
            ]);

            var model = ApplyChange([], creation);
            var createdIds = creation.CreatedElements.ToDictionary(created => created.Name, created => created.Id.ToString());

            var pendingCommit = this.applier.Apply(model,
            [
                new ModelChange { Identity = "SmallCamera", Payload = new ElementPayload { Type = "PartDefinition", Owner = createdIds["Spacecraft"], Name = "SmallCamera" } },
                new ModelChange { Identity = createdIds["camera"], Payload = new ElementPayload { Name = "mainCamera", Definition = "SmallCamera" } },
                new ModelChange { Identity = createdIds["mass"], Payload = new ElementPayload { Value = 35.5, Text = "Dry mass [kg]." } },
                new ModelChange { Identity = createdIds["Camera"], Payload = new ElementPayload { Name = "Camera" } }
            ]);

            var originalIds = model.Select(dto => dto.Id).ToHashSet();
            var updatedIds = pendingCommit.CommitRequest.Change.Where(dataVersion => dataVersion.Payload != null && originalIds.Contains(dataVersion.Identity.Id)).Select(dataVersion => dataVersion.Identity.Id.ToString());
            var deletions = pendingCommit.CommitRequest.Change.Where(dataVersion => dataVersion.Payload == null).ToList();

            var elements = Assemble(ApplyChange(model, pendingCommit));
            var camera = elements[Guid.Parse(createdIds["camera"])];
            var mass = (IAttributeUsage)elements[Guid.Parse(createdIds["mass"])];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(pendingCommit.Problems, Has.Count.EqualTo(0));
                Assert.That(pendingCommit.CreatedElements.Select(created => created.Name), Is.EqualTo(["SmallCamera"]));
                Assert.That(updatedIds, Is.EquivalentTo([createdIds["Spacecraft"], createdIds["camera"], createdIds["mass"], createdIds["Camera"]]));
                Assert.That(deletions, Has.Count.EqualTo(5));
                Assert.That(deletions.TrueForAll(dataVersion => originalIds.Contains(dataVersion.Identity.Id)), Is.True);
                Assert.That(camera.DeclaredName, Is.EqualTo("mainCamera"));
                Assert.That(camera.GetTypeNames(), Is.EqualTo(["SmallCamera"]));
                Assert.That(mass.GetNumericValue(), Is.EqualTo(35.5));
                Assert.That(mass.GetDocumentationBodies(), Is.EqualTo("Dry mass [kg]."));
                Assert.That(elements, Has.Count.EqualTo(model.Count + 2));
            }
        }

        [Test]
        public void Apply_WithDeletions_RemovesTheElementsUnlessTheyAreStillReferenced()
        {
            var dtos = ReadSatelliteDtos();
            var opticalCameraId = dtos.Single(dto => dto.DeclaredName == "OpticalCamera").Id.ToString();

            var refusal = this.applier.Apply(dtos, [new ModelChange { Identity = opticalCameraId }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refusal.CommitRequest, Is.Null);
                Assert.That(refusal.CreatedElements, Has.Count.EqualTo(0));
                Assert.That(refusal.Problems, Has.Count.EqualTo(1));
                Assert.That(refusal.Problems[0], Does.StartWith("Change 1 (delete): the PartDefinition 'OpticalCamera' is still referenced by"));
                Assert.That(refusal.Problems[0], Does.Contain("'EOSat1::Architecture::eosat1::payloadSubsystem::camera'"));
            }

            var pendingCommit = this.applier.Apply(dtos,
            [
                new ModelChange { Identity = CameraId.ToString() },
                new ModelChange { Identity = opticalCameraId },
                new ModelChange { Identity = "newCamera", Payload = new ElementPayload { Type = "PartUsage", Owner = PayloadSubsystemId.ToString(), Name = "camera" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "newCamera", Name = "lens" } },
                new ModelChange { Identity = "newCamera" }
            ]);

            var elements = Assemble(ApplyChange(dtos, pendingCommit));
            var payloadParts = elements[PayloadSubsystemId].ownedElement.OfType<IPartUsage>().Select(part => part.DeclaredName);
            var deletedIds = pendingCommit.CommitRequest.Change.Where(dataVersion => dataVersion.Payload == null).Select(dataVersion => dataVersion.Identity.Id).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(pendingCommit.Problems, Has.Count.EqualTo(0));
                Assert.That(pendingCommit.CreatedElements, Has.Count.EqualTo(0));
                Assert.That(deletedIds, Does.Contain(CameraId));
                Assert.That(deletedIds, Does.Contain(Guid.Parse(opticalCameraId)));
                Assert.That(deletedIds, Has.Count.EqualTo(dtos.Count - elements.Count));
                Assert.That(elements, Does.Not.ContainKey(CameraId));
                Assert.That(elements, Does.Not.ContainKey(Guid.Parse(opticalCameraId)));
                Assert.That(payloadParts, Is.EquivalentTo(["massMemory"]));
                Assert.That(elements, Has.Count.EqualTo(529));
            }
        }

        [Test]
        public void Apply_WithInvalidChanges_ReturnsEveryProblem()
        {
            var dtos = ReadSatelliteDtos();
            var opticalCameraId = dtos.Single(dto => dto.DeclaredName == "OpticalCamera").Id.ToString();
            var relationshipId = dtos.OfType<DtoRelationship>().First().Id.ToString();
            var documentationId = dtos.First(dto => dto.GetType().Name == "Documentation").Id;

            var result = this.applier.Apply(dtos,
            [
                null,
                new ModelChange { Payload = new ElementPayload { Type = "Package", Name = " " } },
                new ModelChange { Payload = new ElementPayload { Type = "Lens", Name = "lens" } },
                new ModelChange { Payload = new ElementPayload { Type = "FeatureTyping", Name = "typing" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartDefinition", Owner = CameraId.ToString().Replace('5', '6'), Name = "Lens" } },
                new ModelChange { Identity = "lens", Payload = new ElementPayload { Type = "PartUsage", Owner = "optics", Name = "lens" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "lens", Name = "mass" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = PayloadSubsystemId.ToString(), Name = "camera" } },
                new ModelChange { Identity = PackageId.ToString(), Payload = new ElementPayload { Type = "Package", Name = "Optics" } },
                new ModelChange { Identity = "optics", Payload = new ElementPayload { Type = "Package", Name = "Optics" } },
                new ModelChange { Identity = "optics", Payload = new ElementPayload { Type = "Package", Name = "Optics2" } },
                new ModelChange { Payload = new ElementPayload { Type = "RequirementUsage", Owner = PackageId.ToString(), Name = "cameraMass" } },
                new ModelChange { Payload = new ElementPayload { Type = "Package", Owner = "optics", Name = "Lenses", Definition = opticalCameraId } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = PayloadSubsystemId.ToString(), Name = "lens", Value = 2 } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = documentationId.ToString(), Name = "lens" } },
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Definition = PackageId.ToString() } },
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload() },
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Owner = PackageId.ToString() } },
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Text = " " } },
                new ModelChange { Payload = new ElementPayload { Name = "mainCamera" } },
                new ModelChange { Identity = relationshipId, Payload = new ElementPayload { Name = "membership" } },
                new ModelChange { Identity = PayloadSubsystemId.ToString() },
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Name = "mainCamera" } },
                new ModelChange { Identity = "antenna" }
            ]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.CommitRequest, Is.Null);
                Assert.That(result.CreatedElements, Has.Count.EqualTo(0));
                Assert.That(result.Problems, Has.Count.EqualTo(22));
                Assert.That(result.Problems[0], Is.EqualTo("Change 1: the change is empty."));
                Assert.That(result.Problems[1], Is.EqualTo("Change 2 (create): The name is missing."));
                Assert.That(result.Problems[2], Does.StartWith("Change 3 (create): 'Lens' is not the metaclass of a package, a definition or a usage"));
                Assert.That(result.Problems[3], Does.StartWith("Change 4 (create): 'FeatureTyping' is not the metaclass").And.EndWith("Relationships are built by the server."));
                Assert.That(result.Problems[4], Does.StartWith("Change 5 (create): No element has the identifier"));
                Assert.That(result.Problems[5], Does.StartWith("Change 6 (create): The owner 'optics' is neither the identifier"));
                Assert.That(result.Problems[6], Does.StartWith("Change 7 (create): The owner 'lens' has not been created"));
                Assert.That(result.Problems[7], Does.StartWith($"Change 8 (create): The PartUsage 'payloadSubsystem' already has a member named 'camera', whose identifier is {CameraId}"));
                Assert.That(result.Problems[8], Does.StartWith("Change 9 (create): The identity").And.EndWith("must be a temporary name, not an identifier: the server gives the identifier."));
                Assert.That(result.Problems[9], Is.EqualTo("Change 11 (create): The temporary name 'optics' is already used by a previous change."));
                Assert.That(result.Problems[10], Is.EqualTo("Change 12 (create): The text of the requirement is missing."));
                Assert.That(result.Problems[11], Is.EqualTo("Change 13 (create): Only a feature, for example a part or an attribute, has a definition, not the Package 'Lenses'."));
                Assert.That(result.Problems[12], Is.EqualTo("Change 14 (create): Only an attribute has a value, not the PartUsage 'lens'."));
                Assert.That(result.Problems[13], Does.StartWith("Change 15 (create): The owner must be a namespace, for example a package, a definition or a usage, not the Documentation"));
                Assert.That(result.Problems[14], Is.EqualTo("Change 16 (update): The definition must be a classifier, for example a part definition, not the Package 'EOSat1'."));
                Assert.That(result.Problems[15], Does.StartWith("Change 17 (update): The payload changes nothing"));
                Assert.That(result.Problems[16], Does.StartWith("Change 18 (update): An update cannot change the owner of an element"));
                Assert.That(result.Problems[17], Is.EqualTo("Change 19 (update): The text is empty."));
                Assert.That(result.Problems[18], Is.EqualTo("Change 20 (update): The element is missing."));
                Assert.That(result.Problems[19], Does.StartWith("Change 21 (update): The element must be an element that is not a relationship, not the"));
                Assert.That(result.Problems[20], Does.StartWith("Change 23 (update): The element").And.EndWith("is deleted by a previous change."));
                Assert.That(result.Problems[21], Does.StartWith("Change 24 (delete): The element 'antenna' is neither the identifier"));
            }
        }

        /// <summary>
        /// Reads the DTOs of the <c>Satellite.json</c> test model.
        /// </summary>
        /// <returns>The DTOs of the elements of the model.</returns>
        private static List<DtoElement> ReadSatelliteDtos()
        {
            using var stream = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json"));

            return new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .ToList();
        }

        /// <summary>
        /// Applies the change of a pending commit to the DTOs of a model, as a commit does: a payload adds or replaces the
        /// element of its identity, no payload removes it.
        /// </summary>
        /// <param name="model">The DTOs of the model before the commit.</param>
        /// <param name="pendingCommit">The <see cref="PendingCommit"/> to apply.</param>
        /// <returns>The DTOs of the model after the commit.</returns>
        private static List<DtoElement> ApplyChange(IEnumerable<DtoElement> model, PendingCommit pendingCommit)
        {
            var dtosById = model.ToDictionary(dto => dto.Id);

            foreach (var dataVersion in pendingCommit.CommitRequest.Change)
            {
                if (dataVersion.Payload is DtoElement element)
                {
                    dtosById[element.Id] = element;
                }
                else
                {
                    dtosById.Remove(dataVersion.Identity.Id);
                }
            }

            return [.. dtosById.Values];
        }

        /// <summary>
        /// Builds the POCOs of the given DTOs with SysML2.NET, to check the model through its derived properties.
        /// </summary>
        /// <param name="dtos">The DTOs of the model.</param>
        /// <returns>The POCOs of the model, indexed by their <c>Id</c>.</returns>
        private static Dictionary<Guid, IElement> Assemble(IEnumerable<DtoElement> dtos)
        {
            var assembler = new Assembler();
            assembler.Synchronize(dtos);

            return assembler.Cache.Values
                .Select(lazyElement => lazyElement.Value)
                .ToDictionary(element => element.Id);
        }
    }
}
