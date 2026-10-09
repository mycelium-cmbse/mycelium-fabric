// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelValidatorTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Moq;

    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Validation;

    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Exceptions;

    /// <summary>
    /// Suite of tests for the <see cref="ModelValidator"/> class.
    /// </summary>
    [TestFixture]
    public class ModelValidatorTestFixture
    {
        private Mock<IValidationRule> warningRule;

        private Mock<IValidationRule> errorRule;

        private Mock<IValidationRule> failingRule;

        [SetUp]
        public void SetUp()
        {
            this.warningRule = CreateRule("warning-rule", ValidationSeverity.Warning);
            this.errorRule = CreateRule("error-rule", ValidationSeverity.Error);
            this.failingRule = CreateRule("failing-rule", ValidationSeverity.Error);

            this.failingRule.Setup(rule => rule.FindProblem(It.IsAny<IElement>(), It.IsAny<ValidationContext>())).Throws(new IncompleteModelException("An element is missing."));
        }

        [Test]
        public void VerifyConstructor()
        {
            var informationRule = CreateRule("a-rule", ValidationSeverity.Information);
            var validator = new ModelValidator([this.warningRule.Object, informationRule.Object, this.failingRule.Object, this.errorRule.Object]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ModelValidator(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(validator.Rules.Select(rule => rule.Name), Is.EqualTo(["error-rule", "failing-rule", "warning-rule", "a-rule"]));
            }
        }

        [Test]
        public void VerifyValidate()
        {
            // A package owns two parts. When the camera is removed, the qualified name of the lens cannot be computed, and the
            // feature memberships of the attributes of the camera lose their owner: they are described by the attribute they own,
            // or by their metaclass without it.
            var builder = new DtoModelBuilder();
            var package = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Model" });
            var part = builder.AddMember(package, new PartUsage { DeclaredName = "camera" });
            var lens = builder.AddMember(package, new PartUsage { DeclaredName = "lens" });
            var mass = builder.AddMember(part, new AttributeUsage { DeclaredName = "mass" }, new FeatureMembership());
            var power = builder.AddMember(part, new AttributeUsage { DeclaredName = "power" }, new FeatureMembership());

            builder.Remove(part);
            var elements = builder.Build();

            builder.Remove(mass);
            var elementsWithoutMass = builder.Build();

            this.warningRule.Setup(rule => rule.FindProblem(It.IsAny<IElement>(), It.IsAny<ValidationContext>())).Returns((IElement element, ValidationContext _) => element.DeclaredName is null or "lens" ? "Unnamed or lens." : null);
            this.errorRule.Setup(rule => rule.FindProblem(It.Is<IElement>(element => element.DeclaredName == "Model"), It.IsAny<ValidationContext>())).Returns("Error.");

            var validator = new ModelValidator([this.warningRule.Object, this.failingRule.Object, this.errorRule.Object]);
            var issues = validator.Validate(elements);
            var issuesWithoutMass = validator.Validate(elementsWithoutMass);

            string DescribeIn(IEnumerable<ValidationIssue> validationIssues, Guid? elementId) => validationIssues.Single(issue => issue.ElementId == elementId).Element;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => validator.Validate(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(issues[0], Is.EqualTo(new ValidationIssue("error-rule", ValidationSeverity.Error, package.Id, "Model", "Error.")));
                Assert.That(issues.Skip(1).Select(issue => issue.Rule), Is.All.EqualTo("warning-rule"));
                Assert.That(issues.Skip(1).Select(issue => issue.Element), Is.Ordered.Using<string>(StringComparer.Ordinal));
                Assert.That(issues.Any(issue => issue.Rule == "failing-rule"), Is.False);
                Assert.That(DescribeIn(issues, lens.Id), Is.EqualTo("lens"));
                Assert.That(DescribeIn(issues, builder.RootNamespace.Id), Is.EqualTo("Namespace"));
                Assert.That(DescribeIn(issues, package.OwningRelationship), Is.EqualTo("OwningMembership in Namespace"));
                Assert.That(DescribeIn(issues, part.OwningRelationship), Is.EqualTo("OwningMembership in Model"));
                Assert.That(DescribeIn(issues, mass.OwningRelationship), Is.EqualTo("FeatureMembership of mass"));
                Assert.That(DescribeIn(issues, power.OwningRelationship), Is.EqualTo("FeatureMembership of power"));
                Assert.That(DescribeIn(issuesWithoutMass, mass.OwningRelationship), Is.EqualTo("FeatureMembership"));
            }
        }

        /// <summary>
        /// Creates a mocked <see cref="IValidationRule"/> that finds no problem.
        /// </summary>
        /// <param name="name">The name of the rule.</param>
        /// <param name="severity">The severity of the rule.</param>
        /// <returns>The mocked rule.</returns>
        private static Mock<IValidationRule> CreateRule(string name, ValidationSeverity severity)
        {
            var rule = new Mock<IValidationRule>();
            rule.Setup(validationRule => validationRule.Name).Returns(name);
            rule.Setup(validationRule => validationRule.Severity).Returns(severity);
            rule.Setup(validationRule => validationRule.Description).Returns($"The {name}.");

            return rule;
        }
    }
}
