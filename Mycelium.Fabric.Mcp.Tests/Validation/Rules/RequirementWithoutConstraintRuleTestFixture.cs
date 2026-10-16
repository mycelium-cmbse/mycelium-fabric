// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementWithoutConstraintRuleTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.DTO.Systems.Requirements;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementWithoutConstraintRule"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementWithoutConstraintRuleTestFixture
    {
        [Test]
        public void VerifyProperties()
        {
            var rule = new RequirementWithoutConstraintRule();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rule.Name, Is.EqualTo("requirement-without-constraint"));
                Assert.That(rule.Severity, Is.EqualTo(ValidationSeverity.Warning));
                Assert.That(rule.Description, Is.EqualTo("A requirement has no required constraint, so it is only text and cannot be verified."));
            }
        }

        [Test]
        public void VerifyFindProblem()
        {
            var builder = new DtoModelBuilder();
            var package = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Requirements" });

            // A requirement with a constraint, one without, one typed by a definition that has a constraint, a group whose
            // nested requirement has none, a satisfy link, and a requirement declared in a part definition.
            var constrained = builder.AddMember(package, new RequirementUsage { DeclaredName = "constrained" });
            builder.AddRequiredConstraint(constrained);

            var textOnly = builder.AddMember(package, new RequirementUsage { DeclaredName = "textOnly" });

            var definition = builder.AddMember(package, new RequirementDefinition { DeclaredName = "MassRequirement" });
            builder.AddRequiredConstraint(definition);
            var typed = builder.AddMember(package, new RequirementUsage { DeclaredName = "typed" });
            builder.AddRelationship(typed, new FeatureTyping { TypedFeature = typed.Id, Type = definition.Id });

            var group = builder.AddMember(package, new RequirementUsage { DeclaredName = "group" });
            var nested = builder.AddMember(group, new RequirementUsage { DeclaredName = "nested" }, new FeatureMembership());

            var satisfy = builder.AddSatisfy(package, constrained.Id);

            var partDefinition = builder.AddMember(package, new PartDefinition { DeclaredName = "Satellite" });
            var declared = builder.AddMember(partDefinition, new RequirementUsage { DeclaredName = "declared" }, new FeatureMembership());

            var elements = builder.Build();
            var context = new ValidationContext(elements);
            var rule = new RequirementWithoutConstraintRule();

            string FindProblem(Guid elementId) => rule.FindProblem(elements.Single(element => element.Id == elementId), context);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => rule.FindProblem(null, context), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => rule.FindProblem(elements[0], null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(FindProblem(textOnly.Id), Is.EqualTo("The requirement has no required constraint, for example subj.mass <= 150 [kg], so it cannot be verified."));
                Assert.That(FindProblem(nested.Id), Is.Not.Null);
                Assert.That(FindProblem(constrained.Id), Is.Null);
                Assert.That(FindProblem(typed.Id), Is.Null);
                Assert.That(FindProblem(group.Id), Is.Null);
                Assert.That(FindProblem(satisfy.Id), Is.Null);
                Assert.That(FindProblem(declared.Id), Is.Null);
                Assert.That(FindProblem(package.Id), Is.Null);
            }
        }
    }
}
