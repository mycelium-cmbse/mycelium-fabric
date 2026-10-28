// ------------------------------------------------------------------------------------------------
//  <copyright file="PartOutsideSystemRuleTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Validation.Rules
{
    using System;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Validation.Rules;

    using SysML2.NET.Core.POCO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="PartOutsideSystemRule"/> class.
    /// </summary>
    [TestFixture]
    public class PartOutsideSystemRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new PartOutsideSystemRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("part-outside-system"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(rule.Description, Does.StartWith("A part with parts or values is next to a part that satisfies requirements"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // The solar array, with a mass, is next to the spacecraft that satisfies the mass requirement; the empty part next to
            // it has neither parts nor values.
            var modelProvider = BudgetChangesHelper.CreateModel(withMissingValues: false, withPartOutside: true);
            var budgetId = modelProvider.Elements.Single(element => element.DeclaredName == "Budget").Id;
            var result = modelProvider.ApplyChanges([new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = budgetId.ToString(), Name = "emptyPart" } }]);

            var elements = modelProvider.Elements;
            var context = new ValidationContext(elements);
            var rule = new PartOutsideSystemRule();

            string FindProblem(string name) => rule.FindProblem(elements.Single(element => element.DeclaredName == name), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(BudgetChangesHelper.SolarArrayName), Is.EqualTo("The part is next to 'spacecraft', which satisfies requirements, in the same package instead of inside the part "
                    + "that holds the system: the budgets and checks of a part do not count the parts next to it. If they form one system, put its parts inside the part that holds it."));
                Assert.That(FindProblem(BudgetChangesHelper.SpacecraftName), Is.Null);
                Assert.That(FindProblem("emptyPart"), Is.Null);
                Assert.That(FindProblem("wheel1"), Is.Null);
                Assert.That(FindProblem("Budget"), Is.Null);
            }

            // Once the solar array satisfies a requirement too, the spacecraft is next to a part that satisfies requirements.
            var massLimitId = elements.Single(element => element.DeclaredName == "massLimit").Id;
            var solarArrayId = elements.Single(element => element.DeclaredName == BudgetChangesHelper.SolarArrayName).Id;
            result = modelProvider.ApplyChanges([new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = massLimitId.ToString(), SatisfyingPart = solarArrayId.ToString() } }]);

            elements = modelProvider.Elements;
            context = new ValidationContext(elements);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(FindProblem(BudgetChangesHelper.SpacecraftName), Does.StartWith("The part is next to 'solarArray', which satisfies requirements"));
                Assert.That(FindProblem(BudgetChangesHelper.SolarArrayName), Does.StartWith("The part is next to 'spacecraft', which satisfies requirements"));
            }
        }

        [Test]
        public void FindProblem_OnTheReferenceModelOrWithoutSatisfyingPart_FindsNone()
        {
            var rule = new PartOutsideSystemRule();
            var satellite = DtoModelBuilder.FromSatellite().Build();
            var satelliteContext = new ValidationContext(satellite);

            // Without satisfy link, no part is known to hold the system: the part next to it is not reported.
            var modelProvider = BudgetChangesHelper.CreateModel(withMissingValues: false, withPartOutside: true);
            var satisfyId = modelProvider.Elements.OfType<ISatisfyRequirementUsage>().Single().Id;
            var result = modelProvider.ApplyChanges([new ModelChange { Identity = satisfyId.ToString() }]);
            var context = new ValidationContext(modelProvider.Elements);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(satellite.Select(element => rule.FindProblem(element, satelliteContext)), Is.All.Null);
                Assert.That(result.Applied, Is.True, string.Join(" ", result.Problems));
                Assert.That(modelProvider.Elements.Select(element => rule.FindProblem(element, context)), Is.All.Null);
            }
        }
    }
}
