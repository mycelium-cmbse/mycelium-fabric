// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementWithoutSatisfyRuleTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementWithoutSatisfyRule"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementWithoutSatisfyRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new RequirementWithoutSatisfyRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("requirement-without-satisfy"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(rule.Description, Does.StartWith("No satisfy link refers to a requirement"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // A satisfied requirement, one without satisfy link, a satisfied group with a nested requirement, and a group
            // without satisfy link whose nested requirement is reported instead.
            var builder = new DtoModelBuilder();
            var package = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Requirements" });
            var satisfied = builder.AddMember(package, new RequirementUsage { DeclaredName = "satisfied" });
            var alone = builder.AddMember(package, new RequirementUsage { DeclaredName = "alone" });
            var satisfiedGroup = builder.AddMember(package, new RequirementUsage { DeclaredName = "satisfiedGroup" });
            var covered = builder.AddMember(satisfiedGroup, new RequirementUsage { DeclaredName = "covered" }, new FeatureMembership());
            var group = builder.AddMember(package, new RequirementUsage { DeclaredName = "group" });
            var nested = builder.AddMember(group, new RequirementUsage { DeclaredName = "nested" }, new FeatureMembership());

            builder.AddSatisfy(package, satisfied.Id);
            builder.AddSatisfy(package, satisfiedGroup.Id);

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new RequirementWithoutSatisfyRule();

            string FindProblem(Guid elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(alone.Id), Is.EqualTo("No satisfy link refers to the requirement, for example satisfy massBudget by eosat1, so no part is known to satisfy it."));
                Assert.That(FindProblem(nested.Id), Is.Not.Null);
                Assert.That(FindProblem(satisfied.Id), Is.Null);
                Assert.That(FindProblem(satisfiedGroup.Id), Is.Null);
                Assert.That(FindProblem(covered.Id), Is.Null);
                Assert.That(FindProblem(group.Id), Is.Null);
            }
        }
    }
}
