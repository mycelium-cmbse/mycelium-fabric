// ------------------------------------------------------------------------------------------------
//  <copyright file="DuplicateNameRuleTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="DuplicateNameRule"/> class.
    /// </summary>
    [TestFixture]
    public class DuplicateNameRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new DuplicateNameRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("duplicate-name"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(rule.Description, Does.StartWith("Two members of a namespace have the same name or short name"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // Two parts named camera and two requirements with the same reqId in one package; another package has a part whose
            // short name is its name, and an anonymous part.
            var builder = new DtoModelBuilder();
            var duplicates = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Duplicates" });
            var distinct = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Distinct" });

            builder.AddMember(duplicates, new PartUsage { DeclaredName = "camera" });
            builder.AddMember(duplicates, new PartUsage { DeclaredName = "camera" });
            builder.AddMember(duplicates, new RequirementUsage { DeclaredName = "massBudget", ReqId = "REQ-1" });
            builder.AddMember(duplicates, new RequirementUsage { DeclaredName = "powerBudget", ReqId = "REQ-1" });
            builder.AddMember(distinct, new PartUsage { DeclaredName = "camera", DeclaredShortName = "camera" });
            builder.AddMember(distinct, new PartUsage());

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new DuplicateNameRule();

            string FindProblem(Guid? elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(duplicates.Id), Is.EqualTo("Several members are named 'camera', 'REQ-1': rename them so that each name designates one member."));
                Assert.That(FindProblem(distinct.Id), Is.Null);
                Assert.That(FindProblem(builder.RootNamespace.Id), Is.Null);
                Assert.That(FindProblem(duplicates.OwningRelationship), Is.Null);
            }
        }
    }
}
