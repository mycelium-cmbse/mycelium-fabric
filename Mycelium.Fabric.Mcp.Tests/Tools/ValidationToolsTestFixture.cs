// ------------------------------------------------------------------------------------------------
//  <copyright file="ValidationToolsTestFixture.cs" company="Starion Group S.A.">
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
    using Mycelium.Fabric.Mcp.Tests.TestHelpers;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Validation.Rules;

    using SysML2.NET.Core.DTO.Core.Types;
    using SysML2.NET.Core.DTO.Kernel.Packages;
    using SysML2.NET.Core.DTO.Systems.Attributes;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="ValidationTools"/> class.
    /// </summary>
    [TestFixture]
    public class ValidationToolsTestFixture
    {
        /// <summary>
        /// The <c>Id</c> of the <c>payloadSubsystem</c> part in <c>Satellite.json</c>.
        /// </summary>
        private static readonly Guid PayloadSubsystemId = Guid.Parse("95a8c184-a12e-1125-c0ee-bbe7a025de5b");

        private Mock<IModelProvider> modelProvider;

        private Mock<IModelValidator> modelValidator;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
            this.modelProvider.Setup(provider => provider.GetRequiredElementById(It.IsAny<Guid>())).Throws(new McpException("No element has this identifier."));

            this.modelValidator = new Mock<IModelValidator>();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ValidationTools(null, this.modelValidator.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ValidationTools(this.modelProvider.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ValidationTools(this.modelProvider.Object, this.modelValidator.Object), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyValidateModel()
        {
            // An error on the camera, a warning on its attribute and 21 warnings on another package: more than one page.
            var builder = new DtoModelBuilder();
            var model = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Model" });
            var other = builder.AddMember(builder.RootNamespace, new Package { DeclaredName = "Other" });
            var camera = builder.AddMember(model, new PartUsage { DeclaredName = "camera" });
            var mass = builder.AddMember(camera, new AttributeUsage { DeclaredName = "mass" }, new FeatureMembership());
            var elements = builder.Build();

            List<ValidationIssue> issues = [new ValidationIssue("broken-reference", ValidationSeverity.Error, camera.Id, "Model::camera", "Broken.")];
            issues.Add(new ValidationIssue("untyped-usage", ValidationSeverity.Warning, mass.Id, "Model::camera::mass", "Untyped."));
            issues.AddRange(Enumerable.Range(0, 21).Select(index => new ValidationIssue("untyped-usage", ValidationSeverity.Warning, other.Id, "Other", $"Untyped {index}.")));

            this.modelProvider.Setup(provider => provider.Elements).Returns(elements);
            this.modelProvider.Setup(provider => provider.GetRequiredElementById(camera.Id)).Returns(elements.Single(element => element.Id == camera.Id));
            this.modelValidator.Setup(validator => validator.Rules).Returns([CreateRule("broken-reference", ValidationSeverity.Error), CreateRule("untyped-usage", ValidationSeverity.Warning)]);
            this.modelValidator.Setup(validator => validator.Validate(elements)).Returns(issues);

            var tools = new ValidationTools(this.modelProvider.Object, this.modelValidator.Object);

            var firstPage = tools.ValidateModel();
            var errors = tools.ValidateModel(severity: ValidationSeverity.Error);
            var lastWarnings = tools.ValidateModel(rule: "untyped-usage", offset: 20);
            var cameraReport = tools.ValidateModel(camera.Id, limit: 1);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.ValidateModel(offset: -1), Throws.TypeOf<McpException>());
                Assert.That(() => tools.ValidateModel(limit: 0), Throws.TypeOf<McpException>());
                Assert.That(() => tools.ValidateModel(limit: 21), Throws.TypeOf<McpException>());
                Assert.That(() => tools.ValidateModel(rule: "unknown"), Throws.TypeOf<McpException>().With.Message.EqualTo("No rule is named 'unknown'. The rules are: broken-reference, untyped-usage."));
                Assert.That(() => tools.ValidateModel(Guid.NewGuid()), Throws.TypeOf<McpException>());
                Assert.That(firstPage.ElementCount, Is.EqualTo(elements.Count));
                Assert.That(firstPage.ErrorCount, Is.EqualTo(1));
                Assert.That(firstPage.WarningCount, Is.EqualTo(22));
                Assert.That(firstPage.InformationCount, Is.EqualTo(0));
                Assert.That(firstPage.Rules, Is.EqualTo([new RuleSummary("broken-reference", ValidationSeverity.Error, "The broken-reference.", 1), new RuleSummary("untyped-usage", ValidationSeverity.Warning, "The untyped-usage.", 22)]));
                Assert.That(firstPage.TotalFound, Is.EqualTo(23));
                Assert.That(firstPage.Issues, Is.EqualTo(issues.Take(20)));
                Assert.That(firstPage.NextOffset, Is.EqualTo(20));
                Assert.That(errors.TotalFound, Is.EqualTo(1));
                Assert.That(errors.Issues, Is.EqualTo(issues.Take(1)));
                Assert.That(errors.NextOffset, Is.Null);
                Assert.That(lastWarnings.TotalFound, Is.EqualTo(22));
                Assert.That(lastWarnings.Issues.Select(issue => issue.Message), Is.EqualTo(["Untyped 19.", "Untyped 20."]));
                Assert.That(lastWarnings.NextOffset, Is.Null);
                Assert.That(cameraReport.ElementCount, Is.EqualTo(3));
                Assert.That(cameraReport.ErrorCount, Is.EqualTo(1));
                Assert.That(cameraReport.WarningCount, Is.EqualTo(1));
                Assert.That(cameraReport.Rules.Select(rule => rule.IssueCount), Is.EqualTo([1, 1]));
                Assert.That(cameraReport.Issues, Is.EqualTo(issues.Take(1)));
                Assert.That(cameraReport.NextOffset, Is.EqualTo(1));
            }

            this.modelValidator.Verify(validator => validator.Validate(elements), Times.Exactly(4));

            // EOSat-1 is well formed: its six requirements are only text, and no part satisfies them.
            var satelliteModelProvider = new InMemoryModelProvider(new Mock<IModelChangeApplier>().Object, new DeSerializer());
            satelliteModelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Satellite.json")));

            IValidationRule[] rules = [new BrokenReferenceRule(), new OrphanElementRule(), new DuplicateNameRule(), new UntypedUsageRule(), new RequirementWithoutConstraintRule(),
                new RequirementWithoutSatisfyRule(), new UnusedDefinitionRule(), new RequirementWithoutIdRule()];

            var satelliteTools = new ValidationTools(satelliteModelProvider, new ModelValidator(rules));

            var report = satelliteTools.ValidateModel();
            var constraintReport = satelliteTools.ValidateModel(rule: "requirement-without-constraint");
            var payloadReport = satelliteTools.ValidateModel(PayloadSubsystemId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(report.ElementCount, Is.EqualTo(548));
                Assert.That(report.ErrorCount, Is.EqualTo(0));
                Assert.That(report.WarningCount, Is.EqualTo(12));
                Assert.That(report.InformationCount, Is.EqualTo(0));
                Assert.That(report.Rules.Select(rule => rule.Name), Is.EqualTo(["broken-reference", "duplicate-name", "orphan-element", "requirement-without-constraint", "requirement-without-satisfy",
                    "untyped-usage", "requirement-without-id", "unused-definition"]));
                Assert.That(constraintReport.TotalFound, Is.EqualTo(6));
                Assert.That(constraintReport.Issues[0].Element, Is.EqualTo("EOSat1::Requirements::attitudeKnowledge"));
                Assert.That(payloadReport.ElementCount, Is.GreaterThan(1).And.LessThan(548));
                Assert.That(payloadReport.TotalFound, Is.EqualTo(0));
            }
        }

        /// <summary>
        /// Creates a mocked <see cref="IValidationRule"/>.
        /// </summary>
        /// <param name="name">The name of the rule.</param>
        /// <param name="severity">The severity of the rule.</param>
        /// <returns>The mocked rule.</returns>
        private static IValidationRule CreateRule(string name, ValidationSeverity severity)
        {
            var rule = new Mock<IValidationRule>();
            rule.Setup(validationRule => validationRule.Name).Returns(name);
            rule.Setup(validationRule => validationRule.Severity).Returns(severity);
            rule.Setup(validationRule => validationRule.Description).Returns($"The {name}.");

            return rule.Object;
        }
    }
}
