// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementProposalBuilder.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Changes;
    using Mycelium.Fabric.Mcp.Documents;
    using Mycelium.Fabric.Mcp.Services;

    using SysML2.NET.Core.POCO.Kernel.Packages;

    /// <summary>
    /// Checks the requirements that the AI assistant has found in a section of a document, before the user sees them: the
    /// server, not the language model, tells which statements are in the section, which identifiers are already modelled or
    /// repeated, which requirements apply_changes would refuse, and which identifiers of the section are missing.
    /// </summary>
    public static class RequirementProposalBuilder
    {
        /// <summary>
        /// The maximum number of characters of the passage shown around a statement, before and after it.
        /// </summary>
        private const int ExcerptContextLength = 200;

        /// <summary>
        /// The start of a problem of apply_changes, followed by the number and the kind of the change.
        /// </summary>
        private const string ChangeProblemStart = "Change ";

        /// <summary>
        /// What separates the number and the kind of a change from its problem, in a problem of apply_changes.
        /// </summary>
        private const string ChangeProblemSeparator = "): ";

        /// <summary>
        /// The characters that separate the words of a statement.
        /// </summary>
        private static readonly char[] WhiteSpaceCharacters = [' ', '\t', ' '];

        /// <summary>
        /// Checks the requirements found in a section and its subsections.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/>, which previews each requirement as apply_changes would.</param>
        /// <param name="document">The <see cref="TextDocument"/> that holds the section.</param>
        /// <param name="sectionNumber">The number of the section, as the outline gives it.</param>
        /// <param name="requirements">The <see cref="ProposedRequirement"/>s given by the AI assistant.</param>
        /// <param name="ownerId">The <c>Id</c> of the package that owns the requirements or the new package, or <c>null</c>.</param>
        /// <param name="newPackage">The name of the package that the import creates, or <c>null</c>.</param>
        /// <param name="idPattern">The regular expression of the identifiers of the document, or <c>null</c>.</param>
        /// <returns>The <see cref="RequirementProposal"/>, without identifier nor token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="modelProvider"/> or <paramref name="document"/> is <c>null</c>.</exception>
        /// <exception cref="McpException">
        /// Thrown when the list is empty, when a requirement has no identifier, name or statement, when the package is missing or
        /// cannot be created, or when <paramref name="idPattern"/> is invalid.
        /// </exception>
        public static RequirementProposal Build(IModelProvider modelProvider, TextDocument document, int sectionNumber, IReadOnlyList<ProposedRequirement> requirements,
            Guid? ownerId, string newPackage, string idPattern)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(document);

            CheckRequirements(requirements);

            var packageChanges = RequirementBatchBuilder.CreatePackageChanges(ownerId, newPackage);
            var ownerName = CheckPackage(modelProvider, ownerId, packageChanges);

            var sections = document.GetSectionAndSubsections(sectionNumber);
            var modelledIds = modelProvider.GetRequirementIds();

            var context = new ProposalContext(modelProvider, packageChanges, ownerId, newPackage, modelledIds, requirements, sections[0],
                [.. sections.SelectMany(GetPassages)]);

            var rows = requirements.Select((requirement, index) => CheckRequirement(context, requirement, index)).ToList();

            return new RequirementProposal(document.FileName, sectionNumber, sections[0].Title, ownerId, string.IsNullOrWhiteSpace(newPackage) ? null : newPackage, rows,
                FindMissedIds(document, sections, idPattern, requirements, modelledIds)) { OwnerName = ownerName };
        }

        /// <summary>
        /// Checks that the list holds requirements, each with an identifier, a name and a statement.
        /// </summary>
        /// <param name="requirements">The <see cref="ProposedRequirement"/>s.</param>
        /// <exception cref="McpException">Thrown when the list is empty, or when a requirement misses a property.</exception>
        private static void CheckRequirements(IReadOnlyList<ProposedRequirement> requirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                throw new McpException("The list of requirements is empty: give the requirements that the section defines and that the model does not have yet.");
            }

            var problems = requirements
                .Select((requirement, index) => (Number: index + 1, Missing: GetMissingProperties(requirement).ToList()))
                .Where(row => row.Missing.Count > 0)
                .Select(row => $"Requirement {row.Number} has no {string.Join(", ", row.Missing)}.")
                .ToList();

            if (problems.Count > 0)
            {
                throw new McpException(string.Join(" ", problems));
            }
        }

        /// <summary>
        /// Gets the properties that a requirement must have and does not have.
        /// </summary>
        /// <param name="requirement">The <see cref="ProposedRequirement"/>, or <c>null</c>.</param>
        /// <returns>The names of the missing properties.</returns>
        private static IEnumerable<string> GetMissingProperties(ProposedRequirement requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement?.ReqId))
            {
                yield return "reqId";
            }

            if (string.IsNullOrWhiteSpace(requirement?.Name))
            {
                yield return "name";
            }

            if (string.IsNullOrWhiteSpace(requirement?.Text))
            {
                yield return "text";
            }
        }

        /// <summary>
        /// Checks the package that owns the requirements: an existing package, or a new package that apply_changes accepts.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/>.</param>
        /// <param name="ownerId">The <c>Id</c> of the existing package, or <c>null</c>.</param>
        /// <param name="packageChanges">The change that creates the new package, or no change.</param>
        /// <returns>The qualified name of the existing package, or <c>null</c> when there is none.</returns>
        /// <exception cref="McpException">Thrown when there is no package, when the owner is not a package, or when the new package is refused.</exception>
        private static string CheckPackage(IModelProvider modelProvider, Guid? ownerId, IReadOnlyList<ModelChange> packageChanges)
        {
            if (ownerId == null && packageChanges.Count == 0)
            {
                throw new McpException("The package is missing: give ownerId (an existing package), newPackage (a package to create), or both.");
            }

            IPackage owner = null;

            if (ownerId is { } id)
            {
                owner = modelProvider.GetRequiredElementById(id) as IPackage ?? throw new McpException($"The owner '{id}' is not a package: give the identifier of a package.");
            }

            var problems = packageChanges.Count == 0 ? [] : modelProvider.PreviewChanges(packageChanges).Problems;

            if (problems.Count > 0)
            {
                throw new McpException($"The new package cannot be created: {string.Join(" ", problems.Select(DescribeProblem))}");
            }

            return owner?.qualifiedName;
        }

        /// <summary>
        /// Checks one requirement: where its statement is written, whether its identifier is free, and whether apply_changes
        /// accepts it as written and rewritten.
        /// </summary>
        /// <param name="context">The <see cref="ProposalContext"/>.</param>
        /// <param name="requirement">The <see cref="ProposedRequirement"/>.</param>
        /// <param name="index">The position of the requirement in the list, from 0.</param>
        /// <returns>The <see cref="ExtractedRequirement"/>, with its findings.</returns>
        private static ExtractedRequirement CheckRequirement(ProposalContext context, ProposedRequirement requirement, int index)
        {
            var findings = new List<ExtractionFinding>();
            var statement = Normalize(requirement.Text);
            var passage = context.Passages.FirstOrDefault(candidate => candidate.Text.Contains(statement, StringComparison.Ordinal));

            if (passage == null)
            {
                findings.Add(new ExtractionFinding(ExtractionFindingKind.NotInSection,
                    $"The statement is not written as such in section {context.Section.Number} '{context.Section.Title}' or its subsections: copy it word for word."));
            }

            if (context.ModelledIds.Contains(requirement.ReqId))
            {
                findings.Add(new ExtractionFinding(ExtractionFindingKind.AlreadyModelled, $"The model already has a requirement {requirement.ReqId}: it is not imported again."));
            }
            else if (context.Requirements.Take(index).Any(earlier => earlier.ReqId == requirement.ReqId))
            {
                findings.Add(new ExtractionFinding(ExtractionFindingKind.Repeated, $"{requirement.ReqId} is given twice: only its first row can be imported."));
            }
            else
            {
                findings.AddRange(Preview(context, requirement, ImportOption.AsWritten).Select(problem => new ExtractionFinding(ExtractionFindingKind.InvalidChange, problem)));

                if (!string.IsNullOrWhiteSpace(requirement.RewrittenText))
                {
                    findings.AddRange(Preview(context, requirement, ImportOption.WithRewrite)
                        .Select(problem => new ExtractionFinding(ExtractionFindingKind.InvalidRewrite, $"Rewrite: {problem}")));
                }
            }

            if (!string.IsNullOrWhiteSpace(requirement.Clarification))
            {
                findings.Add(new ExtractionFinding(ExtractionFindingKind.ToClarify, requirement.Clarification.Trim()));
            }

            return new ExtractedRequirement(CheckMerge(context, requirement, findings), passage?.SectionTitle, passage == null ? null : GetExcerpt(passage.Text, statement), findings);
        }

        /// <summary>
        /// Checks the requirement that the AI assistant proposes to merge into. A proposal of merge always asks the user: without
        /// a reason given by the assistant, the server writes one.
        /// </summary>
        /// <param name="context">The <see cref="ProposalContext"/>.</param>
        /// <param name="requirement">The <see cref="ProposedRequirement"/>.</param>
        /// <param name="findings">The findings of the requirement, to which the findings of the merge are added.</param>
        /// <returns>The <see cref="ProposedRequirement"/>, without its target when the target cannot be used.</returns>
        private static ProposedRequirement CheckMerge(ProposalContext context, ProposedRequirement requirement, List<ExtractionFinding> findings)
        {
            if (string.IsNullOrWhiteSpace(requirement.MergeInto))
            {
                return requirement;
            }

            if (findings.Exists(finding => finding.Kind is ExtractionFindingKind.AlreadyModelled or ExtractionFindingKind.Repeated))
            {
                return requirement with { MergeInto = null };
            }

            var problem = GetMergeProblem(context, requirement);

            if (problem != null)
            {
                findings.Add(new ExtractionFinding(ExtractionFindingKind.InvalidMerge, problem));

                return requirement with { MergeInto = null };
            }

            if (string.IsNullOrWhiteSpace(requirement.Clarification))
            {
                findings.Add(new ExtractionFinding(ExtractionFindingKind.ToClarify, $"The assistant suggests merging it into {requirement.MergeInto}."));
            }

            return requirement;
        }

        /// <summary>
        /// Tells why a requirement cannot be merged into the one that the AI assistant proposes.
        /// </summary>
        /// <param name="context">The <see cref="ProposalContext"/>.</param>
        /// <param name="requirement">The <see cref="ProposedRequirement"/>, which has a target.</param>
        /// <returns>The problem, or <c>null</c> when the target can receive the requirement.</returns>
        private static string GetMergeProblem(ProposalContext context, ProposedRequirement requirement)
        {
            var target = requirement.MergeInto;

            if (target == requirement.ReqId)
            {
                return "It cannot be merged into itself.";
            }

            if (context.Requirements.All(other => other.ReqId != target))
            {
                return $"It cannot be merged into {target}: this requirement is not in the list.";
            }

            return context.ModelledIds.Contains(target) ? $"It cannot be merged into {target}: the model already has this requirement, so it is not imported." : null;
        }

        /// <summary>
        /// Previews a requirement as apply_changes would apply it, after the new package if any, without changing the model.
        /// </summary>
        /// <param name="context">The <see cref="ProposalContext"/>.</param>
        /// <param name="requirement">The <see cref="ProposedRequirement"/>.</param>
        /// <param name="option">The <see cref="ImportOption"/> to preview.</param>
        /// <returns>The problems that apply_changes would report, without the number of the change.</returns>
        private static IEnumerable<string> Preview(ProposalContext context, ProposedRequirement requirement, ImportOption option)
        {
            var requirementChange = RequirementBatchBuilder.CreateRequirementChange(requirement, option, context.OwnerId, context.NewPackage);
            IReadOnlyList<ModelChange> changes = [.. context.PackageChanges, requirementChange];

            return context.ModelProvider.PreviewChanges(changes).Problems.Select(DescribeProblem);
        }

        /// <summary>
        /// Removes the number and the kind of the change from a problem of apply_changes, which mean nothing to the user:
        /// <c>Change 2 (create RequirementUsage): The unit 'Mbps' is unknown.</c> becomes <c>The unit 'Mbps' is unknown.</c>.
        /// </summary>
        /// <param name="problem">The problem.</param>
        /// <returns>The problem, without its change.</returns>
        private static string DescribeProblem(string problem)
        {
            var separatorIndex = problem.IndexOf(ChangeProblemSeparator, StringComparison.Ordinal);

            return problem.StartsWith(ChangeProblemStart, StringComparison.Ordinal) && separatorIndex > 0 ? problem[(separatorIndex + ChangeProblemSeparator.Length)..] : problem;
        }

        /// <summary>
        /// Splits a section into passages where a statement is searched: each table row on its own, and the other lines joined
        /// into paragraphs, which end at a blank line or at a table.
        /// </summary>
        /// <param name="section">The <see cref="DocumentSection"/>.</param>
        /// <returns>The <see cref="Passage"/>s, their white space reduced to single spaces.</returns>
        private static IEnumerable<Passage> GetPassages(DocumentSection section)
        {
            var paragraph = new List<string>();

            foreach (var line in section.Lines)
            {
                var isTableRow = line.TrimStart().StartsWith('|');

                if ((isTableRow || string.IsNullOrWhiteSpace(line)) && paragraph.Count > 0)
                {
                    yield return new Passage(section.Title, Normalize(string.Join(' ', paragraph)));
                    paragraph.Clear();
                }

                if (isTableRow)
                {
                    yield return new Passage(section.Title, Normalize(line));
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    paragraph.Add(line);
                }
            }

            if (paragraph.Count > 0)
            {
                yield return new Passage(section.Title, Normalize(string.Join(' ', paragraph)));
            }
        }

        /// <summary>
        /// Reduces the white space of a text to single spaces, so that a statement copied from a wrapped paragraph is found.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The text, with single spaces between its words.</returns>
        private static string Normalize(string text)
        {
            return string.Join(' ', text.Split(WhiteSpaceCharacters, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Gets the part of a passage shown around a statement: the statement, with at most <see cref="ExcerptContextLength"/>
        /// characters before and after it.
        /// </summary>
        /// <param name="passage">The text of the passage.</param>
        /// <param name="statement">The statement, which the passage contains.</param>
        /// <returns>The excerpt, with an ellipsis where the passage is cut.</returns>
        private static string GetExcerpt(string passage, string statement)
        {
            var statementIndex = passage.IndexOf(statement, StringComparison.Ordinal);
            var start = Math.Max(0, statementIndex - ExcerptContextLength);
            var end = Math.Min(passage.Length, statementIndex + statement.Length + ExcerptContextLength);

            return $"{(start > 0 ? "…" : string.Empty)}{passage[start..end]}{(end < passage.Length ? "…" : string.Empty)}";
        }

        /// <summary>
        /// Finds the identifiers that the section and its subsections define, that the proposal does not hold and that the
        /// model does not have.
        /// </summary>
        /// <param name="document">The <see cref="TextDocument"/>.</param>
        /// <param name="sections">The section and its subsections.</param>
        /// <param name="idPattern">The regular expression of the identifiers, or <c>null</c>.</param>
        /// <param name="requirements">The <see cref="ProposedRequirement"/>s.</param>
        /// <param name="modelledIds">The identifiers of the requirements of the model.</param>
        /// <returns>The missed identifiers, in the order of the document; none without pattern.</returns>
        private static IReadOnlyList<string> FindMissedIds(TextDocument document, IReadOnlyList<DocumentSection> sections, string idPattern,
            IReadOnlyList<ProposedRequirement> requirements, HashSet<string> modelledIds)
        {
            if (string.IsNullOrWhiteSpace(idPattern))
            {
                return [];
            }

            var identifiers = document.FindIdentifiersMatching(idPattern);
            var proposedIds = requirements.Select(requirement => requirement.ReqId).ToHashSet(StringComparer.Ordinal);

            return
            [
                .. sections.SelectMany(section => identifiers[section.Number - 1])
                    .Where(identifier => !proposedIds.Contains(identifier) && !modelledIds.Contains(identifier))
                    .Distinct(StringComparer.Ordinal)
            ];
        }

        /// <summary>
        /// A passage of a section, where a statement is searched.
        /// </summary>
        /// <param name="SectionTitle">The title of the section or subsection of the passage.</param>
        /// <param name="Text">The text of the passage, its white space reduced to single spaces.</param>
        private sealed record Passage(string SectionTitle, string Text);

        /// <summary>
        /// What the check of each requirement needs.
        /// </summary>
        /// <param name="ModelProvider">The <see cref="IModelProvider"/>.</param>
        /// <param name="PackageChanges">The change that creates the new package, or no change.</param>
        /// <param name="OwnerId">The <c>Id</c> of the existing package, or <c>null</c>.</param>
        /// <param name="NewPackage">The name of the new package, or <c>null</c>.</param>
        /// <param name="ModelledIds">The identifiers of the requirements of the model.</param>
        /// <param name="Requirements">All the proposed requirements, to find the repeated identifiers.</param>
        /// <param name="Section">The section, without its subsections.</param>
        /// <param name="Passages">The passages of the section and its subsections.</param>
        private sealed record ProposalContext(IModelProvider ModelProvider, IReadOnlyList<ModelChange> PackageChanges, Guid? OwnerId, string NewPackage,
            HashSet<string> ModelledIds, IReadOnlyList<ProposedRequirement> Requirements, DocumentSection Section, IReadOnlyList<Passage> Passages);
    }
}
