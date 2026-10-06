// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementToolsTestFixture.cs" company="Starion Group S.A.">
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
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementTools"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementToolsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>massBudget</c> requirement (REQ-SYS-001) in <c>Satellite.json</c>.
        /// </summary>
        private const string MassBudgetId = "6b93c533-0395-7e18-1bf6-4475deb47ba5";

        /// <summary>
        /// The <c>Id</c> of the <c>payloadPower</c> requirement (REQ-SYS-002) in <c>Satellite.json</c>.
        /// </summary>
        private const string PayloadPowerId = "d17c3498-3404-4a62-9728-c58e59a4954f";

        /// <summary>
        /// The <c>Id</c> of the <c>eosat1</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string Eosat1Id = "5d06ef4d-4489-500c-e94f-a8cf563d3fc1";

        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private const string PayloadSubsystemId = "95a8c184-a12e-1125-c0ee-bbe7a025de5b";

        private Mock<IModelProvider> modelProvider;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
            this.modelProvider.Setup(provider => provider.Elements).Returns([]);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new RequirementTools(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new RequirementTools(this.modelProvider.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyCheckRequirements()
        {
            var tools = new RequirementTools(this.modelProvider.Object);

            var emptyResult = tools.CheckRequirements();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.CheckRequirements(offset: -1), Throws.TypeOf<McpException>().With.Message.Contains("offset"));
                Assert.That(() => tools.CheckRequirements(limit: 0), Throws.TypeOf<McpException>().With.Message.Contains("limit"));
                Assert.That(() => tools.CheckRequirements(limit: 21), Throws.TypeOf<McpException>().With.Message.Contains("limit"));
                Assert.That(emptyResult.TotalFound, Is.EqualTo(0));
                Assert.That(emptyResult.Requirements, Has.Count.EqualTo(0));
                Assert.That(emptyResult.NextOffset, Is.Null);
            }

            this.modelProvider.Verify(provider => provider.Elements, Times.Once);

            tools = new RequirementTools(CreateSatelliteModelProvider());

            var allChecks = tools.CheckRequirements();
            var firstPage = tools.CheckRequirements(limit: 4);
            var lastPage = tools.CheckRequirements(offset: 4, limit: 4);
            var satisfiedChecks = tools.CheckRequirements(RequirementStatus.Satisfied);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(allChecks.NotVerifiableCount, Is.EqualTo(6));
                Assert.That(allChecks.SatisfiedCount + allChecks.NotSatisfiedCount + allChecks.NotEvaluatedCount, Is.EqualTo(0));
                Assert.That(allChecks.TotalFound, Is.EqualTo(6));
                Assert.That(allChecks.Requirements.Select(check => check.ReqId), Is.EqualTo(["REQ-SYS-001", "REQ-SYS-002", "REQ-SYS-003", "REQ-SYS-004", "REQ-SYS-005", "REQ-SYS-006"]));
                Assert.That(allChecks.NextOffset, Is.Null);
                Assert.That(firstPage.Requirements, Has.Count.EqualTo(4));
                Assert.That(firstPage.NextOffset, Is.EqualTo(4));
                Assert.That(lastPage.Requirements.Select(check => check.ReqId), Is.EqualTo(["REQ-SYS-005", "REQ-SYS-006"]));
                Assert.That(lastPage.NextOffset, Is.Null);
                Assert.That(satisfiedChecks.TotalFound, Is.EqualTo(0));
                Assert.That(satisfiedChecks.NotVerifiableCount, Is.EqualTo(6));
            }
        }

        [Test]
        public void CheckRequirements_AfterConstraintsAndSatisfyLinks_TellsWhichRequirementsAreSatisfied()
        {
            var satelliteModelProvider = CreateSatelliteModelProvider();
            var tools = new RequirementTools(satelliteModelProvider);

            var batch = new ConstructionTools(satelliteModelProvider).ApplyChanges(
            [
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = MassBudgetId, Attribute = "mass", Operator = "<=", Limit = 150, Margin = 20 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = MassBudgetId, SatisfyingPart = Eosat1Id },
                new ModelChange { Kind = ChangeKind.SetConstraint, Element = PayloadPowerId, Attribute = "power", Operator = "<", Limit = 70 },
                new ModelChange { Kind = ChangeKind.Satisfy, Element = PayloadPowerId, SatisfyingPart = PayloadSubsystemId }
            ]);

            var result = tools.CheckRequirements();
            var massBudget = result.Requirements[0];
            var payloadPower = result.Requirements[1];
            var problems = tools.CheckRequirements(RequirementStatus.NotSatisfied);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(batch.Applied, Is.True);
                Assert.That(result.SatisfiedCount, Is.EqualTo(1));
                Assert.That(result.NotSatisfiedCount, Is.EqualTo(1));
                Assert.That(result.NotVerifiableCount, Is.EqualTo(4));
                Assert.That(result.NotEvaluatedCount, Is.EqualTo(0));
                Assert.That(massBudget.Id, Is.EqualTo(Guid.Parse(MassBudgetId)));
                Assert.That(massBudget.ReqId, Is.EqualTo("REQ-SYS-001"));
                Assert.That(massBudget.Name, Is.EqualTo("massBudget"));
                Assert.That(massBudget.Status, Is.EqualTo(RequirementStatus.NotSatisfied));
                Assert.That(massBudget.Constraint, Is.EqualTo("subj.mass * 1.2 <= 150"));
                Assert.That(massBudget.SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1"));
                Assert.That(massBudget.Value, Is.EqualTo(125.8));
                Assert.That(massBudget.Gap, Is.EqualTo(-0.96));
                Assert.That(massBudget.Explanation, Is.EqualTo("150.96 > 150"));
                Assert.That(payloadPower.ReqId, Is.EqualTo("REQ-SYS-002"));
                Assert.That(payloadPower.Status, Is.EqualTo(RequirementStatus.Satisfied));
                Assert.That(payloadPower.Constraint, Is.EqualTo("subj.power < 70"));
                Assert.That(payloadPower.SatisfiedBy, Is.EqualTo("EOSat1::Architecture::eosat1::payloadSubsystem"));
                Assert.That(payloadPower.Value, Is.EqualTo(64));
                Assert.That(payloadPower.Gap, Is.EqualTo(6));
                Assert.That(payloadPower.Explanation, Is.EqualTo("64 < 70"));
                Assert.That(problems.TotalFound, Is.EqualTo(1));
                Assert.That(problems.Requirements[0].ReqId, Is.EqualTo("REQ-SYS-001"));
            }
        }

        /// <summary>
        /// Creates an <see cref="InMemoryModelProvider"/> that holds the <c>Satellite.json</c> test model.
        /// </summary>
        /// <returns>The <see cref="InMemoryModelProvider"/> with the loaded model.</returns>
        private static InMemoryModelProvider CreateSatelliteModelProvider()
        {
            var satelliteModelProvider = new InMemoryModelProvider();
            satelliteModelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));

            return satelliteModelProvider;
        }
    }
}
