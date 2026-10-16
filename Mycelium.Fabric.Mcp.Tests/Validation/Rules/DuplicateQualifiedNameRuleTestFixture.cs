// ------------------------------------------------------------------------------------------------
//  <copyright file="DuplicateQualifiedNameRuleTestFixture.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.DTO.Root.Namespaces;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="DuplicateQualifiedNameRule"/> class.
    /// </summary>
    [TestFixture]
    public class DuplicateQualifiedNameRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new DuplicateQualifiedNameRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("duplicate-qualified-name"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Error));
                Assert.That(rule.Description, Does.StartWith("Several elements have the same qualified name"));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            // Two root namespaces declare an EOSat1 package; a camera part in two packages; two requirements with the same reqId
            // and distinct names; two anonymous parts; two requirements with the same reqId and no name; two lens parts in the same
            // package, the second of which has no qualified name, nor its glass; two Shared packages in the same root namespace.
            var builder = new DtoModelBuilder();
            var otherRoot = builder.Add(new Namespace());
            var firstPackage = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "EOSat1" });
            var secondPackage = builder.AddMember(otherRoot, new Package { DeclaredName = "EOSat1" });
            var payload = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Payload" });
            var aocs = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Aocs" });
            var payloadCamera = builder.AddMember(payload, new PartUsage { DeclaredName = "camera" });
            var aocsCamera = builder.AddMember(aocs, new PartUsage { DeclaredName = "camera" });
            var massBudget = builder.AddMember(payload, new RequirementUsage { DeclaredName = "massBudget", ReqId = "REQ-1" });
            var powerBudget = builder.AddMember(payload, new RequirementUsage { DeclaredName = "powerBudget", ReqId = "REQ-1" });
            var anonymousPart = builder.AddMember(aocs, new PartUsage());
            builder.AddMember(aocs, new PartUsage());
            var unnamedRequirement = builder.AddMember(aocs, new RequirementUsage { ReqId = "REQ-2" });
            builder.AddMember(aocs, new RequirementUsage { ReqId = "REQ-2" });
            var firstLens = builder.AddMember(payload, new PartUsage { DeclaredName = "lens" });
            var secondLens = builder.AddMember(payload, new PartUsage { DeclaredName = "lens" });
            var glass = builder.AddMember(secondLens, new PartUsage { DeclaredName = "glass" }, new FeatureMembership());
            builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Shared" });
            var secondShared = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Shared" });

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new DuplicateQualifiedNameRule();

            string FindProblem(Guid elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(firstPackage.Id), Is.EqualTo("2 elements have the qualified name 'EOSat1', so resolving it is ambiguous: rename them so that each qualified name designates one element."));
                Assert.That(FindProblem(secondPackage.Id), Is.EqualTo(FindProblem(firstPackage.Id)));
                Assert.That(FindProblem(payloadCamera.Id), Is.Null);
                Assert.That(FindProblem(aocsCamera.Id), Is.Null);
                Assert.That(FindProblem(massBudget.Id), Is.Null);
                Assert.That(FindProblem(powerBudget.Id), Is.Null);
                Assert.That(FindProblem(anonymousPart.Id), Is.Null);
                Assert.That(FindProblem(unnamedRequirement.Id), Does.StartWith("2 elements have the qualified name 'Aocs::'REQ-2''"));
                Assert.That(FindProblem(firstLens.Id), Is.Null);
                Assert.That(FindProblem(secondLens.Id), Is.EqualTo("Another member of 'Payload' is already named 'lens', so this element has no qualified name and resolving its name gives the other one: rename one of them."));
                Assert.That(FindProblem(glass.Id), Is.Null);
                Assert.That(FindProblem(secondShared.Id), Does.StartWith("Another member of the root namespace is already named 'Shared'"));
                Assert.That(FindProblem(builder.RootNamespace.Id), Is.Null);
            }
        }
    }
}
