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

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.Serializer.Json;

    using DtoElement = SysML2.NET.Core.DTO.Root.Elements.IElement;
    using DtoNamespace = SysML2.NET.Core.DTO.Root.Namespaces.Namespace;

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

            this.changeApplier
                .Setup(applier => applier.Apply(It.IsAny<IReadOnlyCollection<DtoElement>>(), changes))
                .Returns(new PendingCommit([], [], ["Change 1 (update): a problem."]));

            var refusal = this.modelProvider.ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refusal.Applied, Is.False);
                Assert.That(refusal.Problems, Is.EqualTo(["Change 1 (update): a problem."]));
                Assert.That(this.modelProvider.Elements, Is.SameAs(elements));
            }

            var createdElement = new CreatedElement("lens", Guid.NewGuid(), "lens", "PartUsage");

            this.changeApplier
                .Setup(applier => applier.Apply(It.IsAny<IReadOnlyCollection<DtoElement>>(), changes))
                .Returns(new PendingCommit([CreateDataVersion(CameraId, ReadRenamedCamera())], [createdElement], []));

            var result = this.modelProvider.ApplyChanges(changes);
            var renamedCamera = this.modelProvider.GetElementById(CameraId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(result.CreatedElements, Is.EqualTo([createdElement]));
                Assert.That(result.Problems, Has.Count.EqualTo(0));
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(548));
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
                Assert.That(() => this.modelProvider.CreateCommit([null]), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.CreateCommit([new DataVersion { Payload = renamedCamera }]), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.CreateCommit([CreateDataVersion(Guid.NewGuid(), renamedCamera)]), Throws.TypeOf<ArgumentException>());
                Assert.That(() => this.modelProvider.CreateCommit([CreateDataVersion(CameraId, renamedCamera), new DataVersion { Identity = new DataIdentity(), Payload = new Commit() }]), Throws.TypeOf<ArgumentException>());
                Assert.That(this.modelProvider.GetElementById(CameraId).DeclaredName, Is.EqualTo("camera"));
            }

            var cameraVersion = CreateDataVersion(CameraId, renamedCamera);
            var firstCommit = this.modelProvider.CreateCommit([cameraVersion]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(firstCommit.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(firstCommit.PreviousCommit, Is.EqualTo(Guid.Empty));
                Assert.That(cameraVersion.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(cameraVersion.Commit, Is.EqualTo(firstCommit.Id));
                Assert.That(this.modelProvider.GetElementById(CameraId).DeclaredName, Is.EqualTo("mainCamera"));
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(548));
            }

            var newNamespace = new DtoNamespace { Id = Guid.NewGuid() };
            var secondCommit = this.modelProvider.CreateCommit([CreateDataVersion(newNamespace.Id, newNamespace)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(secondCommit.PreviousCommit, Is.EqualTo(firstCommit.Id));
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(549));
                Assert.That(this.modelProvider.RootElements, Has.Count.EqualTo(2));
            }

            var thirdCommit = this.modelProvider.CreateCommit([CreateDataVersion(newNamespace.Id, null)]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(thirdCommit.PreviousCommit, Is.EqualTo(secondCommit.Id));
                Assert.That(this.modelProvider.GetElementById(newNamespace.Id), Is.Null);
                Assert.That(this.modelProvider.Elements, Has.Count.EqualTo(548));
            }

            this.modelProvider.LoadModel(this.emptyModelPath);

            Assert.That(this.modelProvider.CreateCommit([]).PreviousCommit, Is.EqualTo(Guid.Empty));
        }

        /// <summary>
        /// Creates a <see cref="DataVersion"/> of an element.
        /// </summary>
        /// <param name="elementId">The <c>Id</c> of the element.</param>
        /// <param name="payload">The DTO of the element, or <c>null</c> to delete it.</param>
        /// <returns>The <see cref="DataVersion"/>.</returns>
        private static DataVersion CreateDataVersion(Guid elementId, DtoElement payload)
        {
            return new DataVersion { Identity = new DataIdentity { Id = elementId }, Payload = payload };
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
