// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionTools.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text.Json;

    using Microsoft.Extensions.Options;

    using ModelContextProtocol;
    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Requirements;
    using Mycelium.Fabric.Mcp.Resources;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Validation;
    using Mycelium.Fabric.Mcp.Values;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// The MCP tools that let an AI assistant build and modify the SysML v2 model. The assistant describes the changes in a
    /// structured form and never writes SysML: the server builds the elements and checks them. When the host shows MCP Apps, a
    /// valid batch waits for the decision of the user, taken in the view of the batch, before it is applied.
    /// </summary>
    [McpServerToolType]
    public class ConstructionTools
    {
        /// <summary>
        /// The maximum number of new issues returned with an applied batch, the same as a page of validate_model.
        /// </summary>
        private const int MaximumIssueCount = 20;

        /// <summary>
        /// The message for the AI assistant when a batch waits for the decision of the user.
        /// </summary>
        private const string PendingMessage = "The batch is valid and waits for the decision of the user, who sees it in the conversation. Do not send another batch: "
            + "wait for the user's message, which tells whether it has been applied, with the identifiers of the created elements, or rejected, with a comment.";

        /// <summary>
        /// The problem returned when a batch is sent while another one waits for the decision of the user.
        /// </summary>
        private const string BatchAlreadyPendingProblem = "A batch already waits for the decision of the user, and nothing has been applied. "
            + "Do not send another batch: wait for the user's message, which tells whether the waiting batch has been applied or rejected.";

        /// <summary>
        /// The message returned when a batch is sent while another one waits for the decision of the user, in a host that shows
        /// the waiting batch again.
        /// </summary>
        private const string BatchShownAgainMessage = "A batch already waits for the decision of the user: the batch just sent has been ignored, and the user sees the waiting batch again in the conversation. "
            + "Do not send another batch: wait for the user's message, which tells whether the waiting batch has been applied or rejected.";

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="IModelValidator"/> that finds the issues of the model before and after a batch.
        /// </summary>
        private readonly IModelValidator modelValidator;

        /// <summary>
        /// The <see cref="IPendingBatchStore"/> that keeps the batch waiting for the decision of the user.
        /// </summary>
        private readonly IPendingBatchStore pendingBatchStore;

        /// <summary>
        /// The <see cref="IAppHostDetector"/> that tells whether the host shows the view of a batch.
        /// </summary>
        private readonly IAppHostDetector appHostDetector;

        /// <summary>
        /// The <see cref="ConstructionOptions"/>, which tell whether the new issues are returned and whether a batch waits for
        /// the decision of the user.
        /// </summary>
        private readonly ConstructionOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstructionTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="modelValidator">The <see cref="IModelValidator"/> that finds the issues of the model.</param>
        /// <param name="pendingBatchStore">The <see cref="IPendingBatchStore"/> that keeps the batch waiting for the user.</param>
        /// <param name="appHostDetector">The <see cref="IAppHostDetector"/> that tells whether the host shows MCP Apps.</param>
        /// <param name="options">The <see cref="ConstructionOptions"/> read from the configuration of the host.</param>
        /// <exception cref="ArgumentNullException">Thrown when a parameter is <c>null</c>.</exception>
        public ConstructionTools(IModelProvider modelProvider, IModelValidator modelValidator, IPendingBatchStore pendingBatchStore, IAppHostDetector appHostDetector,
            IOptions<ConstructionOptions> options)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(modelValidator);
            ArgumentNullException.ThrowIfNull(pendingBatchStore);
            ArgumentNullException.ThrowIfNull(appHostDetector);
            ArgumentNullException.ThrowIfNull(options);

            this.modelProvider = modelProvider;
            this.modelValidator = modelValidator;
            this.pendingBatchStore = pendingBatchStore;
            this.appHostDetector = appHostDetector;
            this.options = options.Value;
        }

        /// <summary>
        /// Applies a batch of changes to the model, all or nothing, or keeps it until the user decides when the host shows the
        /// view of the batch and <see cref="ConstructionOptions.ReviewBatches"/> is on.
        /// </summary>
        /// <param name="changes">The changes to apply, in order.</param>
        /// <param name="server">
        /// The <see cref="McpServer"/> that receives the call, given by the SDK, whose client tells whether the host shows MCP
        /// Apps; <c>null</c> when unknown.
        /// </param>
        /// <returns>
        /// The <see cref="CallToolResult"/>: for the AI assistant, the <see cref="ApplyChangesResult"/> in JSON, or a message
        /// when the batch waits for the user; for the view, a <see cref="BatchReview"/> as structured content, with the secret
        /// token of the batch when it waits.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="changes"/> is <c>null</c> or empty.
        /// </exception>
        [McpServerTool(Name = "apply_changes", ReadOnly = false, Destructive = true)]
        [McpAppUi(ResourceUri = BatchReviewResource.Uri)]
        [Description("Applies a batch of changes to the model, in order and all or nothing: if one change is invalid, nothing is applied and every problem is returned, so that the whole batch can be corrected and sent again. "
            + "Each change is shaped like a data version of a commit: an identity and a payload. "
            + "Create: a payload with a type (the SysML v2 metaclass: Package, PartDefinition, PartUsage, AttributeUsage, RequirementUsage...), a name, an owner (none for the top level) "
            + "and, if needed, a definition (for a part), a value and a unit (for an attribute: 38 with kg, true, 'S-band'), a text (required for a requirement), a reqId and a constraint (for a requirement: "
            + "an attribute or path of the subject, an operator, a limit with an optional unit or a limitAttribute, a margin, and kind Assumption for an assumption); "
            + "the identity is then an optional temporary name, that later changes of this batch and of the next batches use instead of an identifier. "
            + "A SatisfyRequirementUsage (satisfy link) takes a satisfiedRequirement and a satisfyingPart (a part, an action or an interface) instead of a name; one per element that satisfies the requirement. "
            + "A definition or a usage takes specializes for :> (part def Camera :> Equipment). A PerformActionUsage takes an owner (the performer) and a performed action usage. "
            + "Ports: a PortDefinition owns the attributes or items that cross the port, with a direction (In, Out, Inout); a PortUsage takes it as definition, with conjugated true on the receiving side. "
            + "Connectors: a ConnectionUsage, an InterfaceUsage (between two ports, definition: an InterfaceDefinition whose ends are ports created with isEnd true), a BindingConnectorAsUsage, "
            + "an AllocationUsage (allocate the first end to the second) or a SuccessionAsUsage (first the first end, then the second, between actions) "
            + "takes two ends, paths of names from its owner (['payloadSubsystem.camera.dataOut', 'dataHandlingSubsystem.obc.dataIn']), and an optional name. "
            + "Update: the identity of the element and a payload with only the properties to change (name, definition, specializes, value, text, direction, conjugated, reqId or constraint). "
            + "Delete: the identity of the element and no payload; refused while another element references it, for example a port connected by an interface. Existing elements are designated by their identifier (Id). "
            + "Once a batch is applied, the result also lists the errors and warnings that it has introduced (newIssues, at most 20, and newIssueCount) and the number of issues it has resolved: "
            + "correct the errors in the next batch; a warning can wait when a later batch of your plan resolves it. "
            + "When the host shows the batch to the user, a valid batch is not applied at once: it waits for the decision of the user. "
            + "Then send no other batch and wait for the user's message, which tells whether it has been applied, with the identifiers, or rejected, with a comment.")]
        [return: Description("Whether the batch has been applied; if so, the identifier, qualified name and type of each created package, definition and usage, the errors and warnings that the batch has introduced "
            + "and the number of issues it has resolved; if not, the problems, each with the number of its change. Or a message telling that the batch waits for the decision of the user.")]
        public CallToolResult ApplyChanges([Description("The changes to apply, in order.")] IReadOnlyList<ModelChange> changes,
            [Description("Bound by the SDK to the server that receives the call, not part of the tool schema.")] McpServer server = null)
        {
            if (changes == null || changes.Count == 0)
            {
                throw new McpException("The batch contains no change.");
            }

            var showsApps = this.appHostDetector.ShowsApps(server?.ClientCapabilities);

            var waitingBatch = this.pendingBatchStore.PendingBatch;

            if (waitingBatch != null)
            {
                // The model cannot change while a batch waits, so its preview is still valid: the user gets its buttons back.
                return showsApps
                    ? this.CreatePendingResult(waitingBatch, this.modelProvider.PreviewChanges(waitingBatch.Changes), BatchShownAgainMessage)
                    : CreateToolResult(new ApplyChangesResult([], [BatchAlreadyPendingProblem]), false);
            }

            if (!showsApps || !this.options.ReviewBatches)
            {
                return CreateToolResult(this.ApplyAndReport(changes), showsApps);
            }

            var modelPreview = this.modelProvider.PreviewChanges(changes);

            if (!modelPreview.IsValid)
            {
                return CreateToolResult(new ApplyChangesResult([], modelPreview.Problems), true);
            }

            var pendingBatch = this.pendingBatchStore.TryAdd(changes);

            if (pendingBatch == null)
            {
                return CreateToolResult(new ApplyChangesResult([], [BatchAlreadyPendingProblem]), true);
            }

            return this.CreatePendingResult(pendingBatch, modelPreview, PendingMessage);
        }

        /// <summary>
        /// Applies or rejects the batch that waits for the decision of the user. Only the view of the batch calls this tool: the
        /// host hides it from the AI assistant, and the secret token proves that the caller has received the preview of this
        /// batch.
        /// </summary>
        /// <param name="batchId">The identifier of the waiting batch.</param>
        /// <param name="token">The secret token of the waiting batch.</param>
        /// <param name="apply"><c>true</c> to apply the batch, <c>false</c> to reject it.</param>
        /// <param name="comment">The optional comment of the user, passed on to the AI assistant.</param>
        /// <returns>
        /// The <see cref="BatchReview"/> of the batch: applied, refused when it can no longer be applied, or rejected, with the
        /// message that the view writes in the conversation.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when no batch waits with this identifier and this token; the waiting batch, if any, is then kept.
        /// </exception>
        [McpServerTool(Name = "decide_batch", ReadOnly = false, Destructive = true, UseStructuredContent = true)]
        [McpAppUi(ResourceUri = BatchReviewResource.Uri, Visibility = [McpUiToolVisibility.App])]
        [Description("Applies or rejects the batch of changes that waits for the decision of the user. Called by the view of the batch only.")]
        [return: Description("The status of the batch (Applied, Refused or Rejected), the message for the AI assistant and, unless rejected, the result of apply_changes.")]
        public BatchReview DecideBatch([Description("The identifier of the waiting batch (batchId of the preview).")] Guid batchId,
            [Description("The secret token of the waiting batch (token of the preview).")] string token,
            [Description("True to apply the batch, false to reject it.")] bool apply,
            [Description("The optional comment of the user, passed on to the AI assistant.")] string comment = null)
        {
            var pendingBatch = this.pendingBatchStore.TryTake(batchId, token)
                ?? throw new McpException("No batch waits for a decision with this identifier and this token.");

            var commentText = string.IsNullOrWhiteSpace(comment) ? string.Empty : $" Comment of the user: \"{comment.Trim()}\"";

            if (!apply)
            {
                return new BatchReview(BatchReviewStatus.Rejected, $"The user has rejected the batch: nothing has been applied.{commentText} "
                    + "Correct the batch as the user asks and send it again, or ask the user what to change.");
            }

            var result = this.ApplyAndReport(pendingBatch.Changes);
            var resultText = JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions);

            return result.Applied
                ? new BatchReview(BatchReviewStatus.Applied, $"The user has applied the batch.{commentText} Result of apply_changes: {resultText}") { Result = result }
                : new BatchReview(BatchReviewStatus.Refused, $"The user has approved the batch, but it can no longer be applied and nothing has been applied.{commentText} "
                    + $"Correct it and send it again. Result of apply_changes: {resultText}") { Result = result };
        }

        /// <summary>
        /// Creates the <see cref="CallToolResult"/> of a batch that has been applied or refused: the
        /// <see cref="ApplyChangesResult"/> in JSON for the AI assistant and, when the host shows the view, a
        /// <see cref="BatchReview"/> as structured content.
        /// </summary>
        /// <param name="result">The <see cref="ApplyChangesResult"/> of the batch.</param>
        /// <param name="showsApps">Whether the host shows the view of the batch.</param>
        /// <returns>The <see cref="CallToolResult"/>.</returns>
        private static CallToolResult CreateToolResult(ApplyChangesResult result, bool showsApps)
        {
            var resultText = JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions);
            var toolResult = new CallToolResult { Content = [new TextContentBlock { Text = resultText }] };

            if (showsApps)
            {
                var review = new BatchReview(result.Applied ? BatchReviewStatus.Applied : BatchReviewStatus.Refused, resultText) { Result = result };
                toolResult.StructuredContent = JsonSerializer.SerializeToElement(review, McpJsonUtilities.DefaultOptions);
            }

            return toolResult;
        }

        /// <summary>
        /// Compares the issues of the model before and after a batch, by rule and element.
        /// </summary>
        /// <param name="issuesBefore">The issues before the batch.</param>
        /// <param name="issuesAfter">The issues after the batch.</param>
        /// <returns>The issues that the batch introduces, and the number of issues that it resolves.</returns>
        private static (List<ValidationIssue> NewIssues, int ResolvedIssueCount) CompareIssues(List<ValidationIssue> issuesBefore, List<ValidationIssue> issuesAfter)
        {
            var keysBefore = issuesBefore.Select(issue => (issue.Rule, issue.ElementId)).ToHashSet();
            var keysAfter = issuesAfter.Select(issue => (issue.Rule, issue.ElementId)).ToHashSet();

            var newIssues = issuesAfter.Where(issue => !keysBefore.Contains((issue.Rule, issue.ElementId))).ToList();
            var resolvedIssueCount = issuesBefore.Count(issue => !keysAfter.Contains((issue.Rule, issue.ElementId)));

            return (newIssues, resolvedIssueCount);
        }

        /// <summary>
        /// Gets the requirements whose status, constrained value or gap to the limit differs between two versions of the model,
        /// compared by the identifier of the requirement usage and the element that satisfies it, since the checker checks a
        /// requirement once per such element.
        /// </summary>
        /// <param name="elementsBefore">The elements of the model before the batch.</param>
        /// <param name="elementsAfter">The elements of the model after the batch.</param>
        /// <returns>The requirements whose status changes, created and deleted ones included, ordered by their identifier.</returns>
        private static List<RequirementChange> CompareRequirements(IReadOnlyList<IElement> elementsBefore, IReadOnlyList<IElement> elementsAfter)
        {
            var checksBefore = new RequirementChecker(elementsBefore).CheckRequirements().ToDictionary(check => (check.Id, check.SatisfiedBy));
            var checksAfter = new RequirementChecker(elementsAfter).CheckRequirements().ToDictionary(check => (check.Id, check.SatisfiedBy));

            return checksBefore.Keys.Union(checksAfter.Keys)
                .Select(key => (Before: checksBefore.GetValueOrDefault(key), After: checksAfter.GetValueOrDefault(key)))
                .Where(checks => checks.Before?.Status != checks.After?.Status || !AreEqual(checks.Before?.Value, checks.After?.Value) || !AreEqual(checks.Before?.Gap, checks.After?.Gap))
                .Select(checks => (Check: checks.After ?? checks.Before, checks.Before, checks.After))
                .Select(checks => new RequirementChange(checks.Check.Id, checks.Check.ReqId, checks.Check.Name, checks.Check.SatisfiedBy, checks.Before?.Status, checks.After?.Status,
                    DescribeRequirementValue(checks.Before, checks.After), checks.After?.Gap - checks.Before?.Gap))
                .OrderBy(change => change.ReqId ?? change.Name, StringComparer.Ordinal)
                .ThenBy(change => change.SatisfiedBy, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Tells whether two values, either of which may be missing, are equal.
        /// </summary>
        /// <param name="left">The first value, or <c>null</c>.</param>
        /// <param name="right">The second value, or <c>null</c>.</param>
        /// <returns><c>true</c> when both are missing or both are equal.</returns>
        private static bool AreEqual(double? left, double? right)
        {
            return left.HasValue == right.HasValue && (!left.HasValue || NumberValue.AreEqual(left.Value, right.Value));
        }

        /// <summary>
        /// Describes the constrained value of a requirement before and after a batch, with its unit.
        /// </summary>
        /// <param name="before">The check before the batch, or <c>null</c> when the batch creates the requirement.</param>
        /// <param name="after">The check after the batch, or <c>null</c> when the batch deletes the requirement.</param>
        /// <returns>The value, for example <c>150.96 → 154.68 kg</c> or <c>20 W</c>, or <c>null</c> when neither check has one.</returns>
        private static string DescribeRequirementValue(RequirementCheck before, RequirementCheck after)
        {
            var check = after ?? before;

            if (check.Value == null && before?.Value == null)
            {
                return null;
            }

            var unit = check.Unit == null ? string.Empty : $" {check.Unit}";
            string Format(double? value) => value == null ? "none" : NumberValue.Format(value.Value);

            return before == null || after == null || AreEqual(before.Value, after.Value) ? $"{Format(check.Value)}{unit}" : $"{Format(before.Value)} → {Format(after.Value)}{unit}";
        }

        /// <summary>
        /// Applies a batch of changes to the model and, when <see cref="ConstructionOptions.ReportNewIssues"/> is on, adds to
        /// the result the issues that it has introduced and the number of issues that it has resolved.
        /// </summary>
        /// <param name="changes">The changes to apply, in order.</param>
        /// <returns>The <see cref="ApplyChangesResult"/> that tells whether the batch has been applied.</returns>
        private ApplyChangesResult ApplyAndReport(IReadOnlyList<ModelChange> changes)
        {
            if (!this.options.ReportNewIssues)
            {
                return this.modelProvider.ApplyChanges(changes);
            }

            var issuesBefore = this.FindIssues(this.modelProvider.Elements);
            var result = this.modelProvider.ApplyChanges(changes);

            if (!result.Applied)
            {
                return result;
            }

            var (newIssues, resolvedIssueCount) = CompareIssues(issuesBefore, this.FindIssues(this.modelProvider.Elements));

            return result with { NewIssues = [.. newIssues.Take(MaximumIssueCount)], NewIssueCount = newIssues.Count, ResolvedIssueCount = resolvedIssueCount };
        }

        /// <summary>
        /// Creates the <see cref="CallToolResult"/> of a batch that waits for the decision of the user: the message for the AI
        /// assistant and, for the view, a pending <see cref="BatchReview"/> as structured content, with the secret token of the
        /// batch.
        /// </summary>
        /// <param name="pendingBatch">The waiting <see cref="PendingBatch"/>.</param>
        /// <param name="modelPreview">The <see cref="ModelPreview"/> of the waiting batch.</param>
        /// <param name="message">The message for the AI assistant.</param>
        /// <returns>The <see cref="CallToolResult"/>.</returns>
        private CallToolResult CreatePendingResult(PendingBatch pendingBatch, ModelPreview modelPreview, string message)
        {
            var review = new BatchReview(BatchReviewStatus.Pending, message) { Preview = this.CreatePreview(pendingBatch, modelPreview) };

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = message }],
                StructuredContent = JsonSerializer.SerializeToElement(review, McpJsonUtilities.DefaultOptions)
            };
        }

        /// <summary>
        /// Creates the <see cref="BatchPreview"/> shown to the user from the <see cref="ModelPreview"/> of a valid batch: the
        /// elements that it creates, modifies and deletes, the totals of mass and power and the requirements that it changes and
        /// the issues that it introduces and resolves.
        /// </summary>
        /// <param name="pendingBatch">The waiting <see cref="PendingBatch"/>.</param>
        /// <param name="modelPreview">The <see cref="ModelPreview"/> of the batch.</param>
        /// <returns>The <see cref="BatchPreview"/>.</returns>
        private BatchPreview CreatePreview(PendingBatch pendingBatch, ModelPreview modelPreview)
        {
            var (newIssues, resolvedIssueCount) = CompareIssues(this.FindIssues(this.modelProvider.Elements), this.FindIssues(modelPreview.Elements));
            var requirementChanges = CompareRequirements(this.modelProvider.Elements, modelPreview.Elements);
            var elementsBefore = this.modelProvider.Elements.ToDictionary(element => element.Id);
            var elementsAfter = modelPreview.Elements.ToDictionary(element => element.Id);

            var createdElements = modelPreview.CreatedElements
                .Select(element => PreviewElement.Create(element.Id, element.QualifiedName, element.Type, null, elementsAfter.GetValueOrDefault(element.Id)))
                .ToList();

            var modifiedElements = modelPreview.ModifiedElements
                .Select(element => PreviewElement.Create(element.Id, element.QualifiedName, element.Type, elementsBefore.GetValueOrDefault(element.Id),
                    elementsAfter.GetValueOrDefault(element.Id)))
                .ToList();

            var deletedElements = modelPreview.DeletedElements
                .Select(element => PreviewElement.Create(element.Id, element.QualifiedName, element.Type, elementsBefore.GetValueOrDefault(element.Id), null))
                .ToList();

            var budgetImpacts = BudgetImpact.Compare(this.modelProvider.Elements, modelPreview.Elements);

            return new BatchPreview(pendingBatch.Id, pendingBatch.Token, createdElements, modifiedElements, deletedElements, budgetImpacts, requirementChanges,
                [.. newIssues.Take(MaximumIssueCount)], newIssues.Count, resolvedIssueCount);
        }

        /// <summary>
        /// Finds the errors and warnings of a model, the information being left out: a definition is often unused until a
        /// later batch creates its usages.
        /// </summary>
        /// <param name="elements">The elements of the model.</param>
        /// <returns>The issues found, the most serious first.</returns>
        private List<ValidationIssue> FindIssues(IReadOnlyList<IElement> elements)
        {
            return this.modelValidator.Validate(elements)
                .Where(issue => issue.Severity <= ValidationSeverity.Warning)
                .ToList();
        }
    }
}
