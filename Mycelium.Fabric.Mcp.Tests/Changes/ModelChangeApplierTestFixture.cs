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

    /// <summary>
    /// Suite of tests for the <see cref="ModelChangeApplier"/> class.
    /// </summary>
    [TestFixture]
    public class ModelChangeApplierTestFixture
    {
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

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ModelChangeApplier(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new ModelChangeApplier([]).Elements, Has.Count.EqualTo(0));
                Assert.That(new ModelChangeApplier(ReadSatelliteDtos()).Elements, Has.Count.EqualTo(548));
            }
        }

        [Test]
        public void Apply_WithCreations_BuildsTheElementsAndTheirRelationships()
        {
            var applier = new ModelChangeApplier([]);

            var result = applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.CreatePackage, TemporaryName = "spacecraft", Name = "Spacecraft" },
                new ModelChange { Kind = ChangeKind.CreatePackage, TemporaryName = "requirements", Owner = "spacecraft", Name = "Requirements" },
                new ModelChange { Kind = ChangeKind.CreatePartDefinition, TemporaryName = "Camera", Owner = "spacecraft", Name = "Camera", Text = "Optical camera." },
                new ModelChange { Kind = ChangeKind.CreateAttribute, Owner = "Camera", Name = "mass", Value = 38, Text = "Dry mass of the unit [kg]." },
                new ModelChange { Kind = ChangeKind.CreatePart, TemporaryName = "camera", Owner = "spacecraft", Name = "camera", Definition = "Camera" },
                new ModelChange { Kind = ChangeKind.CreatePart, Owner = "camera", Name = "lens" },
                new ModelChange { Kind = ChangeKind.CreateRequirement, Owner = "requirements", Name = "cameraMass", Text = "The camera shall weigh less than 40 kg." }
            ]);

            var elements = Assemble(applier.Elements);
            var createdIds = result.CreatedElements.ToDictionary(created => created.Name, created => created.Id);
            var package = elements[createdIds["Spacecraft"]];
            var definition = (IType)elements[createdIds["Camera"]];
            var mass = (IAttributeUsage)elements[createdIds["mass"]];
            var camera = (IPartUsage)elements[createdIds["camera"]];
            var lens = elements[createdIds["lens"]];
            var requirement = elements[createdIds["cameraMass"]];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(result.Problems, Has.Count.EqualTo(0));
                Assert.That(result.CreatedElements.Select(created => created.Type),
                    Is.EqualTo(["Package", "Package", "PartDefinition", "AttributeUsage", "PartUsage", "PartUsage", "RequirementUsage"]));
                Assert.That(result.CreatedElements[0].TemporaryName, Is.EqualTo("spacecraft"));
                Assert.That(result.CreatedElements[3].TemporaryName, Is.Null);
                Assert.That(package.owner, Is.TypeOf<Namespace>());
                Assert.That(package.owner.owner, Is.Null);
                Assert.That(package.qualifiedName, Is.EqualTo("Spacecraft"));
                Assert.That(definition.GetDocumentationBodies(), Is.EqualTo("Optical camera."));
                Assert.That(definition.feature, Is.EqualTo([mass]));
                Assert.That(mass.GetNumericValue(), Is.EqualTo(38));
                Assert.That(mass.GetDocumentationBodies(), Is.EqualTo("Dry mass of the unit [kg]."));
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
        public void Apply_WithModifications_UpdatesTheElements()
        {
            var creation = new ModelChangeApplier([]);

            var createdIds = creation.Apply(
            [
                new ModelChange { Kind = ChangeKind.CreatePackage, TemporaryName = "spacecraft", Name = "Spacecraft" },
                new ModelChange { Kind = ChangeKind.CreatePartDefinition, TemporaryName = "Camera", Owner = "spacecraft", Name = "Camera" },
                new ModelChange { Kind = ChangeKind.CreateAttribute, Owner = "Camera", Name = "mass", Value = 38 },
                new ModelChange { Kind = ChangeKind.CreatePart, Owner = "spacecraft", Name = "camera", Definition = "Camera" }
            ]).CreatedElements.ToDictionary(created => created.Name, created => created.Id.ToString());

            var applier = new ModelChangeApplier(creation.Elements);
            var elementCount = applier.Elements.Count;

            var result = applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.CreatePartDefinition, TemporaryName = "SmallCamera", Owner = createdIds["Spacecraft"], Name = "SmallCamera" },
                new ModelChange { Kind = ChangeKind.SetDefinition, Element = createdIds["camera"], Definition = "SmallCamera" },
                new ModelChange { Kind = ChangeKind.SetValue, Element = createdIds["mass"], Value = 35.5 },
                new ModelChange { Kind = ChangeKind.Rename, Element = createdIds["camera"], Name = "mainCamera" },
                new ModelChange { Kind = ChangeKind.Rename, Element = createdIds["Camera"], Name = "Camera" }
            ]);

            var elements = Assemble(applier.Elements);
            var camera = elements[Guid.Parse(createdIds["camera"])];
            var mass = (IAttributeUsage)elements[Guid.Parse(createdIds["mass"])];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(result.CreatedElements.Select(created => created.Name), Is.EqualTo(["SmallCamera"]));
                Assert.That(camera.DeclaredName, Is.EqualTo("mainCamera"));
                Assert.That(camera.GetTypeNames(), Is.EqualTo(["SmallCamera"]));
                Assert.That(mass.GetNumericValue(), Is.EqualTo(35.5));
                Assert.That(applier.Elements, Has.Count.EqualTo(elementCount + 2));
            }
        }

        [Test]
        public void Apply_WithDeletions_RemovesTheElementsUnlessTheyAreStillReferenced()
        {
            var dtos = ReadSatelliteDtos();
            var opticalCameraId = dtos.Single(dto => dto.DeclaredName == "OpticalCamera").Id.ToString();
            var applier = new ModelChangeApplier(dtos);

            var refusal = applier.Apply([new ModelChange { Kind = ChangeKind.Delete, Element = opticalCameraId }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refusal.Applied, Is.False);
                Assert.That(refusal.Problems, Has.Count.EqualTo(1));
                Assert.That(refusal.Problems[0], Does.StartWith("Change 1 (Delete): the PartDefinition 'OpticalCamera' is still referenced by"));
                Assert.That(refusal.Problems[0], Does.Contain("'EOSat1::Architecture::eosat1::payloadSubsystem::camera'"));
                Assert.That(applier.Elements, Has.Count.EqualTo(548));
            }

            applier = new ModelChangeApplier(ReadSatelliteDtos());

            var result = applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.Delete, Element = CameraId.ToString() },
                new ModelChange { Kind = ChangeKind.Delete, Element = opticalCameraId },
                new ModelChange { Kind = ChangeKind.CreatePart, Owner = PayloadSubsystemId.ToString(), Name = "camera" }
            ]);

            var elements = Assemble(applier.Elements);
            var payloadParts = elements[PayloadSubsystemId].ownedElement.OfType<IPartUsage>().Select(part => part.DeclaredName);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(elements, Does.Not.ContainKey(CameraId));
                Assert.That(elements, Does.Not.ContainKey(Guid.Parse(opticalCameraId)));
                Assert.That(payloadParts, Is.EquivalentTo(["massMemory", "camera"]));
                Assert.That(elements, Has.Count.EqualTo(531));
            }
        }

        [Test]
        public void Apply_WithInvalidChanges_ReturnsEveryProblem()
        {
            var dtos = ReadSatelliteDtos();
            var opticalCameraId = dtos.Single(dto => dto.DeclaredName == "OpticalCamera").Id.ToString();
            var applier = new ModelChangeApplier(dtos);

            Assert.That(() => applier.Apply(null), Throws.TypeOf<ArgumentNullException>());

            var result = applier.Apply(
            [
                null,
                new ModelChange { Kind = ChangeKind.CreatePackage, Name = " " },
                new ModelChange { Kind = ChangeKind.CreatePartDefinition, Name = "Lens" },
                new ModelChange { Kind = ChangeKind.CreatePartDefinition, Owner = CameraId.ToString().Replace('5', '6'), Name = "Lens" },
                new ModelChange { Kind = ChangeKind.CreatePart, TemporaryName = "lens", Owner = "optics", Name = "lens" },
                new ModelChange { Kind = ChangeKind.CreateAttribute, Owner = "lens", Name = "mass" },
                new ModelChange { Kind = ChangeKind.CreateAttribute, Owner = PackageId.ToString(), Name = "mass" },
                new ModelChange { Kind = ChangeKind.CreatePart, Owner = PayloadSubsystemId.ToString(), Name = "camera" },
                new ModelChange { Kind = ChangeKind.CreatePackage, TemporaryName = PackageId.ToString(), Name = "Optics" },
                new ModelChange { Kind = ChangeKind.CreatePackage, TemporaryName = "optics", Name = "Optics" },
                new ModelChange { Kind = ChangeKind.CreatePackage, TemporaryName = "optics", Name = "Optics2" },
                new ModelChange { Kind = ChangeKind.CreateRequirement, Owner = PackageId.ToString(), Name = "cameraMass" },
                new ModelChange { Kind = ChangeKind.CreateAttribute, TemporaryName = "focalLength", Owner = opticalCameraId, Name = "focalLength" },
                new ModelChange { Kind = ChangeKind.SetValue, Element = "focalLength" },
                new ModelChange { Kind = ChangeKind.SetValue, Element = CameraId.ToString(), Value = 35 },
                new ModelChange { Kind = ChangeKind.SetDefinition, Element = CameraId.ToString(), Definition = PackageId.ToString() },
                new ModelChange { Kind = ChangeKind.Delete, Element = PayloadSubsystemId.ToString() },
                new ModelChange { Kind = ChangeKind.Rename, Element = CameraId.ToString(), Name = "mainCamera" },
                new ModelChange { Kind = (ChangeKind)99 }
            ]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.False);
                Assert.That(result.CreatedElements, Has.Count.EqualTo(0));
                Assert.That(result.Problems, Has.Count.EqualTo(16));
                Assert.That(result.Problems[0], Is.EqualTo("Change 1: the change is empty."));
                Assert.That(result.Problems[1], Is.EqualTo("Change 2 (CreatePackage): The name is missing."));
                Assert.That(result.Problems[2], Is.EqualTo("Change 3 (CreatePartDefinition): The owner is missing."));
                Assert.That(result.Problems[3], Does.StartWith("Change 4 (CreatePartDefinition): No element has the identifier"));
                Assert.That(result.Problems[4], Does.StartWith("Change 5 (CreatePart): The owner 'optics' is neither the identifier"));
                Assert.That(result.Problems[5], Does.StartWith("Change 6 (CreateAttribute): The owner 'lens' has not been created"));
                Assert.That(result.Problems[6], Is.EqualTo("Change 7 (CreateAttribute): The owner must be a part definition or a part, not the Package 'EOSat1'."));
                Assert.That(result.Problems[7], Does.StartWith($"Change 8 (CreatePart): The PartUsage 'payloadSubsystem' already has a member named 'camera', whose identifier is {CameraId}"));
                Assert.That(result.Problems[8], Does.EndWith("must not be an identifier."));
                Assert.That(result.Problems[9], Is.EqualTo("Change 11 (CreatePackage): The temporary name 'optics' is already used by a previous change."));
                Assert.That(result.Problems[10], Is.EqualTo("Change 12 (CreateRequirement): The text of the requirement is missing."));
                Assert.That(result.Problems[11], Is.EqualTo("Change 14 (SetValue): The value is missing."));
                Assert.That(result.Problems[12], Is.EqualTo("Change 15 (SetValue): The element must be an attribute, not the PartUsage 'camera'."));
                Assert.That(result.Problems[13], Is.EqualTo("Change 16 (SetDefinition): The definition must be a part definition, not the Package 'EOSat1'."));
                Assert.That(result.Problems[14], Does.StartWith("Change 18 (Rename): The element").And.EndWith("is deleted by a previous change."));
                Assert.That(result.Problems[15], Is.EqualTo("Change 19 (99): '99' is not a kind of change."));
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
