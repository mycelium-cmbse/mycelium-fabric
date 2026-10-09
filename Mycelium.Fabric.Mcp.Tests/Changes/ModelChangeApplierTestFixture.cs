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
    using System.Text.Json;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extensions;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Common;
    using SysML2.NET.Core.Core.Types;
    using SysML2.NET.Core.POCO.Core.Features;
    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Connections;
    using SysML2.NET.Core.POCO.Systems.DefinitionAndUsage;
    using SysML2.NET.Core.POCO.Systems.Interfaces;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Core.POCO.Systems.Ports;
    using SysML2.NET.Core.POCO.Systems.Requirements;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Core.Systems.Requirements;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoFeatureTyping = SysML2.NET.Core.DTO.Core.Features.FeatureTyping;
    using DtoRelationship = SysML2.NET.Core.DTO.Root.Elements.IRelationship;
    using DtoSatisfyRequirementUsage = SysML2.NET.Core.DTO.Systems.Requirements.ISatisfyRequirementUsage;

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

        /// <summary>
        /// The <c>Id</c> of the <c>aocsSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid AocsSubsystemId = Guid.Parse("aed7ac47-7926-ea14-6637-ac50ef557797");

        /// <summary>
        /// The <c>Id</c> of the <c>Requirements</c> package in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid RequirementsPackageId = Guid.Parse("896440c0-d061-7856-fb8f-fe278e26de93");

        /// <summary>
        /// The <c>Id</c> of the <c>massBudget</c> requirement (REQ-SYS-001) in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid MassBudgetId = Guid.Parse("6b93c533-0395-7e18-1bf6-4475deb47ba5");

        /// <summary>
        /// The <c>Id</c> of the <c>eclipseEnergy</c> requirement (REQ-SYS-006) in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid EclipseEnergyId = Guid.Parse("4c3c1285-20ba-ed54-45b3-f54699899da3");

        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid Eosat1Id = Guid.Parse("5d06ef4d-4489-500c-e94f-a8cf563d3fc1");

        /// <summary>
        /// The <c>Id</c> of the <c>payloadPower</c> requirement (REQ-SYS-002) in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadPowerId = Guid.Parse("d17c3498-3404-4a62-9728-c58e59a4954f");

        /// <summary>
        /// The <c>Id</c> of the requirement REQ-SYS-003 in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PointingAccuracyId = Guid.Parse("24253a34-a8ba-fc0a-0dba-e5e83d42bd5f");

        /// <summary>
        /// The <c>Id</c> of the requirement REQ-SYS-004 in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid ReactionWheelsId = Guid.Parse("a2c360e5-0019-9ec5-031e-14eadfab77b8");

        /// <summary>
        /// The <c>Id</c> of the <c>onBoardComputer</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid OnBoardComputerId = Guid.Parse("3d4c9630-97ec-0a3a-296a-742885df1bc0");

        private ModelChangeApplier applier;

        [SetUp]
        public void SetUp()
        {
            this.applier = new ModelChangeApplier(new Serializer(), new DeSerializer());
        }

        [Test]
        public void VerifyConstructor()
        {
            var serializer = new Mock<ISerializer>();
            var deSerializer = new Mock<IDeSerializer>();
            deSerializer.Setup(mock => mock.DeSerialize(It.IsAny<Stream>(), SerializationModeKind.JSON, SerializationTargetKind.PSM, false)).Returns([]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ModelChangeApplier(null, deSerializer.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ModelChangeApplier(serializer.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ModelChangeApplier(serializer.Object, deSerializer.Object).Apply([], []), Throws.Nothing);

                serializer.Verify(mock => mock.Serialize(It.IsAny<IEnumerable<IIdentified>>(), SerializationModeKind.JSON, false, It.IsAny<Stream>(), It.IsAny<JsonWriterOptions>()),
                    Times.Once);
                deSerializer.Verify(mock => mock.DeSerialize(It.IsAny<Stream>(), SerializationModeKind.JSON, SerializationTargetKind.PSM, false), Times.Once);
            }
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

            var result = this.applier.Apply(dtos,
            [
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Name = "mainCamera" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = CameraId.ToString(), Name = "lens" } },
                new ModelChange { Identity = RootNamespaceId.ToString(), Payload = new ElementPayload { Name = "Root" } }
            ]);

            var cameraVersion = result.Value.Change.Single(dataVersion => dataVersion.Identity.Id == CameraId);
            var creations = result.Value.Change.Where(dataVersion => dtos.TrueForAll(dto => dto.Id != dataVersion.Identity.Id)).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(result.Value.Change, Has.Count.EqualTo(4));
                Assert.That(((DtoElement)cameraVersion.Payload).DeclaredName, Is.EqualTo("mainCamera"));
                Assert.That(cameraVersion.Payload, Is.Not.SameAs(camera));
                Assert.That(camera.DeclaredName, Is.EqualTo("camera"));
                Assert.That(camera.OwnedRelationship, Has.Count.EqualTo(cameraRelationshipCount));
                Assert.That(creations.Select(dataVersion => dataVersion.Payload.GetType().Name), Is.EquivalentTo(["FeatureMembership", "PartUsage"]));
                Assert.That(creations.Select(dataVersion => ((DtoElement)dataVersion.Payload).Id), Is.EqualTo(creations.Select(dataVersion => dataVersion.Identity.Id)));
            }
        }

        [Test]
        public void Apply_WithCreations_BuildsTheElementsAndTheirRelationships()
        {
            var result = this.applier.Apply([],
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "requirements", Payload = new ElementPayload { Type = "Package", Owner = "spacecraft", Name = "Requirements" } },
                new ModelChange { Identity = "Camera", Payload = new ElementPayload { Type = "PartDefinition", Owner = "spacecraft", Name = "Camera", Text = "Optical camera." } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Camera", Name = "mass", Value = 38, Text = "Dry mass of the unit [kg]." } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "partUsage", Owner = "spacecraft", Name = "camera", Definition = "Camera" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "camera", Name = "lens" } },
                new ModelChange { Payload = new ElementPayload { Type = "RequirementUsage", Owner = "requirements", Name = "cameraMass", Text = "The camera shall weigh less than 40 kg." } }
            ]);

            var elements = Assemble(CommitRequestHelper.Apply([], result.Value));
            var namedElements = elements.Values.Where(element => element.DeclaredName != null).ToDictionary(element => element.DeclaredName);
            var package = namedElements["Spacecraft"];
            var definition = (IType)namedElements["Camera"];
            var mass = (IAttributeUsage)namedElements["mass"];
            var camera = (IPartUsage)namedElements["camera"];
            var lens = namedElements["lens"];
            var requirement = namedElements["cameraMass"];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(result.Value.Change, Has.Count.EqualTo(elements.Count));
                Assert.That(result.Value.Change.Select(dataVersion => ((DtoElement)dataVersion.Payload).Id), Is.EqualTo(result.Value.Change.Select(dataVersion => dataVersion.Identity.Id)));
                Assert.That(namedElements.Keys, Is.EquivalentTo(["Spacecraft", "Requirements", "Camera", "mass", "camera", "lens", "cameraMass"]));
                Assert.That(package.owner, Is.TypeOf<Namespace>());
                Assert.That(package.owner.owner, Is.Null);
                Assert.That(package.qualifiedName, Is.EqualTo("Spacecraft"));
                Assert.That(definition.GetDocumentationBodies(), Is.EqualTo("Optical camera."));
                Assert.That(definition.feature, Is.EqualTo([mass]));
                Assert.That(mass.GetNumericValue().Number, Is.EqualTo(38));
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

            var model = CommitRequestHelper.Apply([], creation.Value);
            var createdIds = model.Where(dto => dto.DeclaredName != null).ToDictionary(dto => dto.DeclaredName, dto => dto.Id.ToString());

            var result = this.applier.Apply(model,
            [
                new ModelChange { Identity = "SmallCamera", Payload = new ElementPayload { Type = "PartDefinition", Owner = createdIds["Spacecraft"], Name = "SmallCamera" } },
                new ModelChange { Identity = createdIds["camera"], Payload = new ElementPayload { Name = "mainCamera", Definition = "SmallCamera" } },
                new ModelChange { Identity = createdIds["mass"], Payload = new ElementPayload { Value = 35.5, Text = "Dry mass [kg]." } },
                new ModelChange { Identity = createdIds["Camera"], Payload = new ElementPayload { Name = "Camera" } }
            ]);

            var originalIds = model.Select(dto => dto.Id).ToHashSet();
            var updatedIds = result.Value.Change.Where(dataVersion => dataVersion.Payload != null && originalIds.Contains(dataVersion.Identity.Id)).Select(dataVersion => dataVersion.Identity.Id.ToString());
            var deletions = result.Value.Change.Where(dataVersion => dataVersion.Payload == null).ToList();

            var createdNames = result.Value.Change
                .Where(dataVersion => !originalIds.Contains(dataVersion.Identity.Id))
                .Select(dataVersion => ((DtoElement)dataVersion.Payload).DeclaredName)
                .Where(name => name != null);

            var elements = Assemble(CommitRequestHelper.Apply(model, result.Value));
            var camera = elements[Guid.Parse(createdIds["camera"])];
            var mass = (IAttributeUsage)elements[Guid.Parse(createdIds["mass"])];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(createdNames, Is.EqualTo(["SmallCamera"]));
                Assert.That(updatedIds, Is.EquivalentTo([createdIds["Spacecraft"], createdIds["camera"], createdIds["mass"], createdIds["Camera"]]));
                Assert.That(deletions, Has.Count.EqualTo(5));
                Assert.That(deletions.TrueForAll(dataVersion => originalIds.Contains(dataVersion.Identity.Id)), Is.True);
                Assert.That(camera.DeclaredName, Is.EqualTo("mainCamera"));
                Assert.That(camera.GetTypeNames(), Is.EqualTo(["SmallCamera"]));
                Assert.That(mass.GetNumericValue().Number, Is.EqualTo(35.5));
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
                Assert.That(refusal.IsError, Is.True);
                Assert.That(refusal.Errors, Has.Count.EqualTo(1));
                Assert.That(refusal.FirstError.Description, Does.StartWith("Change 1 (delete): the PartDefinition 'OpticalCamera' is still referenced by"));
                Assert.That(refusal.FirstError.Description, Does.Contain("'EOSat1::Architecture::eosat1::payloadSubsystem::camera'"));
            }

            var result = this.applier.Apply(dtos,
            [
                new ModelChange { Identity = CameraId.ToString() },
                new ModelChange { Identity = opticalCameraId },
                new ModelChange { Identity = "newCamera", Payload = new ElementPayload { Type = "PartUsage", Owner = PayloadSubsystemId.ToString(), Name = "camera" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "newCamera", Name = "lens" } },
                new ModelChange { Identity = "newCamera" }
            ]);

            var elements = Assemble(CommitRequestHelper.Apply(dtos, result.Value));
            var payloadParts = elements[PayloadSubsystemId].ownedElement.OfType<IPartUsage>().Select(part => part.DeclaredName);
            var deletedIds = result.Value.Change.Where(dataVersion => dataVersion.Payload == null).Select(dataVersion => dataVersion.Identity.Id).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(result.Value.Change.Select(dataVersion => dataVersion.Identity.Id), Is.SubsetOf(dtos.Select(dto => dto.Id)));
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

            var problems = result.Errors.Select(error => error.Description).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.True);
                Assert.That(result.Errors.TrueForAll(error => error.Type == ErrorType.Validation), Is.True);
                Assert.That(problems, Has.Count.EqualTo(22));
                Assert.That(problems[0], Is.EqualTo("Change 1: the change is empty."));
                Assert.That(problems[1], Is.EqualTo("Change 2 (create): The name is missing."));
                Assert.That(problems[2], Does.StartWith("Change 3 (create): 'Lens' is not the metaclass of a package, a definition, a usage or a connector"));
                Assert.That(problems[3], Does.StartWith("Change 4 (create): 'FeatureTyping' is not the metaclass").And.EndWith("The other relationships are built by the server."));
                Assert.That(problems[4], Does.StartWith("Change 5 (create): No element has the identifier"));
                Assert.That(problems[5], Does.StartWith("Change 6 (create): The owner 'optics' is neither the identifier"));
                Assert.That(problems[6], Does.StartWith("Change 7 (create): The owner 'lens' has not been created"));
                Assert.That(problems[7], Does.StartWith($"Change 8 (create): The PartUsage 'payloadSubsystem' already has a member named 'camera', whose identifier is {CameraId}"));
                Assert.That(problems[8], Does.StartWith("Change 9 (create): The identity").And.EndWith("must be a temporary name, not an identifier: the server gives the identifier."));
                Assert.That(problems[9], Is.EqualTo("Change 11 (create): The temporary name 'optics' is already used by a previous change."));
                Assert.That(problems[10], Is.EqualTo("Change 12 (create): The text of the requirement is missing."));
                Assert.That(problems[11], Is.EqualTo("Change 13 (create): Only a feature, for example a part or an attribute, has a definition, not the Package 'Lenses'."));
                Assert.That(problems[12], Is.EqualTo("Change 14 (create): Only an attribute has a value, not the PartUsage 'lens'."));
                Assert.That(problems[13], Does.StartWith("Change 15 (create): The owner must be a namespace, for example a package, a definition or a usage, not the Documentation"));
                Assert.That(problems[14], Is.EqualTo("Change 16 (update): The definition must be a classifier, for example a part definition, not the Package 'EOSat1'."));
                Assert.That(problems[15], Does.StartWith("Change 17 (update): The payload changes nothing"));
                Assert.That(problems[16], Does.StartWith("Change 18 (update): An update cannot change the owner of an element"));
                Assert.That(problems[17], Is.EqualTo("Change 19 (update): The text is empty."));
                Assert.That(problems[18], Is.EqualTo("Change 20 (update): The element is missing."));
                Assert.That(problems[19], Does.StartWith("Change 21 (update): The element must be an element that is not a relationship, not the"));
                Assert.That(problems[20], Does.StartWith("Change 23 (update): The element").And.EndWith("is deleted by a previous change."));
                Assert.That(problems[21], Does.StartWith("Change 24 (delete): The element 'antenna' is neither the identifier"));
            }
        }

        [Test]
        public void Apply_WithRequirementProperties_MakesTheRequirementsVerifiable()
        {
            var dtos = ReadSatelliteDtos();

            var result = this.applier.Apply(dtos,
            [
                new ModelChange
                {
                    Identity = "cameraMass",
                    Payload = new ElementPayload
                    {
                        Type = "RequirementUsage", Owner = RequirementsPackageId.ToString(), Name = "cameraMass", ReqId = "REQ-PL-001", Text = "The camera shall weigh at most 40 kg.",
                        Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<", Limit = 10 }
                    }
                },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 40, Margin = 10 } } },
                new ModelChange
                {
                    Identity = "cameraSatisfy",
                    Payload = new ElementPayload
                    {
                        Type = "SatisfyRequirementUsage", SatisfiedRequirement = "cameraMass", SatisfyingPart = CameraId.ToString(), Name = "cameraMassSatisfaction", Text = "The camera alone carries this mass."
                    }
                },
                new ModelChange { Identity = EclipseEnergyId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "capacity", Operator = ">=", Limit = 300, Margin = 20 } } },
                new ModelChange { Identity = EclipseEnergyId.ToString(), Payload = new ElementPayload { ReqId = "REQ-SYS-006", Constraint = new ConstraintPayload { Attribute = "mass", Operator = "==", Limit = 5.8, Margin = 0 } } }
            ]);

            var model = CommitRequestHelper.Apply(dtos, result.Value);
            var elements = Assemble(model);
            var cameraMass = elements.Values.OfType<IRequirementUsage>().Single(requirement => requirement.DeclaredName == "cameraMass");
            var satisfy = elements.Values.OfType<ISatisfyRequirementUsage>().Single(satisfyRequirement => satisfyRequirement.DeclaredName == "cameraMassSatisfaction");
            var eclipseEnergy = (IRequirementUsage)elements[EclipseEnergyId];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(cameraMass.ReqId, Is.EqualTo("REQ-PL-001"));
                Assert.That(cameraMass.shortName, Is.EqualTo("REQ-PL-001"));
                Assert.That(cameraMass.GetDocumentationBodies(), Is.EqualTo("The camera shall weigh at most 40 kg."));
                Assert.That(cameraMass.subjectParameter.DeclaredName, Is.EqualTo("subj"));
                Assert.That(cameraMass.subjectParameter.feature.Select(feature => feature.DeclaredName), Is.EqualTo(["mass"]));
                Assert.That(cameraMass.requiredConstraint, Has.Count.EqualTo(1));
                Assert.That(cameraMass.requiredConstraint[0].GetTerm().Value.ToString(), Is.EqualTo("subj.mass * 1.1 <= 40"));
                Assert.That(satisfy.owner, Is.SameAs(cameraMass.owner));
                Assert.That(satisfy.GetDocumentationBodies(), Is.EqualTo("The camera alone carries this mass."));
                Assert.That(satisfy.satisfiedRequirement, Is.SameAs(cameraMass));
                Assert.That(satisfy.ResolveSatisfyingFeature(), Is.SameAs(elements[CameraId]));
                Assert.That(eclipseEnergy.ReqId, Is.EqualTo("REQ-SYS-006"));
                Assert.That(eclipseEnergy.subjectParameter.feature.Select(feature => feature.DeclaredName), Is.EqualTo(["capacity", "mass"]));
                Assert.That(eclipseEnergy.requiredConstraint, Has.Count.EqualTo(1));
                Assert.That(eclipseEnergy.requiredConstraint[0].GetTerm().Value.ToString(), Is.EqualTo("subj.mass == 5.8"));
            }

            // The constraints are replaced, and the attributes are reused: the one of the subject, or the one of its definition
            // when the subject of cameraMass is typed by OpticalCamera.
            var subjectTyping = new DtoFeatureTyping { Id = Guid.NewGuid(), TypedFeature = cameraMass.subjectParameter.Id, Type = model.Single(dto => dto.DeclaredName == "OpticalCamera").Id };
            subjectTyping.OwningRelatedElement = subjectTyping.TypedFeature;
            model.Single(dto => dto.Id == subjectTyping.TypedFeature).OwnedRelationship.Add(subjectTyping.Id);
            model.Add(subjectTyping);

            var replacement = this.applier.Apply(model,
            [
                new ModelChange { Identity = EclipseEnergyId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "capacity", Operator = ">=", Limit = 300, Margin = 20 } } },
                new ModelChange { Identity = cameraMass.Id.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "power", Operator = "<=", Limit = 60 } } }
            ]);

            model = CommitRequestHelper.Apply(model, replacement.Value);
            elements = Assemble(model);
            eclipseEnergy = (IRequirementUsage)elements[EclipseEnergyId];
            cameraMass = (IRequirementUsage)elements[cameraMass.Id];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(replacement.IsError, Is.False);
                Assert.That(eclipseEnergy.subjectParameter.feature, Has.Count.EqualTo(2));
                Assert.That(eclipseEnergy.requiredConstraint, Has.Count.EqualTo(1));
                Assert.That(eclipseEnergy.requiredConstraint[0].GetTerm().Value.ToString(), Is.EqualTo("subj.capacity >= 300 * 1.2"));
                Assert.That(cameraMass.subjectParameter.ownedFeature.Select(feature => feature.DeclaredName), Is.EqualTo(["mass"]));
                Assert.That(cameraMass.requiredConstraint[0].GetTerm().Value.ToString(), Is.EqualTo("subj.power <= 60"));
            }

            // A part that satisfies a requirement can only be deleted with its satisfy link.
            var refusal = this.applier.Apply(model, [new ModelChange { Identity = CameraId.ToString() }]);
            var deletion = this.applier.Apply(model, [new ModelChange { Identity = satisfy.Id.ToString() }, new ModelChange { Identity = CameraId.ToString() }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refusal.Errors, Has.Count.EqualTo(1));
                Assert.That(refusal.FirstError.Description, Does.EndWith($"is still referenced by the SatisfyRequirementUsage {satisfy.Id}. Change or delete these elements first."));
                Assert.That(deletion.IsError, Is.False);
                Assert.That(Assemble(CommitRequestHelper.Apply(model, deletion.Value)), Does.Not.ContainKey(satisfy.Id));
            }
        }

        [Test]
        public void Apply_WithInvalidRequirementProperties_ReturnsEveryProblem()
        {
            var dtos = ReadSatelliteDtos();
            var creation = this.applier.Apply(dtos, [new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = MassBudgetId.ToString(), SatisfyingPart = CameraId.ToString() } }]);
            var satisfyDto = creation.Value.Change.Select(dataVersion => dataVersion.Payload).OfType<DtoSatisfyRequirementUsage>().Single();
            Assert.That(satisfyDto.DeclaredName, Is.Null);

            var model = CommitRequestHelper.Apply(dtos, creation.Value);
            var massBudget = MassBudgetId.ToString();

            var result = this.applier.Apply(model,
            [
                new ModelChange { Payload = new ElementPayload { Type = "RequirementUsage", Owner = RequirementsPackageId.ToString(), Name = "cameraMass", ReqId = "REQ-SYS-001", Text = "The camera shall weigh at most 40 kg." } },
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150 } } },
                new ModelChange { Identity = satisfyDto.Id.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Operator = "<=", Limit = 150 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "=<", Limit = 150 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=" } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, Margin = -5 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "==", Limit = 150, Margin = 20 } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = massBudget, SatisfyingPart = RequirementsPackageId.ToString() } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = massBudget, SatisfyingPart = CameraId.ToString() } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = massBudget, SatisfyingPart = PayloadSubsystemId.ToString() } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = CameraId.ToString(), SatisfyingPart = PayloadSubsystemId.ToString() } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = massBudget, SatisfyingPart = AocsSubsystemId.ToString(), Value = 1 } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = PayloadSubsystemId.ToString(), Name = "lens", SatisfyingPart = CameraId.ToString() } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = massBudget, SatisfyingPart = AocsSubsystemId.ToString(), Name = "massBudget" } }
            ]);

            var problems = result.Errors.Select(error => error.Description).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.True);
                Assert.That(problems, Has.Count.EqualTo(14));
                Assert.That(problems[0], Is.EqualTo($"Change 1 (create): The ReqId 'REQ-SYS-001' is already used by the RequirementUsage 'massBudget', whose identifier is {MassBudgetId}."));
                Assert.That(problems[1], Is.EqualTo("Change 2 (update): Only a requirement has a reqId or a constraint, not the PartUsage 'camera'."));
                Assert.That(problems[2], Is.EqualTo($"Change 3 (update): Only a requirement has a reqId or a constraint, not the SatisfyRequirementUsage {satisfyDto.Id}."));
                Assert.That(problems[3], Is.EqualTo("Change 4 (update): The attribute of the constraint is missing."));
                Assert.That(problems[4], Is.EqualTo("Change 5 (update): The operator '=<' is not supported. Use '<', '<=', '>', '>=', '==', '!='."));
                Assert.That(problems[5], Is.EqualTo("Change 6 (update): The limit of the constraint is missing: give a limit or a limitAttribute."));
                Assert.That(problems[6], Is.EqualTo("Change 7 (update): The margin must be 0 or greater."));
                Assert.That(problems[7], Is.EqualTo("Change 8 (update): A margin cannot be applied with '=='. Use '<=' or '>=' instead."));
                Assert.That(problems[8], Is.EqualTo("Change 9 (create): The satisfying part must be a part, not the Package 'Requirements'."));
                Assert.That(problems[9], Is.EqualTo("Change 10 (create): The RequirementUsage 'massBudget' is already satisfied by the PartUsage 'camera'."));
                Assert.That(problems[10], Is.EqualTo("Change 12 (create): The satisfied requirement must be a requirement, not the PartUsage 'camera'."));
                Assert.That(problems[11], Does.StartWith("Change 13 (create): A SatisfyRequirementUsage takes a satisfied requirement and a satisfying part"));
                Assert.That(problems[12], Does.StartWith("Change 14 (create): The satisfied requirement and the satisfying part only apply to the creation of a SatisfyRequirementUsage."));
                Assert.That(problems[13], Is.EqualTo($"Change 15 (create): The Package 'Requirements' already has a member named 'massBudget', whose identifier is {MassBudgetId}."));
            }
        }

        [Test]
        public void Apply_WithConstraintsAndValuesOfEveryForm_BuildsTheExpressions()
        {
            var dtos = ReadSatelliteDtos();
            new ConstraintDtoBuilder(dtos).Enumeration(RequirementsPackageId, "OrbitKind", "sunSynchronous", "polar");

            var result = this.applier.Apply(dtos,
            [
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "isRedundant", Value = true } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "band", Value = "S-band" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "heat", Value = 5, Unit = "W" } },
                new ModelChange { Identity = "orbit", Payload = new ElementPayload { Type = "AttributeUsage", Owner = Eosat1Id.ToString(), Name = "orbit", Value = "OrbitKind::sunSynchronous" } },
                new ModelChange { Identity = "orbit", Payload = new ElementPayload { Value = "polar" } },
                new ModelChange { Identity = MassBudgetId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "payloadSubsystem.camera.mass", Operator = "<=", Limit = 40, Unit = "kg" } } },
                new ModelChange
                {
                    Identity = MassBudgetId.ToString(),
                    Payload = new ElementPayload { Constraint = new ConstraintPayload { Kind = RequirementConstraintKind.Assumption, Attribute = "orbit", Operator = "==", Limit = "sunSynchronous" } }
                },
                new ModelChange
                {
                    Identity = MassBudgetId.ToString(),
                    Payload = new ElementPayload { Constraint = new ConstraintPayload { Kind = RequirementConstraintKind.Assumption, Attribute = "orbit", Operator = "!=", Limit = "OrbitKind::polar" } }
                },
                new ModelChange { Identity = EclipseEnergyId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "fuelLevel", Operator = ">=", LimitAttribute = "fuelTankCapacity", Margin = 10 } } },
                new ModelChange { Identity = PayloadPowerId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "payloadSubsystem.camera.isRedundant", Operator = "==", Limit = true } } },
                new ModelChange { Identity = PointingAccuracyId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "band", Operator = "!=", Limit = "X-band" } } },
                new ModelChange { Identity = ReactionWheelsId.ToString(), Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "temperature", Operator = ">=", Limit = -20, Unit = "°C" } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = MassBudgetId.ToString(), SatisfyingPart = Eosat1Id.ToString() } }
            ]);

            var model = CommitRequestHelper.Apply(dtos, result.Value);
            var elements = Assemble(model);
            var camera = elements[CameraId].GetAttributeUsages().ToDictionary(attribute => attribute.DeclaredName);
            var orbit = elements[Eosat1Id].GetAttributeUsages().Single(attribute => attribute.DeclaredName == "orbit");
            var massBudget = (IRequirementUsage)elements[MassBudgetId];
            var check = new RequirementChecker(elements.Values).CheckRequirements().Single(requirementCheck => requirementCheck.ReqId == "REQ-SYS-001");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(camera["isRedundant"].GetConstantValue(), Is.EqualTo(new BooleanValue(true)));
                Assert.That(camera["band"].GetConstantValue(), Is.EqualTo(new TextValue("S-band")));
                Assert.That(camera["heat"].GetConstantValue().ToString(), Is.EqualTo("5 [W]"));
                Assert.That(orbit.GetConstantValue(), Is.EqualTo(new EnumValue("polar")));
                Assert.That(massBudget.subjectParameter.ownedFeature.Select(feature => feature.DeclaredName), Is.EqualTo(["payloadSubsystem", "orbit"]));
                Assert.That(massBudget.requiredConstraint.Single().GetTerm().Value.ToString(), Is.EqualTo("subj.payloadSubsystem.camera.mass <= 40 [kg]"));
                Assert.That(massBudget.assumedConstraint.Single().GetTerm().Value.ToString(), Is.EqualTo("subj.orbit != OrbitKind::polar"));
                Assert.That(Constraint(EclipseEnergyId), Is.EqualTo("subj.fuelLevel >= subj.fuelTankCapacity * 1.1"));
                Assert.That(Constraint(PayloadPowerId), Is.EqualTo("subj.payloadSubsystem.camera.isRedundant == true"));
                Assert.That(Constraint(PointingAccuracyId), Is.EqualTo("subj.band != \"X-band\""));
                Assert.That(Constraint(ReactionWheelsId), Is.EqualTo("subj.temperature >= -20 [°C]"));
                Assert.That(check.Status, Is.EqualTo(RequirementStatus.Satisfied));
                Assert.That(check.Explanation,
                    Is.EqualTo("assume subj.orbit != OrbitKind::polar: polar == polar; require subj.payloadSubsystem.camera.mass <= 40 [kg]: 38 [kg] <= 40 [kg]"));
            }

            // Writes the required constraint of a requirement.
            string Constraint(Guid requirementId)
            {
                return ((IRequirementUsage)elements[requirementId]).requiredConstraint.Single().GetTerm().Value.ToString();
            }
        }

        [Test]
        public void Apply_WithInvalidConstraintsAndValues_ReturnsEveryProblem()
        {
            var dtos = ReadSatelliteDtos();
            var builder = new ConstraintDtoBuilder(dtos);
            builder.Enumeration(RequirementsPackageId, "ModeA", "nominal");
            builder.Enumeration(RequirementsPackageId, "ModeB", "nominal");
            var massBudget = MassBudgetId.ToString();

            var result = this.applier.Apply(dtos,
            [
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "isRedundant", Value = true, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "length", Value = 3, Unit = "furlong" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "band", Value = " " } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = CameraId.ToString(), Name = "mode", Value = "nominal" } },
                new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Unit = "kg" } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 150, LimitAttribute = "massLimit" } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", LimitAttribute = "mass..limit" } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = ".mass", Operator = "<=", Limit = 150 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "isRedundant", Operator = "<", Limit = true } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "!=", Limit = 150, Margin = 10 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", LimitAttribute = "massLimit", Unit = "kg" } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Kind = (RequirementConstraintKind)5, Attribute = "mass", Operator = "<=", Limit = 150 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "temperature", Operator = ">=", Limit = -20, Margin = 10 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "temperature", Operator = "<=", Limit = 40, Unit = "°C", Margin = 10 } } },
                new ModelChange { Identity = massBudget, Payload = new ElementPayload { Constraint = new ConstraintPayload { Attribute = "mode", Operator = "==", Limit = "nominal" } } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = massBudget, SatisfyingPart = CameraId.ToString(), Unit = "kg" } }
            ]);

            var problems = result.Errors.Select(error => error.Description).ToList();

            string[] expectedProblems =
            [
                "Change 1 (create): The unit 'kg' only applies to a numeric value.",
                "Change 2 (create): The unit 'furlong' is not known. Use a symbol such as kg, W, km/h, m/s^2 or arcsec, or a name such as kilogram.",
                "Change 3 (create): The value is empty: give a number, a Boolean or a text.",
                "Change 4 (create): 'nominal' names several enumeration values: write 'ModeA::nominal' or 'ModeB::nominal'.",
                "Change 5 (update): The unit 'kg' only applies to a numeric value.",
                "Change 6 (update): Give either a limit or a limitAttribute, not both.",
                "Change 7 (update): The path 'mass..limit' has an empty name: write it as 'mass' or 'camera.mass'.",
                "Change 8 (update): The path '.mass' has an empty name: write it as 'mass' or 'camera.mass'.",
                "Change 9 (update): A Boolean or text limit can only be compared with '==' or '!=', not with '<'.",
                "Change 10 (update): A margin cannot be applied with '!='. Use '<=' or '>=' instead.",
                "Change 11 (update): The unit 'kg' only applies to a numeric value.",
                "Change 12 (update): The kind of the constraint must be Requirement or Assumption.",
                "Change 13 (update): A margin in percent only applies to a positive limit: include the margin in the limit instead.",
                "Change 14 (update): A margin in percent does not apply to a unit with an offset, such as °C: include the margin in the limit instead.",
                "Change 15 (update): 'nominal' names several enumeration values: write 'ModeA::nominal' or 'ModeB::nominal'.",
                "Change 16 (create): A SatisfyRequirementUsage takes a satisfied requirement and a satisfying part, and optionally an owner, a name and a text."
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.True);
                Assert.That(problems, Is.EqualTo(expectedProblems));
            }
        }

        [Test]
        public void Apply_WithPortsAndConnectors_BuildsThemAsTheTextualNotation()
        {
            var dtos = ReadSatelliteDtos();
            var result = this.applier.Apply(dtos, ConnectionChangesHelper.CreateChanges());

            var elements = Assemble(CommitRequestHelper.Apply(dtos, result.Value));
            var newElements = elements.Values.Where(element => dtos.TrueForAll(dto => dto.Id != element.Id)).ToList();
            var named = newElements.Where(element => element.DeclaredName != null).ToDictionary(element => element.DeclaredName);
            var dataPort = (IPortDefinition)named["DataPort"];
            var dataInterface = (IInterfaceDefinition)named["DataInterface"];
            var imageLink = (IInterfaceUsage)named["imageLink"];
            var dataIn = (IPortUsage)named["dataIn"];
            var connection = newElements.OfType<IConnectionUsage>().Single(connectionUsage => connectionUsage is not IInterfaceUsage);
            var binding = newElements.OfType<IBindingConnectorAsUsage>().Single();
            var cameraPath = imageLink.relatedFeature[0].chainingFeature;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(newElements.OfType<IConjugatedPortDefinition>().Single(), Is.SameAs(dataPort.conjugatedPortDefinition));
                Assert.That(dataPort.conjugatedPortDefinition.originalPortDefinition, Is.SameAs(dataPort));
                Assert.That(dataPort.GetDocumentationBodies(), Is.EqualTo("Image data."));
                Assert.That(((IFeature)named["rate"]).Direction, Is.EqualTo(FeatureDirectionKind.Out));
                Assert.That(dataInterface.ownedEndFeature.Select(feature => feature.DeclaredName), Is.EqualTo(["source", "target"]));
                Assert.That(dataInterface.ownedEndFeature.All(feature => feature is IPortUsage { IsConstant: true, OwningRelationship: IEndFeatureMembership }), Is.True);
                Assert.That(dataInterface.ownedEndFeature[1].GetTypeNames(), Is.EqualTo(["~DataPort"]));
                Assert.That(dataIn.GetTypeNames(), Is.EqualTo(["~DataPort"]));
                Assert.That(dataIn.ownedTyping.Single(), Is.InstanceOf<IConjugatedPortTyping>());
                Assert.That(named["imageOut"].GetTypeNames(), Is.EqualTo(["DataPort"]));
                Assert.That(imageLink.owner, Is.SameAs(elements[Eosat1Id]));
                Assert.That(imageLink.IsComposite, Is.True);
                Assert.That(imageLink.interfaceDefinition, Is.EqualTo([dataInterface]));
                Assert.That(imageLink.GetDocumentationBodies(), Is.EqualTo("Images to the on-board computer."));
                Assert.That(imageLink.GetEndPaths(), Is.EqualTo([ConnectionChangesHelper.CameraDataOut, ConnectionChangesHelper.ComputerDataIn]));
                Assert.That(imageLink.ownedEndFeature, Has.Count.EqualTo(2));
                Assert.That(imageLink.ownedEndFeature.All(end => end is IReferenceUsage { IsEnd: true, IsConstant: true, OwningRelationship: IEndFeatureMembership }), Is.True);
                Assert.That(cameraPath.Select(feature => feature.DeclaredName), Is.EqualTo(["payloadSubsystem", "camera", "dataOut"]));
                Assert.That(cameraPath[1], Is.SameAs(elements[CameraId]));
                Assert.That(cameraPath[2].owner.DeclaredName, Is.EqualTo("OpticalCamera"));
                Assert.That(imageLink.relatedFeature[1].chainingFeature[2], Is.SameAs(dataIn));
                Assert.That(connection.DeclaredName, Is.Null);
                Assert.That(connection.relatedFeature, Is.EqualTo([elements[CameraId], elements[Guid.Parse("8f7fcbf6-8c6a-5546-1eb6-2e5adfe7a3d4")]]));
                Assert.That(binding.GetEndPaths(), Is.EqualTo(["imageOut", "camera.dataOut"]));
                Assert.That(binding.ownedEndFeature.All(end => !end.IsConstant), Is.True);
            }
        }

        [Test]
        public void Apply_WithPortAndConnectorUpdatesAndDeletions_ChangesThemUnlessStillConnected()
        {
            var dtos = ReadSatelliteDtos();
            var model = CommitRequestHelper.Apply(dtos, this.applier.Apply(dtos, ConnectionChangesHelper.CreateChanges()).Value);
            var newIds = model.Where(dto => dtos.TrueForAll(original => original.Id != dto.Id)).ToList();
            var ids = newIds.Where(dto => dto.DeclaredName != null).ToDictionary(dto => dto.DeclaredName, dto => dto.Id.ToString());
            var connectionId = newIds.Single(dto => dto.GetType().Name == "ConnectionUsage").Id;
            var bindingId = newIds.Single(dto => dto.GetType().Name == "BindingConnectorAsUsage").Id;
            var massMemoryId = "8f7fcbf6-8c6a-5546-1eb6-2e5adfe7a3d4";

            var refusal = this.applier.Apply(model, [new ModelChange { Identity = ids["dataIn"] }, new ModelChange { Identity = massMemoryId }]);

            string[] expectedProblems =
            [
                "Change 1 (delete): the PortUsage 'dataIn' is still referenced by 'EOSat1::Architecture::eosat1::imageLink'. Change or delete these elements first.",
                $"Change 2 (delete): the PartUsage 'massMemory' is still referenced by the ConnectionUsage {connectionId}. Change or delete these elements first."
            ];

            Assert.That(refusal.Errors.Select(error => error.Description), Is.EqualTo(expectedProblems));

            var result = this.applier.Apply(model,
            [
                new ModelChange { Identity = ids["imageLink"], Payload = new ElementPayload { Name = "cameraToComputer", Text = "Images and housekeeping." } },
                new ModelChange { Identity = ids["dataIn"], Payload = new ElementPayload { Conjugated = false } },
                new ModelChange { Identity = ids["dataOut"], Payload = new ElementPayload { Conjugated = true } },
                new ModelChange { Identity = ids["imageOut"], Payload = new ElementPayload { Definition = ids["DataPort"], Direction = FeatureDirectionKind.Out } },
                new ModelChange { Identity = ids["rate"], Payload = new ElementPayload { Direction = FeatureDirectionKind.In } },
                new ModelChange { Identity = bindingId.ToString() },
                new ModelChange { Identity = connectionId.ToString() },
                new ModelChange { Identity = massMemoryId }
            ]);

            var elements = Assemble(CommitRequestHelper.Apply(model, result.Value));
            var link = (IInterfaceUsage)elements[Guid.Parse(ids["imageLink"])];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.False);
                Assert.That(link.DeclaredName, Is.EqualTo("cameraToComputer"));
                Assert.That(link.GetDocumentationBodies(), Is.EqualTo("Images and housekeeping."));
                Assert.That(link.GetEndPaths(), Is.EqualTo([ConnectionChangesHelper.CameraDataOut, ConnectionChangesHelper.ComputerDataIn]));
                Assert.That(elements[Guid.Parse(ids["dataIn"])].GetTypeNames(), Is.EqualTo(["DataPort"]));
                Assert.That(elements[Guid.Parse(ids["dataOut"])].GetTypeNames(), Is.EqualTo(["~DataPort"]));
                Assert.That(((IFeature)elements[Guid.Parse(ids["imageOut"])]).Direction, Is.EqualTo(FeatureDirectionKind.Out));
                Assert.That(((IFeature)elements[Guid.Parse(ids["rate"])]).Direction, Is.EqualTo(FeatureDirectionKind.In));
                Assert.That(elements.Values.OfType<IConjugatedPortDefinition>().Count(), Is.EqualTo(1));
                Assert.That(elements.Values.OfType<IConnectionUsage>().ToList(), Is.EqualTo([link]));
                Assert.That(elements.Values.OfType<IBindingConnectorAsUsage>(), Is.Empty);
                Assert.That(elements, Does.Not.ContainKey(Guid.Parse(massMemoryId)));
            }
        }

        [Test]
        public void Apply_WithInvalidPortsAndConnectors_ReturnsEveryProblem()
        {
            var dtos = ReadSatelliteDtos();
            var model = CommitRequestHelper.Apply(dtos, this.applier.Apply(dtos, ConnectionChangesHelper.CreateChanges()).Value);
            var ids = model.Where(dto => dto.DeclaredName is "imageLink" or "dataOut" or "DataInterface").ToDictionary(dto => dto.DeclaredName, dto => dto.Id.ToString());
            var conjugatedPortDefinitionId = model.Single(dto => dto.GetType().Name == "ConjugatedPortDefinition").Id.ToString();
            var eosat1 = ConnectionChangesHelper.Eosat1Id;
            var payloadSubsystem = ConnectionChangesHelper.PayloadSubsystemId;
            var opticalCamera = ConnectionChangesHelper.OpticalCameraId;
            IReadOnlyList<string> validEnds = [ConnectionChangesHelper.CameraDataOut, ConnectionChangesHelper.ComputerDataIn];

            var result = this.applier.Apply(model,
            [
                new ModelChange { Payload = new ElementPayload { Type = "InterfaceUsage", Owner = eosat1, Ends = [ConnectionChangesHelper.CameraDataOut] } },
                new ModelChange { Payload = new ElementPayload { Type = "ConnectionUsage", Owner = eosat1, Ends = ["payloadSubsystem.lens", "dataHandlingSubsystem"] } },
                new ModelChange { Payload = new ElementPayload { Type = "InterfaceUsage", Owner = eosat1, Ends = ["payloadSubsystem.camera", ConnectionChangesHelper.ComputerDataIn] } },
                new ModelChange { Payload = new ElementPayload { Type = "ConnectionUsage", Owner = payloadSubsystem, Ends = ["camera", " camera "] } },
                new ModelChange { Payload = new ElementPayload { Type = "InterfaceUsage", Owner = eosat1, Definition = opticalCamera, Ends = validEnds } },
                new ModelChange { Payload = new ElementPayload { Type = "BindingConnectorAsUsage", Owner = payloadSubsystem, Definition = ids["DataInterface"], Ends = ["imageOut", "camera.dataOut"] } },
                new ModelChange { Payload = new ElementPayload { Type = "ConnectionUsage", Owner = payloadSubsystem, Ends = ["camera", "massMemory"], Value = 1 } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = payloadSubsystem, Name = "lens", Ends = ["camera", "massMemory"] } },
                new ModelChange { Payload = new ElementPayload { Type = "Package", Owner = ConnectionChangesHelper.PackageId, Name = "Ports", Direction = FeatureDirectionKind.Out } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = payloadSubsystem, Name = "lens", Definition = opticalCamera, Conjugated = true } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = payloadSubsystem, Name = "commandIn", Conjugated = true } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = opticalCamera, Name = "lens", IsEnd = true } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = ids["DataInterface"], Name = "latency", IsEnd = true } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = payloadSubsystem, Name = "commandIn", Definition = opticalCamera } },
                new ModelChange { Payload = new ElementPayload { Type = "ConjugatedPortDefinition", Owner = ConnectionChangesHelper.PackageId, Name = "~CommandPort" } },
                new ModelChange { Payload = new ElementPayload { Type = "FlowUsage", Owner = eosat1, Ends = validEnds } },
                new ModelChange { Identity = ids["imageLink"], Payload = new ElementPayload { Ends = validEnds } },
                new ModelChange { Identity = ids["dataOut"], Payload = new ElementPayload { IsEnd = true } },
                new ModelChange { Identity = "bare", Payload = new ElementPayload { Type = "PortUsage", Owner = payloadSubsystem, Name = "bare" } },
                new ModelChange { Identity = "bare", Payload = new ElementPayload { Conjugated = true } },
                new ModelChange { Payload = new ElementPayload { Type = "PortUsage", Owner = payloadSubsystem, Name = "commandIn", Definition = conjugatedPortDefinitionId } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = MassBudgetId.ToString(), SatisfyingPart = CameraId.ToString(), Ends = validEnds } },
                new ModelChange { Payload = new ElementPayload { Type = "ConnectionUsage", Owner = payloadSubsystem, Ends = ["camera", " "] } },
                new ModelChange { Payload = new ElementPayload { Type = "ConnectionUsage", Owner = payloadSubsystem, Definition = opticalCamera, Ends = ["camera", "massMemory"] } }
            ]);

            var problems = result.Errors.Select(error => error.Description).ToList();

            string[] expectedProblems =
            [
                "Change 1 (create): A connector connects two ends: give them as paths of names from its owner, for example ['camera.dataOut', 'obc.dataIn'].",
                "Change 2 (create): The end 'payloadSubsystem.lens' is not valid: the PartUsage 'payloadSubsystem' and its definitions have no feature named 'lens'. Use list_children to read their features.",
                "Change 3 (create): The end 'payloadSubsystem.camera' of an interface must be a port, not the PartUsage 'camera'.",
                "Change 4 (create): The two ends are the same feature 'camera'.",
                "Change 5 (create): The definition must be an interface definition, not the PartDefinition 'OpticalCamera'.",
                "Change 6 (create): A BindingConnectorAsUsage has no definition: it states that its two ends are the same thing.",
                "Change 7 (create): A connector takes two ends, and optionally an owner, a name, a definition and a text.",
                "Change 8 (create): Only a connector (ConnectionUsage, InterfaceUsage or BindingConnectorAsUsage) has ends, not the PartUsage 'lens'.",
                "Change 9 (create): Only a feature, for example a port or an attribute, has a direction, not the Package 'Ports'.",
                "Change 10 (create): Only a port can be conjugated, not the PartUsage 'lens'.",
                "Change 11 (create): A conjugated port needs a definition: the port definition whose conjugate types it.",
                "Change 12 (create): Only a feature of a connection or interface definition can be an end, not the PartUsage 'lens' of the PartDefinition 'OpticalCamera'.",
                "Change 13 (create): The ends of an interface definition are ports, not the AttributeUsage 'latency'.",
                "Change 14 (create): The definition must be a port definition, not the PartDefinition 'OpticalCamera'.",
                "Change 15 (create): 'ConjugatedPortDefinition' is not the metaclass of a package, a definition, a usage or a connector, for example Package, PartDefinition, PartUsage, PortUsage, AttributeUsage, RequirementUsage or InterfaceUsage. The other relationships are built by the server.",
                "Change 16 (create): 'FlowUsage' is not the metaclass of a package, a definition, a usage or a connector, for example Package, PartDefinition, PartUsage, PortUsage, AttributeUsage, RequirementUsage or InterfaceUsage. The other relationships are built by the server.",
                "Change 17 (update): The ends of a connector cannot change: delete the connector and create it again.",
                "Change 18 (update): isEnd only applies to the creation of a feature: delete the feature and create it again.",
                "Change 20 (update): The PortUsage 'bare' has no port definition to conjugate: give its definition.",
                "Change 21 (create): A conjugated port definition cannot be given as a definition: give its port definition with conjugated true.",
                "Change 22 (create): A SatisfyRequirementUsage takes a satisfied requirement and a satisfying part, and optionally an owner, a name and a text.",
                "Change 23 (create): An end of the connector is empty.",
                "Change 24 (create): The definition must be a connection definition, not the PartDefinition 'OpticalCamera'."
            ];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.IsError, Is.True);
                Assert.That(problems, Is.EqualTo(expectedProblems));
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
