// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionPlanStoreTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Services
{
    using Mycelium.Fabric.Mcp.Plans;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// Suite of tests for the <see cref="ConstructionPlanStore"/> class.
    /// </summary>
    [TestFixture]
    public class ConstructionPlanStoreTestFixture
    {
        [Test]
        public void VerifyPlan()
        {
            var planStore = new ConstructionPlanStore();
            var planBefore = planStore.Plan;

            var firstPlan = ConstructionPlan.Create([new PlanSection { Name = "Architecture", Elements = [new PlannedElement { Name = "camera", Type = "PartUsage" }] }]).Value;
            var secondPlan = ConstructionPlan.Create([new PlanSection { Name = "Behaviour", Elements = [new PlannedElement { Name = "launch", Type = "StateUsage" }] }]).Value;

            planStore.Plan = firstPlan;
            var keptPlan = planStore.Plan;
            planStore.Plan = secondPlan;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(planBefore, Is.Null);
                Assert.That(keptPlan, Is.SameAs(firstPlan));
                Assert.That(planStore.Plan, Is.SameAs(secondPlan));
            }
        }
    }
}
