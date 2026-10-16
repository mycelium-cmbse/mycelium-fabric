// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementWithoutIdRuleTestFixture.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Validation.Rules;

    using SysML2.NET.Core.DTO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementWithoutIdRule"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementWithoutIdRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new RequirementWithoutIdRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("requirement-without-id"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Information));
                Assert.That(rule.Description, Does.StartWith("A requirement has no identifier (reqId)"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // The requirements of EOSat-1 have an identifier; two requirements without one are added.
            var builder = DtoModelBuilder.FromSatellite();
            var requirements = builder.Get("Requirements");
            var withoutId = builder.AddMember(requirements, new RequirementUsage { DeclaredName = "withoutId" });
            var blankId = builder.AddMember(requirements, new RequirementUsage { DeclaredName = "blankId", ReqId = " " });

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new RequirementWithoutIdRule();

            string FindProblem(Guid elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(withoutId.Id), Is.EqualTo("The requirement has no identifier (reqId), for example REQ-SYS-001."));
                Assert.That(FindProblem(blankId.Id), Is.Not.Null);
                Assert.That(FindProblem(builder.Get("massBudget").Id), Is.Null);
                Assert.That(FindProblem(requirements.Id), Is.Null);
            }
        }
    }
}
