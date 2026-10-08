// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionToolsTestFixture.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    /// <summary>
    /// Suite of tests for the <see cref="ConstructionTools"/> class.
    /// </summary>
    [TestFixture]
    public class ConstructionToolsTestFixture
    {
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
                Assert.That(() => new ConstructionTools(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyApplyChanges()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } }];
            var expectedResult = new ApplyChangesResult(true, [], []);
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(expectedResult);

            var tools = new ConstructionTools(this.modelProvider.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.ApplyChanges(null), Throws.TypeOf<McpException>());
                Assert.That(() => tools.ApplyChanges([]), Throws.TypeOf<McpException>().With.Message.Contains("no change"));
                Assert.That(tools.ApplyChanges(changes), Is.SameAs(expectedResult));
            }

            this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Once);
        }

        [Test]
        public void ApplyChanges_OnEmptyModel_BuildsAModelReadByTheBudgetTools()
        {
            var emptyModelProvider = new InMemoryModelProvider(new ModelChangeApplier());
            emptyModelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            var tools = new ConstructionTools(emptyModelProvider);
            var budgetTools = new BudgetTools(emptyModelProvider);

            var creation = tools.ApplyChanges(
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "Camera", Payload = new ElementPayload { Type = "PartDefinition", Owner = "spacecraft", Name = "Camera" } },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Camera", Name = "mass", Value = 38 } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "camera", Definition = "Camera" } }
            ]);

            var createdIds = creation.CreatedElements.ToDictionary(created => created.TemporaryName, created => created.Id);
            var packageId = createdIds["spacecraft"];
            var elementCount = emptyModelProvider.Elements.Count;

            var refusal = tools.ApplyChanges(
            [
                new ModelChange { Identity = createdIds["cameraMass"].ToString(), Payload = new ElementPayload { Value = 35 } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "unknownOwner", Name = "lens" } }
            ]);

            var totalAfterRefusal = budgetTools.SumAttribute(packageId, "mass").Total;

            var modification = tools.ApplyChanges([new ModelChange { Identity = createdIds["cameraMass"].ToString(), Payload = new ElementPayload { Value = 35 } }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(creation.Applied, Is.True);
                Assert.That(createdIds.Keys, Is.EqualTo(["spacecraft", "Camera", "cameraMass", "camera"]));
                Assert.That(refusal.Applied, Is.False);
                Assert.That(refusal.Problems, Has.Count.EqualTo(1));
                Assert.That(refusal.Problems[0], Does.StartWith("Change 2 (create)"));
                Assert.That(emptyModelProvider.Elements, Has.Count.EqualTo(elementCount));
                Assert.That(totalAfterRefusal, Is.EqualTo(38));
                Assert.That(modification.Applied, Is.True);
                Assert.That(budgetTools.SumAttribute(packageId, "mass").Total, Is.EqualTo(35));
            }
        }
    }
}
