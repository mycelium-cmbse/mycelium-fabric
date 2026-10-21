// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionToolsTestFixture.cs" company="Starion Group S.A.">
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

    using Microsoft.Extensions.Options;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;
    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Validation.Rules;

    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// Suite of tests for the <see cref="ConstructionTools"/> class.
    /// </summary>
    [TestFixture]
    public class ConstructionToolsTestFixture
    {
        private Mock<IModelProvider> modelProvider;

        private Mock<IModelValidator> modelValidator;

        private IOptions<ConstructionOptions> options;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
            this.modelValidator = new Mock<IModelValidator>();
            this.options = Options.Create(new ConstructionOptions());
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ConstructionTools(null, this.modelValidator.Object, this.options), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object, null, this.options), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.options), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyApplyChanges()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } }];
            var expectedResult = new ApplyChangesResult([], []);
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(expectedResult);
            this.modelValidator.Setup(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), null)).Returns([]);

            var tools = new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.options);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.ApplyChanges(null), Throws.TypeOf<McpException>());
                Assert.That(() => tools.ApplyChanges([]), Throws.TypeOf<McpException>().With.Message.Contains("no change"));
                Assert.That(tools.ApplyChanges(changes).CreatedElements, Is.SameAs(expectedResult.CreatedElements));
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Once);
            }
        }

        [Test]
        public void ApplyChanges_WithAnAppliedBatch_ReturnsOnlyTheNewIssues()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Name = "lens" } }];
            var oldIssue = CreateIssue("orphan-element", ValidationSeverity.Error);
            var resolvedIssue = CreateIssue("requirement-without-constraint", ValidationSeverity.Warning);
            var newIssue = CreateIssue("untyped-usage", ValidationSeverity.Warning);
            var information = CreateIssue("unused-definition", ValidationSeverity.Information);

            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([], []));

            this.modelValidator.SetupSequence(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), null))
                .Returns(new List<ValidationIssue> { oldIssue, resolvedIssue })
                .Returns(new List<ValidationIssue> { oldIssue, newIssue, information });

            var tools = new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.options);
            var result = tools.ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.True);
                Assert.That(result.NewIssues, Is.EqualTo(new[] { newIssue }));
                Assert.That(result.NewIssueCount, Is.EqualTo(1));
                Assert.That(result.ResolvedIssueCount, Is.EqualTo(1));
                this.modelValidator.Verify(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), null), Times.Exactly(2));
            }
        }

        [Test]
        public void ApplyChanges_WithManyNewIssues_ReturnsTheFirstTwenty()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Name = "lens" } }];
            var manyIssues = Enumerable.Range(0, 25).Select(_ => CreateIssue("untyped-usage", ValidationSeverity.Warning)).ToList();

            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([], []));
            this.modelValidator.SetupSequence(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), null))
                .Returns(new List<ValidationIssue>())
                .Returns(manyIssues);

            var result = new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.options).ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.NewIssues, Has.Count.EqualTo(20));
                Assert.That(result.NewIssueCount, Is.EqualTo(25));
            }
        }

        [Test]
        public void ApplyChanges_WithARefusedBatch_ReturnsTheProblemsOnly()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = " " } }];
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([], ["Change 1 (create): The name is missing."]));
            this.modelValidator.Setup(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), null)).Returns([]);

            var result = new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.options).ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Applied, Is.False);
                Assert.That(result.NewIssues, Is.Empty);
                this.modelValidator.Verify(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), null), Times.Once);
            }
        }

        [Test]
        public void ApplyChanges_WithTheReportTurnedOff_DoesNotValidate()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } }];
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([], []));
            var turnedOff = Options.Create(new ConstructionOptions { ReportNewIssues = false });

            var result = new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, turnedOff).ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.NewIssues, Is.Empty);
                this.modelValidator.Verify(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
            }
        }

        [Test]
        public void ApplyChanges_OnEmptyModel_BuildsAModelReadByTheBudgetTools()
        {
            var emptyModelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            emptyModelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));

            IValidationRule[] rules = [new BrokenReferenceRule(), new OrphanElementRule(), new DuplicateQualifiedNameRule(), new UntypedUsageRule(), new RequirementWithoutConstraintRule(),
                new RequirementWithoutSatisfyRule(), new UnusedDefinitionRule(), new RequirementWithoutIdRule()];

            var tools = new ConstructionTools(emptyModelProvider, new ModelValidator(rules), this.options);
            var budgetTools = new BudgetTools(emptyModelProvider);

            var creation = tools.ApplyChanges(
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "Camera", Payload = new ElementPayload { Type = "PartDefinition", Owner = "spacecraft", Name = "Camera" } },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Camera", Name = "mass", Value = 38 } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "camera", Definition = "Camera" } }
            ]);

            var createdIds = creation.CreatedElements.ToDictionary(created => created.QualifiedName, created => created.Id);
            var packageId = createdIds["Spacecraft"];
            var cameraMassId = createdIds["Spacecraft::Camera::mass"].ToString();
            var elementCount = emptyModelProvider.Elements.Count;

            var refusal = tools.ApplyChanges(
            [
                new ModelChange { Identity = cameraMassId, Payload = new ElementPayload { Value = 35 } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "unknownOwner", Name = "lens" } }
            ]);

            var elementCountAfterRefusal = emptyModelProvider.Elements.Count;
            var totalAfterRefusal = budgetTools.SumAttribute(packageId, "mass").Total;

            var untypedPart = tools.ApplyChanges([new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = packageId.ToString(), Name = "lens" } }]);
            var lensId = untypedPart.CreatedElements.Single().Id.ToString();

            var modification = tools.ApplyChanges([new ModelChange { Identity = cameraMassId, Payload = new ElementPayload { Value = 35 } }]);
            var totalAfterModification = budgetTools.SumAttribute(packageId, "mass").Total;

            var typing = tools.ApplyChanges([new ModelChange { Identity = lensId, Payload = new ElementPayload { Definition = createdIds["Spacecraft::Camera"].ToString() } }]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(creation.Applied, Is.True);
                Assert.That(createdIds.Keys, Is.EqualTo(["Spacecraft", "Spacecraft::Camera", "Spacecraft::Camera::mass", "Spacecraft::camera"]));
                Assert.That(creation.CreatedElements.Select(created => created.Type), Is.EqualTo(["Package", "PartDefinition", "AttributeUsage", "PartUsage"]));
                Assert.That(refusal.Applied, Is.False);
                Assert.That(refusal.Problems, Has.Count.EqualTo(1));
                Assert.That(refusal.Problems[0], Does.StartWith("Change 2 (create)"));
                Assert.That(totalAfterRefusal, Is.EqualTo(38));
                Assert.That(modification.Applied, Is.True);
                Assert.That(elementCountAfterRefusal, Is.EqualTo(elementCount));
                Assert.That(totalAfterModification, Is.EqualTo(35));
                Assert.That(creation.NewIssues, Is.Empty);
                Assert.That(untypedPart.NewIssues.Select(issue => issue.Rule), Is.EqualTo(["untyped-usage"]));
                Assert.That(modification.NewIssues, Is.Empty);
                Assert.That(typing.NewIssues, Is.Empty);
                Assert.That(typing.ResolvedIssueCount, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Creates a <see cref="ValidationIssue"/> of a new element.
        /// </summary>
        /// <param name="rule">The name of the rule.</param>
        /// <param name="severity">The <see cref="ValidationSeverity"/>.</param>
        /// <returns>The <see cref="ValidationIssue"/>.</returns>
        private static ValidationIssue CreateIssue(string rule, ValidationSeverity severity)
        {
            return new ValidationIssue(rule, severity, Guid.NewGuid(), "PartUsage lens", "A problem.");
        }
    }
}
