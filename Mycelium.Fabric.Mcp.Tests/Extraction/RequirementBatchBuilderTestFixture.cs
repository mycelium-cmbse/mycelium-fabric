// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementBatchBuilderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extraction
{
    using System;
    using System.Linq;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Extraction;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementBatchBuilder"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementBatchBuilderTestFixture
    {
        private static readonly Guid OwnerId = Guid.Parse("896440c0-d061-7856-fb8f-fe278e26de93");

        private static readonly ProposedRequirement CameraMass = new()
        {
            ReqId = "REQ-PAY-002", Name = "cameraMass", Text = "The camera mass shall not exceed 40 kg, as allocated by REQ-SYS-001.",
            Constraint = new ConstraintPayload { Attribute = "mass", Operator = "<=", Limit = 40, Unit = "kg" }
        };

        private static readonly ProposedRequirement DownlinkDelay = new()
        {
            ReqId = "REQ-PAY-004", Name = "downlinkDelay", Text = "The images shall be downlinked within a responsive time (target TBD).",
            Clarification = "TBD target", RewrittenText = "The images shall be downlinked within 2 h.",
            RewrittenConstraint = new ConstraintPayload { Attribute = "downlinkDelay", Operator = "<=", Limit = 2, Unit = "h" }
        };

        private static readonly ProposedRequirement LaunchMass = new() { ReqId = "REQ-SYS-001", Name = "launchMass", Text = "The satellite launch mass shall not exceed 150 kg." };

        private static readonly ProposedRequirement DataRate = new()
        {
            ReqId = "REQ-PAY-009", Name = "dataRate", Text = "The link shall be fast.", Clarification = "vague term: fast", RewrittenText = "The link shall reach 150 Mbps.",
            RewrittenConstraint = new ConstraintPayload { Attribute = "dataRate", Operator = "=<", Limit = 150 }
        };

        private static readonly string[] ImportedIds = ["REQ-PAY-002", "REQ-PAY-004"];

        private RequirementProposal proposal;

        [SetUp]
        public void SetUp()
        {
            this.proposal = new RequirementProposal("EOSat1-requirements.md", 5, "3. Payload requirements", OwnerId, "PayloadRequirements",
            [
                new ExtractedRequirement(CameraMass, "3.1 Camera", CameraMass.Text, []),
                new ExtractedRequirement(DownlinkDelay, "3.2 Data handling", DownlinkDelay.Text, [new ExtractionFinding(ExtractionFindingKind.ToClarify, "TBD target")]),
                new ExtractedRequirement(LaunchMass, "3.1 Camera", null,
                [
                    new ExtractionFinding(ExtractionFindingKind.NotInSection, "The statement is not written as such in section 5."),
                    new ExtractionFinding(ExtractionFindingKind.AlreadyModelled, "The model already has a requirement REQ-SYS-001: it is not imported again.")
                ]),
                new ExtractedRequirement(DataRate, null, null, [new ExtractionFinding(ExtractionFindingKind.InvalidRewrite, "Rewrite: The operator '=<' is not supported.")]),
                new ExtractedRequirement(CameraMass with { Name = "cameraMassAgain" }, "3.1 Camera", CameraMass.Text,
                    [new ExtractionFinding(ExtractionFindingKind.Repeated, "REQ-PAY-002 is given twice: only its first row can be imported.")])
            ], []);
        }

        [Test]
        public void VerifyCreatePackageChanges()
        {
            var packageChanges = RequirementBatchBuilder.CreatePackageChanges(OwnerId, "PayloadRequirements");
            var topLevelChanges = RequirementBatchBuilder.CreatePackageChanges(null, "PayloadRequirements");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(RequirementBatchBuilder.CreatePackageChanges(OwnerId, null), Is.Empty);
                Assert.That(RequirementBatchBuilder.CreatePackageChanges(OwnerId, " "), Is.Empty);
                Assert.That(packageChanges, Has.Count.EqualTo(1));
                Assert.That(packageChanges[0].Identity, Is.EqualTo("PayloadRequirements"));
                Assert.That(packageChanges[0].Payload.Type, Is.EqualTo("Package"));
                Assert.That(packageChanges[0].Payload.Name, Is.EqualTo("PayloadRequirements"));
                Assert.That(packageChanges[0].Payload.Owner, Is.EqualTo(OwnerId.ToString()));
                Assert.That(topLevelChanges[0].Payload.Owner, Is.Null);
            }
        }

        [Test]
        public void VerifyCreateRequirementChange()
        {
            var asWritten = RequirementBatchBuilder.CreateRequirementChange(CameraMass, ImportOption.AsWritten, OwnerId, null);
            var inNewPackage = RequirementBatchBuilder.CreateRequirementChange(CameraMass, ImportOption.AsWritten, OwnerId, "PayloadRequirements");
            var rewritten = RequirementBatchBuilder.CreateRequirementChange(DownlinkDelay, ImportOption.WithRewrite, OwnerId, null);
            var asGoal = RequirementBatchBuilder.CreateRequirementChange(CameraMass, ImportOption.AsGoal, OwnerId, null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(asWritten.Identity, Is.Null);
                Assert.That(asWritten.Payload.Type, Is.EqualTo("RequirementUsage"));
                Assert.That(asWritten.Payload.Name, Is.EqualTo("cameraMass"));
                Assert.That(asWritten.Payload.ReqId, Is.EqualTo("REQ-PAY-002"));
                Assert.That(asWritten.Payload.Text, Is.EqualTo(CameraMass.Text));
                Assert.That(asWritten.Payload.Constraint, Is.SameAs(CameraMass.Constraint));
                Assert.That(asWritten.Payload.Owner, Is.EqualTo(OwnerId.ToString()));
                Assert.That(inNewPackage.Payload.Owner, Is.EqualTo("PayloadRequirements"));
                Assert.That(rewritten.Payload.Text, Is.EqualTo("The images shall be downlinked within 2 h."));
                Assert.That(rewritten.Payload.Constraint, Is.SameAs(DownlinkDelay.RewrittenConstraint));
                Assert.That(asGoal.Payload.Text, Is.EqualTo(CameraMass.Text));
                Assert.That(asGoal.Payload.Constraint, Is.Null);
                Assert.That(() => RequirementBatchBuilder.CreateRequirementChange(null, ImportOption.AsWritten, OwnerId, null), Throws.TypeOf<ArgumentNullException>());
            }
        }

        [Test]
        public void VerifyBuild()
        {
            var changes = RequirementBatchBuilder.Build(this.proposal,
            [
                new ImportChoice { ReqId = "REQ-PAY-004", Option = ImportOption.WithRewrite },
                new ImportChoice { ReqId = "REQ-PAY-002", Option = ImportOption.AsWritten }
            ]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(changes, Has.Count.EqualTo(3));
                Assert.That(changes[0].Payload.Type, Is.EqualTo("Package"));
                Assert.That(changes.Skip(1).Select(change => change.Payload.ReqId), Is.EqualTo(ImportedIds));
                Assert.That(changes[1].Payload.Name, Is.EqualTo("cameraMass"));
                Assert.That(changes[2].Payload.Text, Is.EqualTo(DownlinkDelay.RewrittenText));
                Assert.That(changes.Skip(1).Select(change => change.Payload.Owner), Has.All.EqualTo("PayloadRequirements"));

                Assert.That(() => RequirementBatchBuilder.Build(null, []), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => RequirementBatchBuilder.Build(this.proposal, null), Throws.TypeOf<McpException>().With.Message.Contains("No requirement is chosen"));
                Assert.That(() => RequirementBatchBuilder.Build(this.proposal, []), Throws.TypeOf<McpException>().With.Message.Contains("No requirement is chosen"));

                Assert.That(() => RequirementBatchBuilder.Build(this.proposal,
                    [
                        new ImportChoice { ReqId = "REQ-XXX-001", Option = ImportOption.AsWritten },
                        new ImportChoice { ReqId = "REQ-SYS-001", Option = ImportOption.AsWritten },
                        new ImportChoice { ReqId = "REQ-PAY-002", Option = (ImportOption)7 },
                        new ImportChoice { ReqId = "REQ-PAY-009", Option = ImportOption.WithRewrite },
                        new ImportChoice { ReqId = "REQ-PAY-004", Option = ImportOption.AsGoal },
                        new ImportChoice { ReqId = "REQ-PAY-004", Option = ImportOption.AsWritten },
                        null
                    ]),
                    Throws.TypeOf<McpException>().With.Message.StartsWith("Nothing has been imported.")
                        .And.Message.Contains("'REQ-XXX-001' is not a requirement of the proposal.")
                        .And.Message.Contains("REQ-SYS-001 cannot be imported: The model already has a requirement REQ-SYS-001")
                        .And.Message.Contains("The option of REQ-PAY-002 must be AsWritten, WithRewrite or AsGoal.")
                        .And.Message.Contains("REQ-PAY-009 has no valid rewrite")
                        .And.Message.Contains("REQ-PAY-004 is chosen more than once.")
                        .And.Message.Contains("'' is not a requirement of the proposal."));

                Assert.That(() => RequirementBatchBuilder.Build(this.proposal, [new ImportChoice { ReqId = "REQ-PAY-002", Option = ImportOption.WithRewrite }]),
                    Throws.TypeOf<McpException>().With.Message.Contains("REQ-PAY-002 has no valid rewrite"));
            }
        }
    }
}
