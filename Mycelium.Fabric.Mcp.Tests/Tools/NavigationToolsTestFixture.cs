// ------------------------------------------------------------------------------------------------
//  <copyright file="NavigationToolsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Root.Namespaces;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;

    /// <summary>
    /// Suite of tests for the <see cref="NavigationTools"/> class.
    /// </summary>
    [TestFixture]
    public class NavigationToolsTestFixture
    {
        /// <summary>
        /// The <c>ElementId</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string PayloadSubsystemId = "95a8c184-a12e-1125-c0ee-bbe7a025de5b";

        /// <summary>
        /// The <c>ElementId</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string CameraId = "558a0ae4-8585-66a3-9bc6-52322650941c";

        private Mock<IModelProvider> modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new NavigationTools(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new NavigationTools(this.modelProvider.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyGetModelOverview()
        {
            this.modelProvider.Setup(provider => provider.Elements).Returns([]);
            this.modelProvider.Setup(provider => provider.RootElements).Returns([]);

            var tools = new NavigationTools(this.modelProvider.Object);
            var overview = tools.GetModelOverview();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(overview.TopLevelElements, Has.Count.EqualTo(0));
                Assert.That(overview.ElementCount, Is.EqualTo(0));
                Assert.That(overview.NamedElementCount, Is.EqualTo(0));
                Assert.That(overview.MostFrequentTypes, Has.Count.EqualTo(0));
            }

            // A root namespace owns the "EOSat1" package through a membership; an unnamed part completes the model.
            var rootNamespace = new Namespace();
            var membership = new OwningMembership();
            var package = new Package { DeclaredName = "EOSat1" };
            var part = new PartUsage();

            rootNamespace.AssignOwnership(membership, package);

            this.modelProvider.Setup(provider => provider.Elements).Returns([rootNamespace, membership, package, part]);
            this.modelProvider.Setup(provider => provider.RootElements).Returns([rootNamespace]);

            overview = tools.GetModelOverview();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(overview.TopLevelElements, Is.EqualTo(["EOSat1"]));
                Assert.That(overview.ElementCount, Is.EqualTo(4));
                Assert.That(overview.NamedElementCount, Is.EqualTo(1));
                Assert.That(overview.MostFrequentTypes, Has.Count.EqualTo(4));
                Assert.That(overview.MostFrequentTypes["PartUsage"], Is.EqualTo(1));
                this.modelProvider.VerifyGet(provider => provider.Elements, Times.Exactly(2));
                this.modelProvider.VerifyGet(provider => provider.RootElements, Times.Exactly(2));
            }
        }

        [Test]
        public void VerifyFindElementsByName()
        {
            // An unnamed part and 25 named parts, more than the 20 results the tool returns.
            var elements = new List<IElement> { new PartUsage() };
            elements.AddRange(Enumerable.Range(0, 25).Select(index => new PartUsage { DeclaredName = $"part{index:D2}" }));

            this.modelProvider.Setup(provider => provider.Elements).Returns(elements);

            var tools = new NavigationTools(this.modelProvider.Object);
            var result = tools.FindElementsByName("PART");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.FindElementsByName(" "), Throws.TypeOf<McpException>());
                Assert.That(result.TotalFound, Is.EqualTo(25));
                Assert.That(result.Elements, Has.Count.EqualTo(20));
                Assert.That(result.Elements[0].Name, Is.EqualTo("part00"));
                Assert.That(tools.FindElementsByName("unknown").TotalFound, Is.EqualTo(0));
            }

            tools = CreateSatelliteTools();

            var cameras = tools.FindElementsByName("camera");

            Assert.That(cameras.Elements.Select(element => element.Name), Is.SupersetOf(["camera", "OpticalCamera"]));
        }

        [Test]
        public void VerifyGetElementDetails()
        {
            var tools = new NavigationTools(this.modelProvider.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.GetElementDetails("unknown-id"), Throws.TypeOf<McpException>().With.Message.Contains("unknown-id"));
                Assert.That(() => tools.GetElementDetails(" "), Throws.TypeOf<McpException>());
            }

            tools = CreateSatelliteTools();

            var camera = tools.GetElementDetails(CameraId);
            var payloadSubsystem = tools.GetElementDetails(PayloadSubsystemId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(camera.ElementId, Is.EqualTo(CameraId));
                Assert.That(camera.Name, Is.EqualTo("camera"));
                Assert.That(camera.ShortName, Is.Null);
                Assert.That(camera.Type, Is.EqualTo("PartUsage : OpticalCamera"));
                Assert.That(camera.QualifiedName, Is.EqualTo("EOSat1::Architecture::eosat1::payloadSubsystem::camera"));
                Assert.That(camera.OwnerId, Is.EqualTo(PayloadSubsystemId));
                Assert.That(camera.OwnerName, Is.EqualTo("payloadSubsystem"));
                Assert.That(camera.ChildCount, Is.EqualTo(0));
                Assert.That(camera.Documentation, Is.Null);
                Assert.That(payloadSubsystem.Documentation, Is.EqualTo("Payload: the imaging instrument and its data storage."));
            }
        }

        [Test]
        public void VerifyListChildren()
        {
            var tools = new NavigationTools(this.modelProvider.Object);

            Assert.That(() => tools.ListChildren("unknown-id"), Throws.TypeOf<McpException>());

            tools = CreateSatelliteTools();

            var children = tools.ListChildren(PayloadSubsystemId);
            var camera = children.Single(child => child.Name == "camera");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(children.Select(child => child.Name), Is.SupersetOf(["camera", "massMemory"]));
                Assert.That(camera.ElementId, Is.EqualTo(CameraId));
                Assert.That(camera.Type, Is.EqualTo("PartUsage : OpticalCamera"));
                Assert.That(camera.QualifiedName, Is.EqualTo("EOSat1::Architecture::eosat1::payloadSubsystem::camera"));
                Assert.That(tools.ListChildren(CameraId), Is.Empty);
            }
        }

        /// <summary>
        /// Creates <see cref="NavigationTools"/> that work on the <c>Satellite.json</c> test model.
        /// </summary>
        /// <returns>The <see cref="NavigationTools"/> on the loaded model.</returns>
        private static NavigationTools CreateSatelliteTools()
        {
            var satelliteModelProvider = new InMemoryModelProvider();
            satelliteModelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));

            return new NavigationTools(satelliteModelProvider);
        }
    }
}