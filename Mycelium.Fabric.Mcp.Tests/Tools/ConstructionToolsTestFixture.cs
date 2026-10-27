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
    using System.Text.Json;

    using Microsoft.Extensions.Options;

    using ModelContextProtocol;
    using ModelContextProtocol.Protocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Requirements;
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

        private Mock<IAppHostDetector> appHostDetector;

        private PendingBatchStore pendingBatchStore;

        private IOptions<ConstructionOptions> options;

        [SetUp]
        public void SetUp()
        {
            this.modelProvider = new Mock<IModelProvider>();
            this.modelValidator = new Mock<IModelValidator>();
            this.appHostDetector = new Mock<IAppHostDetector>();
            this.pendingBatchStore = new PendingBatchStore();
            this.options = Options.Create(new ConstructionOptions());

            this.modelValidator.Setup(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), null)).Returns([]);
            this.modelProvider.Setup(provider => provider.Elements).Returns([]);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new ConstructionTools(null, this.modelValidator.Object, this.pendingBatchStore, this.appHostDetector.Object, this.options), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object, null, this.pendingBatchStore, this.appHostDetector.Object, this.options), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, null, this.appHostDetector.Object, this.options), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.pendingBatchStore, null, this.options), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.pendingBatchStore, this.appHostDetector.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.CreateTools(), Throws.Nothing);
            }
        }

        [Test]
        public void VerifyApplyChanges()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } }];
            var createdElement = new CreatedElement(Guid.NewGuid(), "Spacecraft", "Package");
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([createdElement], []));

            var tools = this.CreateTools();
            var toolResult = tools.ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.ApplyChanges(null), Throws.TypeOf<McpException>());
                Assert.That(() => tools.ApplyChanges([]), Throws.TypeOf<McpException>().With.Message.Contains("no change"));
                Assert.That(ReadResult(toolResult).CreatedElements, Is.EqualTo(new[] { createdElement }));
                Assert.That(toolResult.StructuredContent, Is.Null);
                Assert.That(toolResult.Meta, Is.Null);
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Once);
                this.modelProvider.Verify(provider => provider.PreviewChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Never);
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

            var result = ReadResult(this.CreateTools().ApplyChanges(changes));

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

            var result = ReadResult(this.CreateTools().ApplyChanges(changes));

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

            var result = ReadResult(this.CreateTools().ApplyChanges(changes));

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

            var result = ReadResult(this.CreateTools(turnedOff).ApplyChanges(changes));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.NewIssues, Is.Empty);
                this.modelValidator.Verify(validator => validator.Validate(It.IsAny<IReadOnlyList<IElement>>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
            }
        }

        [Test]
        public void ApplyChanges_InAHostWithApps_KeepsTheBatchUntilTheUserDecides()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Name = "starTracker" } }];
            var createdElement = new CreatedElement(Guid.NewGuid(), "EOSat1::starTracker", "PartUsage");
            this.modelProvider.Setup(provider => provider.PreviewChanges(changes)).Returns(new ModelPreview([]) { CreatedElements = [createdElement] });
            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);

            var toolResult = this.CreateTools().ApplyChanges(changes);
            var review = ReadReview(toolResult);
            var token = review.Preview.Token;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(toolResult.Content.OfType<TextContentBlock>().Single().Text, Does.Contain("waits for the decision of the user"));
                Assert.That(review.Status, Is.EqualTo(BatchReviewStatus.Pending));
                Assert.That(review.Preview.CreatedElements, Is.EqualTo(new[] { new PreviewElement(createdElement.Id, createdElement.QualifiedName, createdElement.Type) }));
                Assert.That(review.Preview.BatchId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(token, Has.Length.EqualTo(32));
                Assert.That(toolResult.Content.OfType<TextContentBlock>().Single().Text, Does.Not.Contain(token));
                Assert.That(review.Preview.BudgetImpacts, Is.Empty);
                Assert.That(this.pendingBatchStore.HasPendingBatch, Is.True);
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Never);
            }
        }

        [Test]
        public void ApplyChanges_InAHostWithApps_RefusesAnInvalidBatch()
        {
            IReadOnlyList<ModelChange> invalid = [new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "unknown", Name = "lens" } }];
            this.modelProvider.Setup(provider => provider.PreviewChanges(invalid)).Returns(new ModelPreview(["Change 1 (create): The owner 'unknown' is unknown."]));
            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);

            var invalidReview = ReadReview(this.CreateTools().ApplyChanges(invalid));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invalidReview.Status, Is.EqualTo(BatchReviewStatus.Refused));
                Assert.That(invalidReview.Result.Problems, Is.EqualTo(["Change 1 (create): The owner 'unknown' is unknown."]));
                Assert.That(this.pendingBatchStore.HasPendingBatch, Is.False);
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Never);
            }
        }

        [Test]
        public void ApplyChanges_WithABatchWaiting_ShowsTheWaitingBatchAgainInAHostWithApps()
        {
            IReadOnlyList<ModelChange> waiting = [new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Name = "lens" } }];
            IReadOnlyList<ModelChange> another = [new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Name = "starTracker" } }];
            var createdElement = new CreatedElement(Guid.NewGuid(), "EOSat1::lens", "PartUsage");
            this.modelProvider.Setup(provider => provider.PreviewChanges(waiting)).Returns(new ModelPreview([]) { CreatedElements = [createdElement] });
            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);

            var tools = this.CreateTools();
            var firstResult = tools.ApplyChanges(waiting);
            var secondResult = tools.ApplyChanges(another);
            var secondReview = ReadReview(secondResult);
            var secondToken = secondReview.Preview.Token;
            var rejection = tools.DecideBatch(secondReview.Preview.BatchId, secondToken, false);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(secondResult.Content.OfType<TextContentBlock>().Single().Text, Does.StartWith("A batch already waits for the decision of the user"));
                Assert.That(secondReview.Status, Is.EqualTo(BatchReviewStatus.Pending));
                Assert.That(secondReview.Preview.BatchId, Is.EqualTo(ReadReview(firstResult).Preview.BatchId));
                Assert.That(secondReview.Preview.CreatedElements, Is.EqualTo(new[] { new PreviewElement(createdElement.Id, createdElement.QualifiedName, createdElement.Type) }));
                Assert.That(secondToken, Is.EqualTo(ReadReview(firstResult).Preview.Token));
                Assert.That(rejection.Status, Is.EqualTo(BatchReviewStatus.Rejected));
                Assert.That(this.pendingBatchStore.HasPendingBatch, Is.False);
                this.modelProvider.Verify(provider => provider.PreviewChanges(another), Times.Never);
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Never);
            }
        }

        [Test]
        public void ApplyChanges_WithABatchWaiting_RefusesTheBatchInAHostWithoutApps()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Name = "lens" } }];
            this.pendingBatchStore.TryAdd(changes);

            var toolResult = this.CreateTools().ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ReadResult(toolResult).Problems.Single(), Does.StartWith("A batch already waits for the decision of the user"));
                Assert.That(toolResult.StructuredContent, Is.Null);
                Assert.That(toolResult.Meta, Is.Null);
                this.modelProvider.Verify(provider => provider.PreviewChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Never);
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Never);
            }
        }

        [Test]
        public void ApplyChanges_InAHostWithApps_WithTheReviewTurnedOff_AppliesTheBatch()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } }];
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([], []));
            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);
            var turnedOff = Options.Create(new ConstructionOptions { ReviewBatches = false });

            var toolResult = this.CreateTools(turnedOff).ApplyChanges(changes);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ReadReview(toolResult).Status, Is.EqualTo(BatchReviewStatus.Applied));
                Assert.That(ReadResult(toolResult).Applied, Is.True);
                Assert.That(toolResult.Meta, Is.Null);
                Assert.That(this.pendingBatchStore.HasPendingBatch, Is.False);
                this.modelProvider.Verify(provider => provider.PreviewChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Never);
            }
        }

        [Test]
        public void VerifyDecideBatch()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } }];
            var createdElement = new CreatedElement(Guid.NewGuid(), "Spacecraft", "Package");
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([createdElement], []));

            var tools = this.CreateTools();

            var rejectedBatch = this.pendingBatchStore.TryAdd(changes);
            var wrongToken = new string('0', 32);
            Assert.That(() => tools.DecideBatch(rejectedBatch.Id, wrongToken, true), Throws.TypeOf<McpException>());
            Assert.That(() => tools.DecideBatch(Guid.NewGuid(), rejectedBatch.Token, true), Throws.TypeOf<McpException>());
            var hasPendingBatchAfterWrongCalls = this.pendingBatchStore.HasPendingBatch;
            var rejection = tools.DecideBatch(rejectedBatch.Id, rejectedBatch.Token, false, "Put it in the AOCS.");

            var appliedBatch = this.pendingBatchStore.TryAdd(changes);
            var application = tools.DecideBatch(appliedBatch.Id, appliedBatch.Token, true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hasPendingBatchAfterWrongCalls, Is.True);
                Assert.That(rejection.Status, Is.EqualTo(BatchReviewStatus.Rejected));
                Assert.That(rejection.Message, Does.Contain("rejected").And.Contain("Put it in the AOCS."));
                Assert.That(application.Status, Is.EqualTo(BatchReviewStatus.Applied));
                Assert.That(application.Result.CreatedElements, Is.EqualTo(new[] { createdElement }));
                Assert.That(application.Message, Does.Contain(createdElement.Id.ToString()));
                Assert.That(this.pendingBatchStore.HasPendingBatch, Is.False);
                Assert.That(() => tools.DecideBatch(appliedBatch.Id, appliedBatch.Token, true), Throws.TypeOf<McpException>());
                this.modelProvider.Verify(provider => provider.ApplyChanges(changes), Times.Once);
            }
        }

        [Test]
        public void DecideBatch_WithABatchThatCanNoLongerBeApplied_ReturnsTheProblems()
        {
            IReadOnlyList<ModelChange> changes = [new ModelChange { Identity = Guid.NewGuid().ToString(), Payload = null }];
            this.modelProvider.Setup(provider => provider.ApplyChanges(changes)).Returns(new ApplyChangesResult([], ["Change 1 (delete): no such element."]));

            var pendingBatch = this.pendingBatchStore.TryAdd(changes);
            var review = this.CreateTools().DecideBatch(pendingBatch.Id, pendingBatch.Token, true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(review.Status, Is.EqualTo(BatchReviewStatus.Refused));
                Assert.That(review.Result.Problems, Is.EqualTo(["Change 1 (delete): no such element."]));
                Assert.That(review.Message, Does.Contain("can no longer be applied"));
            }
        }

        [Test]
        public void ApplyChanges_OnEmptyModel_BuildsAModelReadByTheBudgetTools()
        {
            var emptyModelProvider = CreateEmptyModelProvider();
            var tools = new ConstructionTools(emptyModelProvider, CreateModelValidator(), this.pendingBatchStore, this.appHostDetector.Object, this.options);
            var budgetTools = new BudgetTools(emptyModelProvider);

            var creation = ReadResult(tools.ApplyChanges(
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "Camera", Payload = new ElementPayload { Type = "PartDefinition", Owner = "spacecraft", Name = "Camera" } },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Camera", Name = "mass", Value = 38 } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "camera", Definition = "Camera" } }
            ]));

            var createdIds = creation.CreatedElements.ToDictionary(created => created.QualifiedName, created => created.Id);
            var packageId = createdIds["Spacecraft"];
            var cameraMassId = createdIds["Spacecraft::Camera::mass"].ToString();
            var elementCount = emptyModelProvider.Elements.Count;

            var refusal = ReadResult(tools.ApplyChanges(
            [
                new ModelChange { Identity = cameraMassId, Payload = new ElementPayload { Value = 35 } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "unknownOwner", Name = "lens" } }
            ]));

            var elementCountAfterRefusal = emptyModelProvider.Elements.Count;
            var totalAfterRefusal = budgetTools.SumAttribute(packageId, "mass").Total;

            var untypedPart = ReadResult(tools.ApplyChanges([new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = packageId.ToString(), Name = "lens" } }]));
            var lensId = untypedPart.CreatedElements.Single().Id.ToString();

            var modification = ReadResult(tools.ApplyChanges([new ModelChange { Identity = cameraMassId, Payload = new ElementPayload { Value = 35 } }]));
            var totalAfterModification = budgetTools.SumAttribute(packageId, "mass").Total;

            var typing = ReadResult(tools.ApplyChanges([new ModelChange { Identity = lensId, Payload = new ElementPayload { Definition = createdIds["Spacecraft::Camera"].ToString() } }]));

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

        [Test]
        public void ApplyChanges_InAHostWithApps_PreviewsARequirementThatTheBatchBreaks()
        {
            var emptyModelProvider = CreateEmptyModelProvider();
            var tools = new ConstructionTools(emptyModelProvider, CreateModelValidator(), this.pendingBatchStore, this.appHostDetector.Object, this.options);
            var budgetTools = new BudgetTools(emptyModelProvider);

            // Built without the review: the host does not show MCP Apps yet.
            var creation = ReadResult(tools.ApplyChanges(
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "camera", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "camera" } },
                new ModelChange { Identity = "cameraMass", Payload = new ElementPayload { Type = "AttributeUsage", Owner = "camera", Name = "mass", Value = 38 } },
                new ModelChange
                {
                    Identity = "cameraMassRequirement",
                    Payload = new ElementPayload
                    {
                        Type = "RequirementUsage", Owner = "spacecraft", Name = "cameraMassRequirement", ReqId = "REQ-PL-001", Text = "The camera shall weigh at most 40 kg.",
                        Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 40 }
                    }
                },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = "cameraMassRequirement", SatisfyingPart = "camera" } },
                new ModelChange { Identity = "spareCamera", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "spareCamera" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "spareCamera", Name = "mass", Value = 38 } },
                new ModelChange { Payload = new ElementPayload { Type = "SatisfyRequirementUsage", SatisfiedRequirement = "cameraMassRequirement", SatisfyingPart = "spareCamera" } }
            ]));

            var packageId = creation.CreatedElements.Single(created => created.QualifiedName == "Spacecraft").Id;
            var cameraMassId = creation.CreatedElements.Single(created => created.QualifiedName == "Spacecraft::camera::mass").Id;
            var elementCount = emptyModelProvider.Elements.Count;

            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);

            var toolResult = tools.ApplyChanges([new ModelChange { Identity = cameraMassId.ToString(), Payload = new ElementPayload { Value = 45 } }]);
            var review = ReadReview(toolResult);
            var totalWhilePending = budgetTools.SumAttribute(packageId, "mass").Total;
            var elementCountWhilePending = emptyModelProvider.Elements.Count;

            var application = tools.DecideBatch(review.Preview.BatchId, review.Preview.Token, true);
            var totalAfterApplication = budgetTools.SumAttribute(packageId, "mass").Total;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(creation.Applied, Is.True);
                Assert.That(review.Status, Is.EqualTo(BatchReviewStatus.Pending));
                Assert.That(review.Preview.CreatedElements, Is.Empty);
                Assert.That(review.Preview.ModifiedElements, Is.EqualTo(new[] { new PreviewElement(cameraMassId, "Spacecraft::camera::mass", "AttributeUsage", Value: "38 → 45", Domain: "SYS") }));
                Assert.That(review.Preview.DeletedElements, Is.Empty);
                Assert.That(review.Preview.RequirementChanges, Is.EqualTo(new[]
                {
                    new RequirementChange(review.Preview.RequirementChanges[0].Id, "REQ-PL-001", "cameraMassRequirement", "Spacecraft::camera", RequirementStatus.Satisfied, RequirementStatus.NotSatisfied,
                        "38 → 45", -7)
                }));
                Assert.That(review.Preview.BudgetImpacts, Is.EqualTo(new[] { new BudgetImpact("Spacecraft::camera", "mass", 38, 45, null) }));
                Assert.That(totalWhilePending, Is.EqualTo(76));
                Assert.That(elementCountWhilePending, Is.EqualTo(elementCount));
                Assert.That(application.Status, Is.EqualTo(BatchReviewStatus.Applied));
                Assert.That(totalAfterApplication, Is.EqualTo(83));
            }
        }

        [Test]
        public void ApplyChanges_InAHostWithApps_PreviewsTheDefinitionValueAndDomainOfTheCreatedElements()
        {
            var emptyModelProvider = CreateEmptyModelProvider();
            var tools = new ConstructionTools(emptyModelProvider, CreateModelValidator(), this.pendingBatchStore, this.appHostDetector.Object, this.options);
            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);

            var review = ReadReview(tools.ApplyChanges(
            [
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "Package", Name = "Spacecraft" } },
                new ModelChange { Identity = "Radiator", Payload = new ElementPayload { Type = "PartDefinition", Owner = "spacecraft", Name = "Radiator" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "Radiator", Name = "mass", Value = 2, Unit = "kg" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "Radiator", Name = "fin" } },
                new ModelChange { Identity = "eosat1", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "eosat1" } },
                new ModelChange { Identity = "thermal", Payload = new ElementPayload { Type = "PartUsage", Owner = "eosat1", Name = "thermalSubsystem" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "thermal", Name = "radiator", Definition = "Radiator" } },
                new ModelChange { Identity = "heater", Payload = new ElementPayload { Type = "PartUsage", Owner = "thermal", Name = "heater" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "heater", Name = "power", Value = 5, Unit = "W" } },
                new ModelChange { Payload = new ElementPayload { Type = "RequirementUsage", Owner = "spacecraft", Name = "heaterPower", ReqId = "REQ-THE-001", Text = "The heater shall draw at most 25 W." } },
                new ModelChange { Identity = "testBench", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "testBench" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "testBench", Name = "mass", Value = 3 } },
                new ModelChange { Identity = "sensor", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "sensor" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "sensor", Name = "mass", Value = 4, Text = "Dry mass of the unit [kg]." } }
            ]));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(review.Status, Is.EqualTo(BatchReviewStatus.Pending));
                Assert.That(review.Preview.CreatedElements.Select(element => (element.QualifiedName, element.Definition, element.Value, element.Domain)), Is.EqualTo(new (string, string, string, string)[]
                {
                    ("Spacecraft", null, null, null),
                    ("Spacecraft::Radiator", null, null, null),
                    ("Spacecraft::Radiator::mass", null, "2 kg", null),
                    ("Spacecraft::Radiator::fin", null, null, null),
                    ("Spacecraft::eosat1", null, "2 kg · 5 W", PreviewElement.SystemDomain),
                    ("Spacecraft::eosat1::thermalSubsystem", null, "2 kg · 5 W", "THE"),
                    ("Spacecraft::eosat1::thermalSubsystem::radiator", "Radiator", "2 kg", "THE"),
                    ("Spacecraft::eosat1::thermalSubsystem::heater", null, "5 W", "THE"),
                    ("Spacecraft::eosat1::thermalSubsystem::heater::power", null, "5 W", "THE"),
                    ("Spacecraft::heaterPower", null, "REQ-THE-001", null),
                    ("Spacecraft::testBench", null, "mass 3", PreviewElement.SystemDomain),
                    ("Spacecraft::testBench::mass", null, "3", PreviewElement.SystemDomain),
                    ("Spacecraft::sensor", null, "4 kg", PreviewElement.SystemDomain),
                    ("Spacecraft::sensor::mass", null, "4 kg", PreviewElement.SystemDomain)
                }));
            }
        }

        [Test]
        public void ApplyChanges_InAHostWithApps_PreviewsTheDomainOfTheElementsOfAMission()
        {
            var emptyModelProvider = CreateEmptyModelProvider();
            var tools = new ConstructionTools(emptyModelProvider, CreateModelValidator(), this.pendingBatchStore, this.appHostDetector.Object, this.options);
            this.appHostDetector.Setup(detector => detector.ShowsApps(It.IsAny<ClientCapabilities>())).Returns(true);

            var review = ReadReview(tools.ApplyChanges(
            [
                new ModelChange { Identity = "missionPackage", Payload = new ElementPayload { Type = "Package", Name = "Mission" } },
                new ModelChange { Identity = "mission", Payload = new ElementPayload { Type = "PartUsage", Owner = "missionPackage", Name = "mission" } },
                new ModelChange { Identity = "spacecraft", Payload = new ElementPayload { Type = "PartUsage", Owner = "mission", Name = "spacecraft" } },
                new ModelChange { Payload = new ElementPayload { Type = "AttributeUsage", Owner = "spacecraft", Name = "mass", Value = 120 } },
                new ModelChange { Identity = "aocs", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "aocsSubsystem" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "aocs", Name = "reactionWheel" } },
                new ModelChange { Identity = "thermal", Payload = new ElementPayload { Type = "PartUsage", Owner = "spacecraft", Name = "thermalSubsystem" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "thermal", Name = "heater" } },
                new ModelChange { Identity = "groundStation", Payload = new ElementPayload { Type = "PartUsage", Owner = "mission", Name = "groundStation" } },
                new ModelChange { Payload = new ElementPayload { Type = "PartUsage", Owner = "groundStation", Name = "antenna" } }
            ]));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(review.Status, Is.EqualTo(BatchReviewStatus.Pending));
                Assert.That(review.Preview.CreatedElements.Select(element => (element.QualifiedName, element.Domain)), Is.EqualTo(new (string, string)[]
                {
                    ("Mission", null),
                    ("Mission::mission", PreviewElement.SystemDomain),
                    ("Mission::mission::spacecraft", PreviewElement.SystemDomain),
                    ("Mission::mission::spacecraft::mass", PreviewElement.SystemDomain),
                    ("Mission::mission::spacecraft::aocsSubsystem", "AOC"),
                    ("Mission::mission::spacecraft::aocsSubsystem::reactionWheel", "AOC"),
                    ("Mission::mission::spacecraft::thermalSubsystem", "THE"),
                    ("Mission::mission::spacecraft::thermalSubsystem::heater", "THE"),
                    ("Mission::mission::groundStation", "GRO"),
                    ("Mission::mission::groundStation::antenna", "GRO")
                }));
            }
        }

        /// <summary>
        /// Creates the <see cref="ConstructionTools"/> on the mocked model provider and validator.
        /// </summary>
        /// <param name="constructionOptions">The options, or <c>null</c> for the default ones.</param>
        /// <returns>The <see cref="ConstructionTools"/>.</returns>
        private ConstructionTools CreateTools(IOptions<ConstructionOptions> constructionOptions = null)
        {
            return new ConstructionTools(this.modelProvider.Object, this.modelValidator.Object, this.pendingBatchStore, this.appHostDetector.Object, constructionOptions ?? this.options);
        }

        /// <summary>
        /// Creates an <see cref="InMemoryModelProvider"/> that holds the empty model of <c>Empty.json</c>.
        /// </summary>
        /// <returns>The <see cref="InMemoryModelProvider"/>.</returns>
        private static InMemoryModelProvider CreateEmptyModelProvider()
        {
            var emptyModelProvider = new InMemoryModelProvider(new ModelChangeApplier(new Serializer(), new DeSerializer()), new DeSerializer());
            emptyModelProvider.LoadModel(new Uri(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "Empty.json")));
            return emptyModelProvider;
        }

        /// <summary>
        /// Creates a <see cref="ModelValidator"/> with all the rules.
        /// </summary>
        /// <returns>The <see cref="ModelValidator"/>.</returns>
        private static ModelValidator CreateModelValidator()
        {
            IValidationRule[] rules = [new BrokenReferenceRule(), new OrphanElementRule(), new DuplicateQualifiedNameRule(), new UntypedUsageRule(), new RequirementWithoutConstraintRule(),
                new RequirementWithoutSatisfyRule(), new UnusedDefinitionRule(), new RequirementWithoutIdRule()];

            return new ModelValidator(rules);
        }

        /// <summary>
        /// Reads the <see cref="ApplyChangesResult"/> that apply_changes writes in JSON for the AI assistant.
        /// </summary>
        /// <param name="toolResult">The <see cref="CallToolResult"/> of apply_changes.</param>
        /// <returns>The <see cref="ApplyChangesResult"/>.</returns>
        private static ApplyChangesResult ReadResult(CallToolResult toolResult)
        {
            return JsonSerializer.Deserialize<ApplyChangesResult>(toolResult.Content.OfType<TextContentBlock>().Single().Text, McpJsonUtilities.DefaultOptions);
        }

        /// <summary>
        /// Reads the <see cref="BatchReview"/> that apply_changes sends to the view as structured content.
        /// </summary>
        /// <param name="toolResult">The <see cref="CallToolResult"/> of apply_changes.</param>
        /// <returns>The <see cref="BatchReview"/>.</returns>
        private static BatchReview ReadReview(CallToolResult toolResult)
        {
            return toolResult.StructuredContent.Value.Deserialize<BatchReview>(McpJsonUtilities.DefaultOptions);
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
