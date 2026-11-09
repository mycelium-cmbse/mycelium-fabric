// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementProposalBuilderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tests.Extraction
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using ModelContextProtocol;

    using Moq;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Extraction;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Kernel.Packages;
    using SysML2.NET.Core.POCO.Root.Elements;
    using SysML2.NET.Core.POCO.Systems.Parts;

    /// <summary>
    /// Suite of tests for the <see cref="RequirementProposalBuilder"/> class.
    /// </summary>
    [TestFixture]
    public class RequirementProposalBuilderTestFixture
    {
        private const string IdPattern = @"REQ-[A-Z]+-\d{3}";

        private const string UnknownUnitProblem = "Change 2 (create RequirementUsage): The unit 'kbps' is not known. Use a symbol such as kg, W, km/h, m/s^2 or arcsec, "
            + "a name such as kilogram, or a unit declared in a library of the model.";

        private static readonly Guid OwnerId = Guid.Parse("896440c0-d061-7856-fb8f-fe278e26de93");

        private static readonly Guid PartId = Guid.Parse("558a0ae4-8585-66a3-9bc6-52322650941c");

        private static readonly string[] MissedIds = ["REQ-PAY-002"];

        private static readonly ExtractionFindingKind[] RewriteFindings = [ExtractionFindingKind.InvalidRewrite, ExtractionFindingKind.ToClarify];

        private static readonly ExtractionFindingKind[] ModelledFindings = [ExtractionFindingKind.NotInSection, ExtractionFindingKind.AlreadyModelled];

        private Mock<IModelProvider> modelProvider;

        private TextDocument document;

        private List<ProposedRequirement> requirements;

        [SetUp]
        public void SetUp()
        {
            using (var content = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "EOSat1-requirements.md")))
            {
                this.document = new TextDocument("EOSat1-requirements.md", new MarkdownDocumentParser().Parse(content));
            }

            this.modelProvider = new Mock<IModelProvider>();
            this.modelProvider.Setup(provider => provider.GetRequirementIds()).Returns(new HashSet<string> { "REQ-SYS-001" });
            this.modelProvider.Setup(provider => provider.GetRequiredElementById(OwnerId)).Returns(Mock.Of<IPackage>(package => package.qualifiedName == "EOSat1::Requirements"));
            this.modelProvider.Setup(provider => provider.GetRequiredElementById(PartId)).Returns(new Mock<IPartUsage>().Object);
            this.modelProvider.Setup(provider => provider.PreviewChanges(It.IsAny<IReadOnlyList<ModelChange>>())).Returns(new ModelPreview([]));

            // apply_changes refuses the unit kbps, the operator =< and a second package named Taken.
            this.modelProvider.Setup(provider => provider.PreviewChanges(It.Is<IReadOnlyList<ModelChange>>(changes => IsLastConstraint(changes, constraint => constraint.Unit == "kbps"))))
                .Returns(new ModelPreview([UnknownUnitProblem]));
            this.modelProvider.Setup(provider => provider.PreviewChanges(It.Is<IReadOnlyList<ModelChange>>(changes => IsLastConstraint(changes, constraint => constraint.Operator == "=<"))))
                .Returns(new ModelPreview(["Change 2 (create RequirementUsage): The operator '=<' is not supported."]));
            this.modelProvider.Setup(provider => provider.PreviewChanges(It.Is<IReadOnlyList<ModelChange>>(changes => changes[0].Payload.Name == "Taken")))
                .Returns(new ModelPreview(["Change 1 (create Package): The Package 'Requirements' already has a member named 'Taken'."]));

            this.requirements =
            [
                new ProposedRequirement
                {
                    ReqId = "REQ-PAY-001", Name = "groundSampling", Text = "The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km.",
                    Kind = RequirementKind.Performance, Constraint = new ConstraintPayload { Attribute = "groundSamplingDistance", Operator = "<=", Limit = 5, Unit = "m" }
                },
                new ProposedRequirement
                {
                    ReqId = "REQ-PAY-003", Name = "imageStorage", Text = "The mass memory  shall store at least 512 Gbit of images.",
                    Constraint = new ConstraintPayload { Attribute = "storageCapacity", Operator = ">=", Limit = 512, Unit = "kbps" }
                },
                new ProposedRequirement
                {
                    ReqId = "REQ-PAY-004", Name = "downlinkDelay", Text = "The images shall be downlinked within a responsive time (target TBD).",
                    Clarification = " TBD target ", RewrittenText = "The images shall be downlinked within 2 h.",
                    RewrittenConstraint = new ConstraintPayload { Attribute = "downlinkDelay", Operator = "=<", Limit = 2, Unit = "h" }
                },
                new ProposedRequirement { ReqId = "REQ-SYS-001", Name = "launchMass", Text = "The launch mass shall not exceed 150 kg." },
                new ProposedRequirement { ReqId = "REQ-PAY-001", Name = "groundSamplingAgain", Text = "The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km." }
            ];
        }

        [Test]
        public void VerifyBuild()
        {
            var proposal = RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, this.requirements, OwnerId, "PayloadRequirements", IdPattern);
            var rows = proposal.Requirements;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(proposal.FileName, Is.EqualTo("EOSat1-requirements.md"));
                Assert.That(proposal.SectionNumber, Is.EqualTo(5));
                Assert.That(proposal.SectionTitle, Is.EqualTo("3. Payload requirements"));
                Assert.That(proposal.OwnerId, Is.EqualTo(OwnerId));
                Assert.That(proposal.OwnerName, Is.EqualTo("EOSat1::Requirements"));
                Assert.That(proposal.NewPackage, Is.EqualTo("PayloadRequirements"));
                Assert.That(proposal.MissedIds, Is.EqualTo(MissedIds));
                Assert.That(proposal.Id, Is.EqualTo(Guid.Empty));
                Assert.That(proposal.Token, Is.Null);
                Assert.That(rows, Has.Count.EqualTo(5));

                Assert.That(rows[0].IsReady, Is.True);
                Assert.That(rows[0].IsBlocked, Is.False);
                Assert.That(rows[0].SourceTitle, Is.EqualTo("3.1 Camera"));
                Assert.That(rows[0].Excerpt, Is.EqualTo("| REQ-PAY-001 | The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km. | H |"));

                Assert.That(rows[1].SourceTitle, Is.EqualTo("3.2 Data handling"));
                Assert.That(rows[1].Findings.Select(finding => finding.Kind), Is.EqualTo(new[] { ExtractionFindingKind.InvalidChange }));
                Assert.That(rows[1].Findings[0].Message, Does.StartWith("The unit 'kbps' is not known."));

                Assert.That(rows[2].Findings.Select(finding => finding.Kind), Is.EqualTo(RewriteFindings));
                Assert.That(rows[2].Findings[0].Message, Is.EqualTo("Rewrite: The operator '=<' is not supported."));
                Assert.That(rows[2].Findings[1].Message, Is.EqualTo("TBD target"));
                Assert.That(rows[2].IsReady, Is.False);
                Assert.That(rows[2].IsBlocked, Is.False);

                Assert.That(rows[3].Findings.Select(finding => finding.Kind), Is.EqualTo(ModelledFindings));
                Assert.That(rows[3].Findings[0].Message, Does.Contain("section 5 '3. Payload requirements'"));
                Assert.That(rows[3].SourceTitle, Is.Null);
                Assert.That(rows[3].Excerpt, Is.Null);
                Assert.That(rows[3].IsBlocked, Is.True);

                Assert.That(rows[4].Findings.Select(finding => finding.Kind), Is.EqualTo(new[] { ExtractionFindingKind.Repeated }));
                Assert.That(rows[4].IsBlocked, Is.True);

                // The new package once, then each requirement that can be imported, its rewrite too.
                this.modelProvider.Verify(provider => provider.PreviewChanges(It.IsAny<IReadOnlyList<ModelChange>>()), Times.Exactly(5));
                this.modelProvider.Verify(provider => provider.PreviewChanges(It.Is<IReadOnlyList<ModelChange>>(changes => changes.Count == 2 && changes[0].Payload.Type == "Package")),
                    Times.Exactly(4));
            }

            var ground = this.requirements[0];
            var storage = this.requirements[1];
            var withMerges = RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5,
            [
                ground,
                storage with { Constraint = null, MergeInto = "REQ-PAY-001" },
                this.requirements[2] with { MergeInto = "REQ-PAY-001" },
                ground with { ReqId = "REQ-PAY-010", Name = "self", MergeInto = "REQ-PAY-010" },
                ground with { ReqId = "REQ-PAY-011", Name = "outside", MergeInto = "REQ-PAY-099" },
                ground with { ReqId = "REQ-PAY-012", Name = "modelled", MergeInto = "REQ-SYS-001" },
                this.requirements[3] with { MergeInto = "REQ-PAY-001" },
                ground with { ReqId = "REQ-PAY-013", Name = "blank", MergeInto = " " }
            ], OwnerId, "PayloadRequirements", null).Requirements;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withMerges[1].Requirement.MergeInto, Is.EqualTo("REQ-PAY-001"));
                Assert.That(withMerges[1].Findings.Select(finding => finding.Kind), Is.EqualTo(new[] { ExtractionFindingKind.ToClarify }));
                Assert.That(withMerges[1].Findings[0].Message, Is.EqualTo("The assistant suggests merging it into REQ-PAY-001."));
                Assert.That(withMerges[2].Requirement.MergeInto, Is.EqualTo("REQ-PAY-001"));
                Assert.That(withMerges[2].Findings.Select(finding => finding.Message), Has.None.Contains("suggests merging"));
                Assert.That(withMerges[3].Requirement.MergeInto, Is.Null);
                Assert.That(withMerges[3].Findings.Select(finding => finding.Kind), Is.EqualTo(new[] { ExtractionFindingKind.InvalidMerge }));
                Assert.That(withMerges[3].Findings[0].Message, Is.EqualTo("It cannot be merged into itself."));
                Assert.That(withMerges[3].IsBlocked, Is.False);
                Assert.That(withMerges[4].Requirement.MergeInto, Is.Null);
                Assert.That(withMerges[4].Findings[0].Message, Is.EqualTo("It cannot be merged into REQ-PAY-099: this requirement is not in the list."));
                Assert.That(withMerges[5].Requirement.MergeInto, Is.Null);
                Assert.That(withMerges[5].Findings[0].Message, Does.StartWith("It cannot be merged into REQ-SYS-001: the model already has this requirement"));
                Assert.That(withMerges[6].Requirement.MergeInto, Is.Null);
                Assert.That(withMerges[6].Findings.Select(finding => finding.Kind), Has.None.EqualTo(ExtractionFindingKind.InvalidMerge));
                Assert.That(withMerges[7].IsReady, Is.True);
            }

            this.modelProvider.Invocations.Clear();

            var inExistingPackage = RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 6, [this.requirements[0]], OwnerId, " ", null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(inExistingPackage.NewPackage, Is.Null);
                Assert.That(inExistingPackage.MissedIds, Is.Empty);
                Assert.That(inExistingPackage.Requirements[0].IsReady, Is.True);
                this.modelProvider.Verify(provider => provider.PreviewChanges(It.Is<IReadOnlyList<ModelChange>>(changes => changes.Count == 1 && changes[0].Payload.Type == "RequirementUsage" && changes[0].Payload.Owner == OwnerId.ToString())),
                    Times.Once);

                Assert.That(() => RequirementProposalBuilder.Build(null, this.document, 5, this.requirements, OwnerId, null, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, null, 5, this.requirements, OwnerId, null, null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, null, OwnerId, null, null),
                    Throws.TypeOf<McpException>().With.Message.Contains("list of requirements is empty"));
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, [], OwnerId, null, null),
                    Throws.TypeOf<McpException>().With.Message.Contains("list of requirements is empty"));
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, [new ProposedRequirement { ReqId = "REQ-A-001" }, null], OwnerId, null, null),
                    Throws.TypeOf<McpException>().With.Message.EqualTo("Requirement 1 has no name, text. Requirement 2 has no reqId, name, text."));
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, this.requirements, null, null, null),
                    Throws.TypeOf<McpException>().With.Message.Contains("The package is missing"));
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, this.requirements, PartId, null, null),
                    Throws.TypeOf<McpException>().With.Message.Contains("is not a package"));
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, this.requirements, OwnerId, "Taken", null),
                    Throws.TypeOf<McpException>().With.Message.EqualTo("The new package cannot be created: The Package 'Requirements' already has a member named 'Taken'."));
                Assert.That(() => RequirementProposalBuilder.Build(this.modelProvider.Object, this.document, 5, this.requirements, OwnerId, null, "REQ-["),
                    Throws.TypeOf<McpException>().With.Message.Contains("not a valid regular expression"));
            }
        }

        [Test]
        public void VerifyBuildFindsWrappedAndLongPassages()
        {
            var longText = string.Join(' ', Enumerable.Repeat("Context", 60));
            var section = new DocumentSection(1, 1, "Mass", ["The launch mass shall", "  not exceed 150 kg.", "", longText + " The satellite shall be as light as possible. " + longText]);
            var wrappedDocument = new TextDocument("Wrapped.md", [section]);

            IReadOnlyList<ProposedRequirement> wrappedRequirements =
            [
                new ProposedRequirement { ReqId = "REQ-SYS-010", Name = "launchMass", Text = "The launch mass shall not exceed 150 kg." },
                new ProposedRequirement { ReqId = "REQ-SYS-011", Name = "lightness", Text = "The satellite shall be as light as possible." }
            ];

            var proposal = RequirementProposalBuilder.Build(this.modelProvider.Object, wrappedDocument, 1, wrappedRequirements, null, "MassRequirements", null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(proposal.OwnerId, Is.Null);
                Assert.That(proposal.OwnerName, Is.Null);
                Assert.That(proposal.Requirements[0].Excerpt, Is.EqualTo("The launch mass shall not exceed 150 kg."));
                Assert.That(proposal.Requirements[1].Excerpt, Does.StartWith("…").And.EndWith("…").And.Contain("The satellite shall be as light as possible."));
                Assert.That(proposal.Requirements[1].Excerpt.Length, Is.LessThan(longText.Length));
                Assert.That(proposal.Requirements.Select(row => row.IsReady), Has.All.True);
            }
        }

        // Tells whether the last change of a batch has a constraint that matches a condition.
        private static bool IsLastConstraint(IReadOnlyList<ModelChange> changes, Func<ConstraintPayload, bool> condition)
        {
            return changes[^1].Payload.Constraint is { } constraint && condition(constraint);
        }
    }
}
