// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementExtractionTools.cs" company="Starion Group S.A.">
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
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;
    using ModelContextProtocol.Extensions.Apps;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extraction;
    using Mycelium.Fabric.Mcp.Resources;
    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// The MCP tools that let the user review the requirements that the AI assistant has found in a section of a document,
    /// before they are imported: the assistant proposes them, the server checks them, and the user imports the ones they keep
    /// from the view of the extraction, in one batch.
    /// </summary>
    [McpServerToolType]
    public class RequirementExtractionTools
    {
        /// <summary>
        /// The message for the AI assistant when the user reviews the proposal in the view.
        /// </summary>
        private const string ReviewedInViewMessage = """
            The user reviews these requirements in a view and imports the ones they keep, in one batch. Do not send them with apply_changes: wait for the user's message, which tells what has been imported.
            """;

        /// <summary>
        /// The message for the AI assistant when the host shows no view.
        /// </summary>
        private const string NoViewMessage = """
            Nothing has been modelled and the host shows no view. Send one apply_changes batch for these requirements: leave out the ones already modelled or repeated; for the others with findings, correct what the server reports; create a requirement to clarify without constraint unless the user approves its rewrite. Then tell the user the clarifications and the missedIds.
            """;

        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="IDocumentReader"/> that reads the documents.
        /// </summary>
        private readonly IDocumentReader documentReader;

        /// <summary>
        /// The <see cref="IRequirementProposalStore"/> that keeps the proposals until the user imports them.
        /// </summary>
        private readonly IRequirementProposalStore proposalStore;

        /// <summary>
        /// The <see cref="IPendingBatchStore"/> that tells whether a batch waits for the decision of the user.
        /// </summary>
        private readonly IPendingBatchStore pendingBatchStore;

        /// <summary>
        /// The <see cref="IAppHostDetector"/> that tells whether the host shows the view of the extraction.
        /// </summary>
        private readonly IAppHostDetector appHostDetector;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequirementExtractionTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="documentReader">The <see cref="IDocumentReader"/> that reads the documents.</param>
        /// <param name="proposalStore">The <see cref="IRequirementProposalStore"/> that keeps the proposals.</param>
        /// <param name="pendingBatchStore">The <see cref="IPendingBatchStore"/> that keeps the batch waiting for the user.</param>
        /// <param name="appHostDetector">The <see cref="IAppHostDetector"/> that tells whether the host shows MCP Apps.</param>
        /// <exception cref="ArgumentNullException">Thrown when a parameter is <c>null</c>.</exception>
        public RequirementExtractionTools(IModelProvider modelProvider, IDocumentReader documentReader, IRequirementProposalStore proposalStore, IPendingBatchStore pendingBatchStore,
            IAppHostDetector appHostDetector)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(documentReader);
            ArgumentNullException.ThrowIfNull(proposalStore);
            ArgumentNullException.ThrowIfNull(pendingBatchStore);
            ArgumentNullException.ThrowIfNull(appHostDetector);

            this.modelProvider = modelProvider;
            this.documentReader = documentReader;
            this.proposalStore = proposalStore;
            this.pendingBatchStore = pendingBatchStore;
            this.appHostDetector = appHostDetector;
        }

        /// <summary>
        /// Checks the requirements that the AI assistant has found in a section of a document and shows them to the user, who
        /// imports the ones they keep from the view; changes nothing.
        /// </summary>
        /// <param name="fileName">The simple name of the file in the document folder.</param>
        /// <param name="section">The number of the section, as the outline gives it; its subsections are included.</param>
        /// <param name="requirements">The <see cref="ProposedRequirement"/>s found in the section.</param>
        /// <param name="ownerId">The <c>Id</c> of the package that owns the requirements or the new package, or <c>null</c>.</param>
        /// <param name="newPackage">The name of a package to create for the requirements, or <c>null</c>.</param>
        /// <param name="idPattern">The regular expression of the identifiers of the document, or <c>null</c>.</param>
        /// <param name="server">The <see cref="McpServer"/> of the request, which tells what the host can show.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> of the request, canceled when the client cancels it. It is not a parameter of the
        /// tool.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> whose result is the <see cref="CallToolResult"/>: for the AI assistant, a message and the
        /// <see cref="RequirementProposal"/> in JSON; for the view, when the host shows MCP Apps, the proposal kept by the server,
        /// with its token, as structured content.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when the document cannot be read, when <paramref name="section"/> is not the number of a section, or when the
        /// requirements or the package are invalid.
        /// </exception>
        [McpServerTool(Name = "propose_requirements", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [McpAppUi(ResourceUri = RequirementExtractionResource.Uri)]
        [Description("Proposes the requirements that you have found in one section of a document of the document folder, and its subsections, before they are modelled. Changes nothing. "
            + "Give each requirement with its identifier, a name, the statement copied word for word, its kind, a constraint when the statement sets a numeric limit, "
            + "and for a requirement to clarify (TBD, vague term, several requirements in one) the reason and, if the document allows it, a rewritten statement. "
            + "The server checks each one: statement not written as such in the section, identifier already modelled or given twice, requirement that apply_changes would refuse; "
            + "with idPattern, it also lists the identifiers of the section that the list misses. "
            + "When the host shows the result in a view, the user imports the requirements they keep from it: then do not use apply_changes for them, wait for the user's message.")]
        [return: Description("A message that tells what to do next, then the proposal: the section, the package, each requirement with where its statement is written and the findings of the server, "
            + "and the identifiers missed.")]
        public async Task<CallToolResult> ProposeRequirementsAsync(
            [Description("The simple name of the file in the document folder, for example 'EOSat1-requirements.md'.")] string fileName,
            [Description("The number of the section, as the outline of read_document gives it. Its subsections are included.")] int section,
            [Description("The requirements that the section and its subsections define and that the model does not have yet.")] IReadOnlyList<ProposedRequirement> requirements,
            [Description("Optional: the identifier (Id) of the package that owns the requirements, or under which newPackage is created.")] Guid? ownerId = null,
            [Description("Optional: the name of a package to create for the requirements, for example 'PayloadRequirements'.")] string newPackage = null,
            [Description("Optional: the regular expression of the identifiers, as in read_document, to list the identifiers that the list misses.")] string idPattern = null,
            [Description("Bound by the SDK to the server that receives the call, not part of the tool schema.")] McpServer server = null,
            [Description("Bound by the SDK to the cancellation of the request, not part of the tool schema.")] CancellationToken cancellationToken = default)
        {
            var document = await this.documentReader.ReadAsync(fileName, cancellationToken);

            if (section < 1 || section > document.Sections.Count)
            {
                throw new McpException($"The document '{document.FileName}' has no section {section}: use a number from 1 to {document.Sections.Count}, as given by the outline.");
            }

            var proposal = RequirementProposalBuilder.Build(this.modelProvider, document, section, requirements, ownerId, newPackage, idPattern);

            if (!this.appHostDetector.ShowsApps(server?.ClientCapabilities))
            {
                return CreateProposalResult(NoViewMessage, proposal);
            }

            var keptProposal = this.proposalStore.Add(proposal);
            var toolResult = CreateProposalResult(ReviewedInViewMessage, keptProposal with { Token = null });
            toolResult.StructuredContent = JsonSerializer.SerializeToElement(keptProposal, McpJsonUtilities.DefaultOptions);

            return toolResult;
        }

        /// <summary>
        /// Imports the requirements that the user has chosen in the view of the extraction, in one batch, with the checks of
        /// apply_changes. Only the view calls this tool: the host hides it from the AI assistant, and the secret token proves that
        /// the caller has received the proposal.
        /// </summary>
        /// <param name="proposalId">The identifier of the proposal.</param>
        /// <param name="token">The secret token of the proposal.</param>
        /// <param name="choices">The <see cref="ImportChoice"/>s of the user.</param>
        /// <returns>
        /// The <see cref="CallToolResult"/>: the message for the AI assistant, and the <see cref="RequirementImport"/> as
        /// structured content.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when no proposal has this identifier and this token, when a batch waits for the decision of the user, or when
        /// a choice is invalid; nothing is then imported.
        /// </exception>
        [McpServerTool(Name = "import_requirements", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [McpAppUi(ResourceUri = RequirementExtractionResource.Uri, Visibility = [McpUiToolVisibility.App])]
        [Description("Imports, in one batch, the requirements that the user has chosen in the view of the extraction. Called by the view only.")]
        [return: Description("Whether the requirements have been imported, the message for the AI assistant and the result of apply_changes.")]
        public CallToolResult ImportRequirements(
            [Description("The identifier of the proposal (id of the proposal).")] Guid proposalId,
            [Description("The secret token of the proposal (token of the proposal).")] string token,
            [Description("The requirements to import, each with its option: AsWritten, WithRewrite or AsGoal.")] IReadOnlyList<ImportChoice> choices)
        {
            var proposal = this.proposalStore.TryGet(proposalId, token)
                ?? throw new McpException("This proposal can no longer be imported: it has been imported already, or the server has restarted. "
                    + "Ask the assistant to propose the requirements again.");

            if (this.pendingBatchStore.HasPendingBatch)
            {
                throw new McpException("A batch of changes waits for your decision in the conversation: apply or reject it first, then import.");
            }

            var result = this.modelProvider.ApplyChanges(RequirementBatchBuilder.Build(proposal, choices));

            if (result.Applied)
            {
                this.proposalStore.Remove(proposal.Id);
            }

            var import = new RequirementImport(DescribeImport(proposal, choices, result), result);

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = import.Message }],
                StructuredContent = JsonSerializer.SerializeToElement(import, McpJsonUtilities.DefaultOptions)
            };
        }

        /// <summary>
        /// Creates the <see cref="CallToolResult"/> of a proposal for the AI assistant: the message, then the proposal in JSON.
        /// </summary>
        /// <param name="message">The message that tells the AI assistant what to do next.</param>
        /// <param name="proposal">The <see cref="RequirementProposal"/>, without token.</param>
        /// <returns>The <see cref="CallToolResult"/>.</returns>
        private static CallToolResult CreateProposalResult(string message, RequirementProposal proposal)
        {
            return new CallToolResult
            {
                Content =
                [
                    new TextContentBlock { Text = message },
                    new TextContentBlock { Text = JsonSerializer.Serialize(proposal, McpJsonUtilities.DefaultOptions) }
                ]
            };
        }

        /// <summary>
        /// Writes the message for the AI assistant after an import: what has been imported and how, what the user has left out,
        /// and the result of apply_changes.
        /// </summary>
        /// <param name="proposal">The imported <see cref="RequirementProposal"/>.</param>
        /// <param name="choices">The <see cref="ImportChoice"/>s of the user.</param>
        /// <param name="result">The <see cref="ApplyChangesResult"/> of the batch.</param>
        /// <returns>The message.</returns>
        private static string DescribeImport(RequirementProposal proposal, IReadOnlyList<ImportChoice> choices, ApplyChangesResult result)
        {
            var section = $"section {proposal.SectionNumber} '{proposal.SectionTitle}' of '{proposal.FileName}'";
            var resultText = JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions);

            if (!result.Applied)
            {
                return $"The user has tried to import requirements of {section}, but the batch has been refused and nothing has been applied. Result of apply_changes: {resultText} "
                    + "Propose the requirements of the section again with propose_requirements, corrected.";
            }

            var chosenIds = choices.Select(choice => choice.ReqId).ToHashSet(StringComparer.Ordinal);
            var imported = string.Join(", ", choices.Select(choice => $"{choice.ReqId} ({DescribeOption(choice.Option)})"));
            var leftOut = proposal.Requirements.Select(row => row.Requirement.ReqId).Where(reqId => !chosenIds.Contains(reqId)).Distinct(StringComparer.Ordinal).ToList();
            var leftOutText = leftOut.Count == 0 ? string.Empty : $" Left out by the user: {string.Join(", ", leftOut)}.";

            return $"The user has imported {choices.Count} requirement{(choices.Count == 1 ? string.Empty : "s")} of {section} in one batch: {imported}.{leftOutText} "
                + $"Result of apply_changes: {resultText} "
                + "Go on with the next section of the plan.";
        }

        /// <summary>
        /// Describes how a requirement has been imported.
        /// </summary>
        /// <param name="option">The <see cref="ImportOption"/>.</param>
        /// <returns>The description, for example <c>as a goal, without constraint</c>.</returns>
        private static string DescribeOption(ImportOption option)
        {
            return option switch
            {
                ImportOption.WithRewrite => "rewritten",
                ImportOption.AsGoal => "as a goal, without constraint",
                _ => "as written"
            };
        }
    }
}
