// ------------------------------------------------------------------------------------------------
//  <copyright file="BudgetToolsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Tools
{
    using System;
    using System.IO;
    using System.Linq;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Core.Types;
    using SysML2.NET.Core.POCO.Kernel.Expressions;
    using SysML2.NET.Core.POCO.Kernel.FeatureValues;
    using SysML2.NET.Core.POCO.Systems.Attributes;
    using SysML2.NET.Core.POCO.Systems.Parts;
    using SysML2.NET.Extensions;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="BudgetTools"/> class.
    /// </summary>
    [TestFixture]
    public class BudgetToolsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        /// <summary>
        /// The <c>Id</c> of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid CameraId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        /// <summary>
        /// The <c>Id</c> of the <c>EOSat1</c> package in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PackageId = Guid.Parse("9d4b1232-b67b-5023-5163-7bdc6de8ff50");

        /// <summary>
        /// The qualified name of the <c>camera</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string CameraQualifiedName = "EOSat1::Architecture::eosat1::payloadSubsystem::camera";

        private Mock<IModelProvider> modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
            this.modelProvider.Setup(provider => provider.GetRequiredElementById(It.IsAny<Guid>())).Throws(new McpException("No element has this identifier."));
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new BudgetTools(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new BudgetTools(this.modelProvider.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyGetAttributeValues()
        {
            var tools = new BudgetTools(this.modelProvider.Object);

            Assert.That(() => tools.GetAttributeValues(Guid.NewGuid()), Throws.TypeOf<McpException>());

            tools = CreateSatelliteTools();

            var camera = tools.GetAttributeValues(CameraId);
            var package = tools.GetAttributeValues(PackageId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(package.Types, Is.Empty);
                Assert.That(package.Attributes, Has.Count.EqualTo(0));
                Assert.That(camera.Id, Is.EqualTo(CameraId));
                Assert.That(camera.Name, Is.EqualTo("camera"));
                Assert.That(camera.Types, Is.EqualTo(["OpticalCamera"]));
                Assert.That(camera.Attributes.Select(attribute => attribute.Name), Is.EqualTo(["mass", "power"]));
                Assert.That(camera.Attributes[0].Value, Is.EqualTo(38));
                Assert.That(camera.Attributes[0].Documentation, Is.EqualTo("Dry mass of the unit [kg]."));
                Assert.That(camera.Attributes[1].Value, Is.EqualTo(55));
            }
        }

        [Test]
        public void VerifySumAttribute()
        {
            // An untyped part that owns its own mass.
            var part = new PartUsage { Id = Guid.NewGuid() };
            var mass = new AttributeUsage { DeclaredName = "mass" };
            part.AssignOwnership(new FeatureMembership(), mass);
            mass.AssignOwnership(new FeatureValue(), new LiteralInteger { Value = 2 });

            this.modelProvider.Setup(provider => provider.GetRequiredElementById(part.Id)).Returns(part);

            var tools = new BudgetTools(this.modelProvider.Object);
            var partMass = tools.SumAttribute(part.Id, "mass");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.SumAttribute(PayloadSubsystemId, " "), Throws.TypeOf<McpException>());
                Assert.That(() => tools.SumAttribute(Guid.NewGuid(), "mass"), Throws.TypeOf<McpException>());
                Assert.That(partMass.Total, Is.EqualTo(2));
                Assert.That(partMass.Contributions[0].Id, Is.EqualTo(part.Id));
                Assert.That(partMass.Contributions[0].Types, Is.Empty);
            }

            tools = CreateSatelliteTools();

            var payloadMass = tools.SumAttribute(PayloadSubsystemId, "mass");
            var cameraContribution = payloadMass.Contributions.Single(contribution => contribution.Id == CameraId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.SumAttribute(PayloadSubsystemId, "unknownAttribute"), Throws.TypeOf<McpException>().With.Message.Contains("unknownAttribute"));
                Assert.That(payloadMass.Attribute, Is.EqualTo("mass"));
                Assert.That(payloadMass.Total, Is.EqualTo(39.5));
                Assert.That(payloadMass.ContributorCount, Is.EqualTo(2));
                Assert.That(payloadMass.Contributions.Select(contribution => contribution.Value), Is.EquivalentTo([38, 1.5]));
                Assert.That(cameraContribution.QualifiedName, Is.EqualTo(CameraQualifiedName));
                Assert.That(cameraContribution.Types, Is.EqualTo(["OpticalCamera"]));
            }
        }

        [Test]
        public void VerifyEvaluateWhatIf()
        {
            var tools = CreateSatelliteTools();

            var whatIf = tools.EvaluateWhatIf(PayloadSubsystemId, "mass", CameraId, 35);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.EvaluateWhatIf(PayloadSubsystemId, " ", CameraId, 35), Throws.TypeOf<McpException>());
                Assert.That(() => tools.EvaluateWhatIf(PayloadSubsystemId, "mass", PayloadSubsystemId, 35),
                    Throws.TypeOf<McpException>().With.Message.Contains(PayloadSubsystemId.ToString()));
                Assert.That(whatIf.Attribute, Is.EqualTo("mass"));
                Assert.That(whatIf.ChangedElement, Is.EqualTo(CameraQualifiedName));
                Assert.That(whatIf.OldValue, Is.EqualTo(38));
                Assert.That(whatIf.NewValue, Is.EqualTo(35));
                Assert.That(whatIf.CurrentTotal, Is.EqualTo(39.5));
                Assert.That(whatIf.NewTotal, Is.EqualTo(36.5));
                Assert.That(whatIf.Difference, Is.EqualTo(-3));
            }
        }

        /// <summary>
        /// Creates <see cref="BudgetTools"/> that work on the <c>Satellite.json</c> test model.
        /// </summary>
        /// <returns>The <see cref="BudgetTools"/> on the loaded model.</returns>
        private static BudgetTools CreateSatelliteTools()
        {
            var satelliteModelProvider = new InMemoryModelProvider(new Mock<IModelChangeApplier>().Object, new DeSerializer());
            satelliteModelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));

            return new BudgetTools(satelliteModelProvider);
        }
    }
}
