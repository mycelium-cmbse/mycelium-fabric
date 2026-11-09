// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementExtractionToolsTestFixture.cs" company="Starion Group S.A.">
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
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Extraction;
    using Mycelium.Fabric.Mcp.Services;
    using Mycelium.Fabric.Mcp.Tools;

    using SysML2.NET.Core.POCO.Kernel.Packages;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementExtractionTools"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementExtractionToolsTestFixture
    {
        private const string FileName = "EOSat1-requirements.md";

        private const string Token = "0123456789abcdef0123456789abcdef";

        private static readonly Guid OwnerId = Guid.Parse("896440c0-d061-7856-fb8f-fe278e26de93");

        private static readonly Guid ProposalId = Guid.Parse("f40d2207-3be4-439a-92d4-1b7525698544");

        private static readonly ProposedRequirement GroundSampling = new()
        {
            ReqId = "REQ-PAY-001", Name = "groundSampling", Text = "The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km.",
            Constraint = new ConstraintPayload { Attribute = "groundSamplingDistance", Operator = "<=", Limit = 5, Unit = "m" }
        };

        private static readonly ProposedRequirement DownlinkDelay = new()
        {
            ReqId = "REQ-PAY-004", Name = "downlinkDelay", Text = "The images shall be downlinked within a responsive time (target TBD).", Clarification = "TBD target"
        };

        private Mock<IModelProvider> modelProvider;

        private Mock<IDocumentReader> documentReader;

        private Mock<IRequirementProposalStore> proposalStore;

        private Mock<IPendingBatchStore> pendingBatchStore;

        private Mock<IAppHostDetector> appHostDetector;

        private Mock<McpServer> server;

        private ClientCapabilities clientCapabilities;

        private RequirementProposal keptProposal;

        [SetUp]
        public void SetUp()
        {
            TextDocument document;

            using (var content = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", FileName)))
            {
                document = new TextDocument(FileName, new MarkdownDocumentParser().Parse(content));
            }

            this.modelProvider = new Mock<IModelProvider>();
            this.modelProvider.Setup(provider => provider.Elements).Returns([]);
            this.modelProvider.Setup(provider => provider.GetRequiredElementById(OwnerId)).Returns(Mock.Of<IPackage>(package => package.qualifiedName == "EOSat1::Requirements"));
            this.modelProvider.Setup(provider => provider.PreviewChanges(It.IsAny<IReadOnlyList<ModelChange>>())).Returns(new ModelPreview([]));

            this.documentReader = new Mock<IDocumentReader>();
            this.documentReader.Setup(reader => reader.ReadAsync(FileName, It.IsAny<CancellationToken>())).ReturnsAsync(document);
            this.documentReader.Setup(reader => reader.ReadAsync("Missing.md", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new McpException("The document folder has no file named 'Missing.md'."));

            this.proposalStore = new Mock<IRequirementProposalStore>();
            this.proposalStore.Setup(store => store.Add(It.IsAny<RequirementProposal>())).Returns<RequirementProposal>(proposal => proposal with { Id = ProposalId, Token = Token });

            this.keptProposal = new RequirementProposal(FileName, 5, "3. Payload requirements", OwnerId, "PayloadRequirements",
            [
                new ExtractedRequirement(GroundSampling, "3.1 Camera", GroundSampling.Text, []),
                new ExtractedRequirement(DownlinkDelay, "3.2 Data handling", DownlinkDelay.Text, [new ExtractionFinding(ExtractionFindingKind.ToClarify, "TBD target")])
            ], []) { Id = ProposalId, Token = Token };

            this.proposalStore.Setup(store => store.TryGet(ProposalId, Token)).Returns(this.keptProposal);

            this.pendingBatchStore = new Mock<IPendingBatchStore>();
            this.appHostDetector = new Mock<IAppHostDetector>();

            this.clientCapabilities = new ClientCapabilities();
            this.server = new Mock<McpServer>();
            this.server.Setup(mcpServer => mcpServer.ClientCapabilities).Returns(this.clientCapabilities);
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new RequirementExtractionTools(null, this.documentReader.Object, this.proposalStore.Object, this.pendingBatchStore.Object,
                    this.appHostDetector.Object, this.server.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new RequirementExtractionTools(this.modelProvider.Object, null, this.proposalStore.Object, this.pendingBatchStore.Object,
                    this.appHostDetector.Object, this.server.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new RequirementExtractionTools(this.modelProvider.Object, this.documentReader.Object, null, this.pendingBatchStore.Object,
                    this.appHostDetector.Object, this.server.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new RequirementExtractionTools(this.modelProvider.Object, this.documentReader.Object, this.proposalStore.Object, null,
                    this.appHostDetector.Object, this.server.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new RequirementExtractionTools(this.modelProvider.Object, this.documentReader.Object, this.proposalStore.Object, this.pendingBatchStore.Object,
                    null, this.server.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => new RequirementExtractionTools(this.modelProvider.Object, this.documentReader.Object, this.proposalStore.Object, this.pendingBatchStore.Object,
                    this.appHostDetector.Object, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(this.CreateTools, Throws.Nothing);
            }
        }

        [Test]
        public async Task VerifyProposeRequirementsAsync()
        {
            var tools = this.CreateTools();

            var textResult = await tools.ProposeRequirementsAsync(FileName, 5, [GroundSampling, DownlinkDelay], OwnerId, "PayloadRequirements");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(textResult.Content, Has.Count.EqualTo(2));
                Assert.That(ReadText(textResult, 0), Does.StartWith("Nothing has been modelled and the host shows no view."));
                Assert.That(ReadText(textResult, 1), Does.Contain("\"reqId\":\"REQ-PAY-001\"").And.Contain("\"isReady\":true").And.Not.Contain("token"));
                Assert.That(textResult.StructuredContent, Is.Null);
                this.proposalStore.Verify(store => store.Add(It.IsAny<RequirementProposal>()), Times.Never);
            }

            this.appHostDetector.Setup(detector => detector.ShowsApps(this.clientCapabilities)).Returns(true);

            var viewResult = await tools.ProposeRequirementsAsync(FileName, 5, [GroundSampling, DownlinkDelay], OwnerId, "PayloadRequirements");
            var viewProposal = viewResult.StructuredContent.Value.Deserialize<RequirementProposal>(McpJsonUtilities.DefaultOptions);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ReadText(viewResult, 0), Does.Contain("Do not send them with apply_changes: wait for the user's message"));
                Assert.That(ReadText(viewResult, 1), Does.Contain(ProposalId.ToString()).And.Not.Contain(Token));
                Assert.That(viewProposal.Id, Is.EqualTo(ProposalId));
                Assert.That(viewProposal.Token, Is.EqualTo(Token));
                Assert.That(viewProposal.Requirements, Has.Count.EqualTo(2));
                Assert.That(viewProposal.Requirements[1].Findings[0].Kind, Is.EqualTo(ExtractionFindingKind.ToClarify));
                this.proposalStore.Verify(store => store.Add(It.Is<RequirementProposal>(proposal => proposal.SectionNumber == 5 && proposal.Token == null)), Times.Once);

                Assert.That(() => tools.ProposeRequirementsAsync(FileName, 0, [GroundSampling], OwnerId), Throws.TypeOf<McpException>().With.Message.Contains("has no section 0"));
                Assert.That(() => tools.ProposeRequirementsAsync(FileName, 9, [GroundSampling], OwnerId), Throws.TypeOf<McpException>().With.Message.Contains("from 1 to 8"));
                Assert.That(() => tools.ProposeRequirementsAsync("Missing.md", 1, [GroundSampling], OwnerId), Throws.TypeOf<McpException>().With.Message.Contains("Missing.md"));
                Assert.That(() => tools.ProposeRequirementsAsync(FileName, 5, [], OwnerId), Throws.TypeOf<McpException>().With.Message.Contains("empty"));
            }
        }

        [Test]
        public void VerifyImportRequirements()
        {
            var created = new CreatedElement(Guid.NewGuid(), "EOSat1::Requirements::PayloadRequirements::groundSampling", "RequirementUsage");
            this.modelProvider.Setup(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>())).Returns(new ApplyChangesResult([created], []));

            var tools = this.CreateTools();
            var result = tools.ImportRequirements(ProposalId, Token, [new ImportChoice { ReqId = "REQ-PAY-001", Option = ImportOption.AsWritten }]);
            var import = result.StructuredContent.Value.Deserialize<RequirementImport>(McpJsonUtilities.DefaultOptions);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(import.Imported, Is.True);
                Assert.That(import.Result.CreatedElements, Is.EqualTo(new[] { created }));
                Assert.That(import.Message, Does.StartWith("The user has imported 1 requirement of section 5 '3. Payload requirements' of 'EOSat1-requirements.md' in one batch: "
                    + "REQ-PAY-001 (as written). Left out by the user: REQ-PAY-004."));
                Assert.That(import.Message, Does.EndWith("Go on with the next section of the plan."));
                Assert.That(ReadText(result, 0), Is.EqualTo(import.Message));
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.Is<IReadOnlyList<ModelChange>>(changes => changes.Count == 2 && changes[1].Payload.ReqId == "REQ-PAY-001")),
                    Times.Once);
                this.proposalStore.Verify(store => store.Remove(ProposalId), Times.Once);
            }

            this.modelProvider.Setup(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>())).Returns(new ApplyChangesResult([], ["Change 2 (create RequirementUsage): refused."]));

            var refused = tools.ImportRequirements(ProposalId, Token, [new ImportChoice { ReqId = "REQ-PAY-004", Option = ImportOption.AsGoal }])
                .StructuredContent.Value.Deserialize<RequirementImport>(McpJsonUtilities.DefaultOptions);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(refused.Imported, Is.False);
                Assert.That(refused.Message, Does.Contain("the batch has been refused and nothing has been applied").And.Contain("refused."));
                this.proposalStore.Verify(store => store.Remove(ProposalId), Times.Once);

                Assert.That(() => tools.ImportRequirements(ProposalId, "wrong", [new ImportChoice { ReqId = "REQ-PAY-001" }]),
                    Throws.TypeOf<McpException>().With.Message.Contains("can no longer be imported"));
                Assert.That(() => tools.ImportRequirements(ProposalId, Token, [new ImportChoice { ReqId = "REQ-XXX-001" }]),
                    Throws.TypeOf<McpException>().With.Message.Contains("'REQ-XXX-001' is not a requirement of the proposal."));
            }

            this.pendingBatchStore.Setup(store => store.HasPendingBatch).Returns(true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => tools.ImportRequirements(ProposalId, Token, [new ImportChoice { ReqId = "REQ-PAY-001" }]),
                    Throws.TypeOf<McpException>().With.Message.Contains("A batch of changes waits for your decision"));
                this.modelProvider.Verify(provider => provider.ApplyChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Exactly(2));
            }
        }

        private static string ReadText(CallToolResult result, int index)
        {
            return ((TextContentBlock)result.Content[index]).Text;
        }

        private RequirementExtractionTools CreateTools()
        {
            return new RequirementExtractionTools(this.modelProvider.Object, this.documentReader.Object, this.proposalStore.Object, this.pendingBatchStore.Object,
                this.appHostDetector.Object, this.server.Object);
        }
    }
}
