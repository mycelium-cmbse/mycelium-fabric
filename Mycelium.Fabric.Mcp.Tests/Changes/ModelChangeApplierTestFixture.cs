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
    using SysML2.NET.Core.POCO.Systems.Requirements;
    using SysML2.NET.Core.Root.Namespaces;
    using SysML2.NET.Dal;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoFeatureTyping = SysML2.NET.Core.DTO.Core.Features.FeatureTyping;

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

        [Test]
        public void Apply_WithRequirementChanges_MakesTheRequirementsVerifiable()
        {
            var applier = new ModelChangeApplier(ReadSatelliteDtos());

            var result = applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.CreateRequirement, TemporaryName = "cameraMass", Owner = RequirementsPackageId.ToString(), Name = "cameraMass", ReqId = "REQ-PL-001", Text = "The camera shall weigh at most 40 kg." },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = "cameraMass", Attribute = "mass", Operator = "<", Limit = 10 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = "cameraMass", Attribute = "mass", Operator = "<=", Limit = 40, Margin = 10 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = "cameraMass", SatisfyingPart = CameraId.ToString() },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = EclipseEnergyId.ToString(), Attribute = "capacity", Operator = ">=", Limit = 300, Margin = 20 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = EclipseEnergyId.ToString(), Attribute = "mass", Operator = "==", Limit = 5.8, Margin = 0 }
            ]);

            var elements = Assemble(applier.Elements);
            var cameraMass = (IRequirementUsage)elements[result.CreatedElements[0].Id];
            var satisfy = (ISatisfyRequirementUsage)elements[result.CreatedElements[1].Id];
            var eclipseEnergy = (IRequirementUsage)elements[EclipseEnergyId];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(result.CreatedElements.Select(created => created.Type), Is.EqualTo(["RequirementUsage", "SatisfyRequirementUsage"]));
                Assert.That(result.CreatedElements[1].Name, Is.Null);
                Assert.That(cameraMass.ReqId, Is.EqualTo("REQ-PL-001"));
                Assert.That(cameraMass.shortName, Is.EqualTo("REQ-PL-001"));
                Assert.That(cameraMass.subjectParameter.DeclaredName, Is.EqualTo("subj"));
                Assert.That(cameraMass.subjectParameter.feature.Select(feature => feature.DeclaredName), Is.EqualTo(["mass"]));
                Assert.That(cameraMass.requiredConstraint, Has.Count.EqualTo(1));
                Assert.That(cameraMass.requiredConstraint[0].GetAttributeConstraint().ToString(), Is.EqualTo("subj.mass * 1.1 <= 40"));
                Assert.That(satisfy.owner, Is.SameAs(cameraMass.owner));
                Assert.That(satisfy.satisfiedRequirement, Is.SameAs(cameraMass));
                Assert.That(satisfy.ResolveSatisfyingFeature(), Is.SameAs(elements[CameraId]));
                Assert.That(eclipseEnergy.subjectParameter.feature.Select(feature => feature.DeclaredName), Is.EqualTo(["capacity", "mass"]));
                Assert.That(eclipseEnergy.requiredConstraint[0].GetAttributeConstraint().ToString(), Is.EqualTo("subj.mass == 5.8"));
            }

            // The constraints are replaced, and the attributes are reused: the one of the subject, or the one of its definition
            // when the subject of cameraMass is typed by OpticalCamera.
            var dtos = applier.Elements.ToList();
            var subjectTyping = new DtoFeatureTyping { Id = Guid.NewGuid(), TypedFeature = cameraMass.subjectParameter.Id, Type = dtos.Single(dto => dto.DeclaredName == "OpticalCamera").Id };
            subjectTyping.OwningRelatedElement = subjectTyping.TypedFeature;
            dtos.Single(dto => dto.Id == subjectTyping.TypedFeature).OwnedRelationship.Add(subjectTyping.Id);
            dtos.Add(subjectTyping);

            applier = new ModelChangeApplier(dtos);

            var replacement = applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = EclipseEnergyId.ToString(), Attribute = "capacity", Operator = ">=", Limit = 300, Margin = 20 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = cameraMass.Id.ToString(), Attribute = "power", Operator = "<=", Limit = 60 }
            ]);

            elements = Assemble(applier.Elements);
            eclipseEnergy = (IRequirementUsage)elements[EclipseEnergyId];
            cameraMass = (IRequirementUsage)elements[cameraMass.Id];

            using (Assert.EnterMultipleScope())
            {
                Assert.That(replacement.Applied, Is.True);
                Assert.That(eclipseEnergy.subjectParameter.feature, Has.Count.EqualTo(2));
                Assert.That(eclipseEnergy.requiredConstraint, Has.Count.EqualTo(1));
                Assert.That(eclipseEnergy.requiredConstraint[0].GetAttributeConstraint().ToString(), Is.EqualTo("subj.capacity >= 300 * 1.2"));
                Assert.That(cameraMass.subjectParameter.ownedFeature.Select(feature => feature.DeclaredName), Is.EqualTo(["mass"]));
                Assert.That(cameraMass.requiredConstraint[0].GetAttributeConstraint().ToString(), Is.EqualTo("subj.power <= 60"));
            }

            // A part that satisfies a requirement can only be deleted with its satisfy link.
            var refusal = new ModelChangeApplier(applier.Elements).Apply([new ModelChange { Kind = ChangeKind.Delete, Element = CameraId.ToString() }]);

            applier = new ModelChangeApplier(applier.Elements);
            var deletion = applier.Apply(
            [
                new ModelChange { Kind = ChangeKind.Delete, Element = satisfy.Id.ToString() },
                new ModelChange { Kind = ChangeKind.Delete, Element = CameraId.ToString() }
            ]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refusal.Applied, Is.False);
                Assert.That(refusal.Problems[0], Does.EndWith($"is still referenced by the SatisfyRequirementUsage {satisfy.Id}. Change or delete these elements first."));
                Assert.That(deletion.Applied, Is.True);
                Assert.That(Assemble(applier.Elements), Does.Not.ContainKey(satisfy.Id));
            }
        }

        [Test]
        public void Apply_WithInvalidRequirementChanges_ReturnsEveryProblem()
        {
            var creation = new ModelChangeApplier(ReadSatelliteDtos());
            var satisfyId = creation.Apply([new ModelChange { Kind = ChangeKind.Satisfy, Element = MassBudgetId.ToString(), SatisfyingPart = CameraId.ToString() }]).CreatedElements[0].Id;

            var result = new ModelChangeApplier(creation.Elements).Apply(
            [
                new ModelChange { Kind = ChangeKind.CreateRequirement, Owner = RequirementsPackageId.ToString(), Name = "cameraMass", ReqId = "REQ-SYS-001", Text = "The camera shall weigh at most 40 kg." },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = CameraId.ToString(), Attribute = "mass", Operator = "<=", Limit = 150 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = satisfyId.ToString(), Attribute = "mass", Operator = "<=", Limit = 150 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = MassBudgetId.ToString(), Operator = "<=", Limit = 150 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = MassBudgetId.ToString(), Attribute = "mass", Operator = "=<", Limit = 150 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = MassBudgetId.ToString(), Attribute = "mass", Operator = "<=" },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = MassBudgetId.ToString(), Attribute = "mass", Operator = "<=", Limit = 150, Margin = -5 },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = MassBudgetId.ToString(), Attribute = "mass", Operator = "==", Limit = 150, Margin = 20 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = MassBudgetId.ToString(), SatisfyingPart = RequirementsPackageId.ToString() },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = MassBudgetId.ToString(), SatisfyingPart = CameraId.ToString() },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = MassBudgetId.ToString(), SatisfyingPart = PayloadSubsystemId.ToString() }
            ]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.False);
                Assert.That(result.Problems, Has.Count.EqualTo(10));
                Assert.That(result.Problems[0], Is.EqualTo($"Change 1 (CreateRequirement): The ReqId 'REQ-SYS-001' is already used by the RequirementUsage 'massBudget', whose identifier is {MassBudgetId}."));
                Assert.That(result.Problems[1], Is.EqualTo("Change 2 (SetConstraint): The element must be a requirement, not the PartUsage 'camera'."));
                Assert.That(result.Problems[2], Is.EqualTo($"Change 3 (SetConstraint): The element must be a requirement, not the SatisfyRequirementUsage {satisfyId}."));
                Assert.That(result.Problems[3], Is.EqualTo("Change 4 (SetConstraint): The attribute is missing."));
                Assert.That(result.Problems[4], Is.EqualTo("Change 5 (SetConstraint): The operator '=<' is not supported. Use '<', '<=', '>', '>=', '=='."));
                Assert.That(result.Problems[5], Is.EqualTo("Change 6 (SetConstraint): The limit is missing."));
                Assert.That(result.Problems[6], Is.EqualTo("Change 7 (SetConstraint): The margin must be 0 or greater."));
                Assert.That(result.Problems[7], Is.EqualTo("Change 8 (SetConstraint): A margin cannot be applied with '=='. Use '<=' or '>=' instead."));
                Assert.That(result.Problems[8], Is.EqualTo("Change 9 (Satisfy): The satisfying part must be a part, not the Package 'Requirements'."));
                Assert.That(result.Problems[9], Is.EqualTo("Change 10 (Satisfy): The RequirementUsage 'massBudget' is already satisfied by the PartUsage 'camera'."));
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
