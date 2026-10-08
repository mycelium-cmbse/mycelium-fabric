// ------------------------------------------------------------------------------------------------
//  <copyright file="InMemoryModelProviderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ErrorOr;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    using DtoAttributeUsage = SysML2.NET.Core.DTO.Systems.Attributes.AttributeUsage;
    using DtoDocumentation = SysML2.NET.Core.DTO.Root.Annotations.Documentation;
    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoFeatureMembership = SysML2.NET.Core.DTO.Core.Types.FeatureMembership;
    using DtoNamespace = SysML2.NET.Core.DTO.Root.Namespaces.Namespace;
    using DtoOwningMembership = SysML2.NET.Core.DTO.Root.Namespaces.OwningMembership;
    using DtoPartUsage = SysML2.NET.Core.DTO.Systems.Parts.PartUsage;
    using DtoReferenceUsage = SysML2.NET.Core.DTO.Systems.DefinitionAndUsage.ReferenceUsage;
    using DtoRelationship = SysML2.NET.Core.DTO.Root.Elements.IRelationship;
    using DtoSubjectMembership = SysML2.NET.Core.DTO.Systems.Requirements.SubjectMembership;
    using Error = ErrorOr.Error;

    /// <summary>
    /// Suite of tests for the <see cref="InMemoryModelProvider"/> class.
    /// </summary>
    [TestFixture]
    public class InMemoryModelProviderTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        private string dataDirectory;

        private Uri satelliteModelPath;

        private Uri emptyModelPath;

        private Mock<IModelChangeApplier> changeApplier;

        private InMemoryModelProvider modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.dataDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
            this.satelliteModelPath = new Uri(Path.Combine(this.dataDirectory, "Satellite.json"));
            this.emptyModelPath = new Uri(Path.Combine(this.dataDirectory, "Empty.json"));

            this.changeApplier = new Mock<IModelChangeApplier>();
            this.modelProvider = new InMemoryModelProvider(this.changeApplier.Object);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new InMemoryModelProvider(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(0));
                Assert.That(this.modelProvider.RootElements, Has.Count.EqualTo(0));
                Assert.That(this.modelProvider.GetElementById(PayloadSubsystemId), Is.Null);
            }
        }

        [Test]
        public void VerifyLoadModel()
        {
            var missingModelPath = new Uri(Path.Combine(this.dataDirectory, "Missing.json"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.modelProvider.LoadModel(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.modelProvider.LoadModel(new Uri("Satellite.json", UriKind.Relative)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.LoadModel(new Uri("https://example.com/Satellite.json")), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.LoadModel(missingModelPath), Throws.TypeOf<FileNotFoundException>());
                Assert.That(() => this.modelProvider.LoadModel(this.satelliteModelPath), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyElements()
        {
            this.modelProvider.LoadModel(this.satelliteModelPath);

            Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(548));

            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(0));
        }

        [Test]
        public void VerifyRootElements()
        {
            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.RootElements, Has.Count.EqualTo(0));

            this.modelProvider.LoadModel(this.satelliteModelPath);
            var rootElements = this.modelProvider.RootElements;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rootElements, Has.Count.EqualTo(1));
                Assert.That(rootElements[0], Is.InstanceOf<INamespace>());
                Assert.That(rootElements[0].OwningRelationship, Is.Null);
            }
        }

        [Test]
        public void VerifyGetElementById()
        {
            this.modelProvider.LoadModel(this.satelliteModelPath);

            var payloadSubsystem = this.modelProvider.GetElementById(PayloadSubsystemId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.modelProvider.GetElementById(Guid.NewGuid()), Is.Null);
                Assert.That(payloadSubsystem, Is.Not.Null);
                Assert.That(payloadSubsystem.Id, Is.EqualTo(PayloadSubsystemId));
                Assert.That(payloadSubsystem.DeclaredName, Is.EqualTo("payloadSubsystem"));
            }

            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.GetElementById(PayloadSubsystemId), Is.Null);
        }

        [Test]
        public void VerifyGetRequiredElementById()
        {
            this.modelProvider.LoadModel(this.satelliteModelPath);

            var unknownId = Guid.NewGuid();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.modelProvider.GetRequiredElementById(unknownId), Throws.TypeOf<McpException>().With.Message.Contains(unknownId.ToString()));
                Assert.That(this.modelProvider.GetRequiredElementById(PayloadSubsystemId).DeclaredName, Is.EqualTo("payloadSubsystem"));
            }
        }

        [Test]
        public void VerifyApplyChanges()
        {
            Assert.That(() => this.modelProvider.ApplyChanges(null), Throws.TypeOf<ArgumentNullException>());

            this.modelProvider.LoadModel(this.satelliteModelPath);

            var elements = this.modelProvider.Elements;
            var camera = this.modelProvider.GetElementById(CameraId);
            IReadOnlyList<ModelChange> changes = [new ModelChange { Identity = CameraId.ToString(), Payload = new ElementPayload { Name = "mainCamera" } }];

            ErrorOr<CommitRequest> problem = Error.Validation(description: "Change 1 (update): a problem.");
            this.changeApplier.Setup(applier => applier.Apply(It.IsAny<IReadOnlyCollection<DtoElement>>(), changes)).Returns(problem);

            var refusal = this.modelProvider.ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refusal.Applied, Is.False);
                Assert.That(refusal.CreatedElements, Has.Count.EqualTo(0));
                Assert.That(refusal.Problems, Is.EqualTo(["Change 1 (update): a problem."]));
                Assert.That(this.modelProvider.Elements, Is.SameAs(elements));
            }

            // The commit renames the camera and gives it a lens, with what the server builds around: memberships, a
            // documentation, and a subject that is not created by a change.
            var renamedCameraDto = ReadRenamedCamera();
            var commitRequest = CreateCommitRequest(CreateDataVersion(CameraId, renamedCameraDto));
            var lens = AddOwnedElement(commitRequest, renamedCameraDto, new DtoFeatureMembership(), new DtoPartUsage { DeclaredName = "lens" });
            var lensMass = AddOwnedElement(commitRequest, lens, new DtoFeatureMembership(), new DtoAttributeUsage { DeclaredName = "mass" });
            AddOwnedElement(commitRequest, lens, new DtoOwningMembership(), new DtoDocumentation { Body = "Main lens." });
            var subject = AddOwnedElement(commitRequest, lens, new DtoSubjectMembership(), new DtoReferenceUsage { DeclaredName = "subj" });
            AddOwnedElement(commitRequest, subject, new DtoFeatureMembership(), new DtoAttributeUsage { DeclaredName = "mass" });

            this.changeApplier.Setup(applier => applier.Apply(It.IsAny<IReadOnlyCollection<DtoElement>>(), changes)).Returns(commitRequest);

            var result = this.modelProvider.ApplyChanges(changes);
            var renamedCamera = this.modelProvider.GetElementById(CameraId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(result.Problems, Has.Count.EqualTo(0));

                Assert.That(result.CreatedElements, Is.EqualTo(
                [
                    new CreatedElement(lens.Id, "EOSat1::Architecture::eosat1::payloadSubsystem::mainCamera::lens", "PartUsage"),
                    new CreatedElement(lensMass.Id, "EOSat1::Architecture::eosat1::payloadSubsystem::mainCamera::lens::mass", "AttributeUsage")
                ]));

                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(558));
                Assert.That(renamedCamera.qualifiedName, Is.EqualTo("EOSat1::Architecture::eosat1::payloadSubsystem::mainCamera"));
                Assert.That(renamedCamera, Is.Not.SameAs(camera));
                Assert.That(camera.DeclaredName, Is.EqualTo("camera"));
            }

            this.changeApplier.Verify(applier => applier.Apply(It.Is<IReadOnlyCollection<DtoElement>>(model => model.Count == 548), changes), Times.Exactly(2));
        }

        [Test]
        public void VerifyCreateCommit()
        {
            Assert.That(() => this.modelProvider.CreateCommit(null), Throws.TypeOf<ArgumentNullException>());

            this.modelProvider.LoadModel(this.satelliteModelPath);

            var renamedCamera = ReadRenamedCamera();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.modelProvider.CreateCommit(new CommitRequest { Change = null }), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.CreateCommit(CreateCommitRequest((DataVersionRequest)null)), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.CreateCommit(CreateCommitRequest(new DataVersionRequest { Payload = renamedCamera })), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.CreateCommit(CreateCommitRequest(CreateDataVersion(Guid.NewGuid(), renamedCamera))), Throws.TypeOf<ArgumentException>());

                Assert.That(() => this.modelProvider.CreateCommit(CreateCommitRequest(CreateDataVersion(CameraId, renamedCamera), new DataVersionRequest { Identity = new DataIdentityRequest(), Payload = new ExternalDataRequest() })),
                    Throws.TypeOf<ArgumentException>());

                Assert.That(this.modelProvider.GetElementById(CameraId).DeclaredName, Is.EqualTo("camera"));
            }

            var firstRequest = CreateCommitRequest(CreateDataVersion(CameraId, renamedCamera));
            firstRequest.Name = "Rename the camera";

            var firstCommit = this.modelProvider.CreateCommit(firstRequest);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstCommit.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(firstCommit.Name, Is.EqualTo("Rename the camera"));
                Assert.That(firstCommit.PreviousCommit, Has.Count.EqualTo(0));
                Assert.That(this.modelProvider.GetElementById(CameraId).DeclaredName, Is.EqualTo("mainCamera"));
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(548));
            }

            var newNamespace = new DtoNamespace { Id = Guid.NewGuid() };
            var secondCommit = this.modelProvider.CreateCommit(CreateCommitRequest(CreateDataVersion(newNamespace.Id, newNamespace)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(secondCommit.PreviousCommit, Is.EqualTo([firstCommit.Id]));
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(549));
                Assert.That(this.modelProvider.RootElements, Has.Count.EqualTo(2));
            }

            var thirdCommit = this.modelProvider.CreateCommit(CreateCommitRequest(CreateDataVersion(newNamespace.Id, null)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(thirdCommit.PreviousCommit, Is.EqualTo([secondCommit.Id]));
                Assert.That(this.modelProvider.GetElementById(newNamespace.Id), Is.Null);
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(548));
            }

            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.CreateCommit(new CommitRequest()).PreviousCommit, Has.Count.EqualTo(0));
        }

        /// <summary>
        /// Creates a <see cref="CommitRequest"/> with the given change.
        /// </summary>
        /// <param name="change">The <see cref="DataVersionRequest"/> records of the commit.</param>
        /// <returns>The <see cref="CommitRequest"/>.</returns>
        private static CommitRequest CreateCommitRequest(params DataVersionRequest[] change)
        {
            return new CommitRequest { Change = [.. change] };
        }

        /// <summary>
        /// Adds to a <see cref="CommitRequest"/> a new element owned by another element of the commit through a new
        /// membership, with both sides of each link set.
        /// </summary>
        /// <typeparam name="T">The type of the new element.</typeparam>
        /// <param name="commitRequest">The <see cref="CommitRequest"/> to complete.</param>
        /// <param name="owner">The DTO of the owner, which is a payload of the commit.</param>
        /// <param name="membership">The new membership, which owns the element.</param>
        /// <param name="element">The new element.</param>
        /// <returns>The new element, with its <c>Id</c>.</returns>
        private static T AddOwnedElement<T>(CommitRequest commitRequest, DtoElement owner, DtoRelationship membership, T element) where T : DtoElement
        {
            membership.Id = Guid.NewGuid();
            element.Id = Guid.NewGuid();

            owner.OwnedRelationship.Add(membership.Id);
            membership.OwningRelatedElement = owner.Id;
            membership.OwnedRelatedElement.Add(element.Id);
            element.OwningRelationship = membership.Id;

            commitRequest.Change.Add(CreateDataVersion(membership.Id, membership));
            commitRequest.Change.Add(CreateDataVersion(element.Id, element));

            return element;
        }

        /// <summary>
        /// Creates a <see cref="DataVersionRequest"/> of an element.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <param name="payload">The DTO of the element, or <c>null</c> to delete it.</param>
        /// <returns>The <see cref="DataVersionRequest"/>.</returns>
        private static DataVersionRequest CreateDataVersion(Guid elementId, DtoElement payload)
        {
            return new DataVersionRequest { Identity = new DataIdentityRequest { Id = elementId }, Payload = payload };
        }

        /// <summary>
        /// Reads the DTO of the <c>camera</c> part of <c>Satellite.json</c>, renamed <c>mainCamera</c>.
        /// </summary>
        /// <returns>The renamed DTO.</returns>
        private static DtoElement ReadRenamedCamera()
        {
            using var stream = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json"));

            var camera = new DeSerializer()
                .DeSerialize(stream, SerializationModeKind.JSON, SerializationTargetKind.PSM, false)
                .OfType<DtoElement>()
                .Single(element => element.Id == CameraId);

            camera.DeclaredName = "mainCamera";

            return camera;
        }
    }
}
