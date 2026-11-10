// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementBatchBuilder.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Builds the batch of changes, as apply_changes takes it, that creates proposed requirements: the package that owns them
    /// when it is new, then one requirement usage per requirement.
    /// </summary>
    public static class RequirementBatchBuilder
    {
        /// <summary>
        /// The metaclass of the package that owns the requirements.
        /// </summary>
        private const string PackageType = "Package";

        /// <summary>
        /// The metaclass of a requirement.
        /// </summary>
        private const string RequirementType = "RequirementUsage";

        /// <summary>
        /// Creates the change that creates the package that owns the requirements, when it is new. Its temporary name is its
        /// name, so that the requirements of the batch, and later batches, can designate it.
        /// </summary>
        /// <param name="ownerId">The <c>Id</c> of the package under which the new package is created, or <c>null</c> for the top level.</param>
        /// <param name="newPackage">The name of the new package, or <c>null</c> when the requirements go to an existing package.</param>
        /// <returns>The change that creates the package, or no change.</returns>
        public static IReadOnlyList<ModelChange> CreatePackageChanges(Guid? ownerId, string newPackage)
        {
            return string.IsNullOrWhiteSpace(newPackage)
                ? []
                : [new ModelChange { Identity = newPackage, Payload = new ElementPayload { Type = PackageType, Name = newPackage, Owner = ownerId?.ToString() } }];
        }

        /// <summary>
        /// Creates the change that creates a requirement usage from a proposed requirement.
        /// </summary>
        /// <param name="requirement">The <see cref="ProposedRequirement"/>.</param>
        /// <param name="option">The <see cref="ImportOption"/>: which statement and which constraint the requirement gets.</param>
        /// <param name="ownerId">The <c>Id</c> of the package that owns the requirement when <paramref name="newPackage"/> is <c>null</c>.</param>
        /// <param name="newPackage">The name of the package created by the same batch, or <c>null</c>.</param>
        /// <returns>The <see cref="ModelChange"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requirement"/> is <c>null</c>.</exception>
        public static ModelChange CreateRequirementChange(ProposedRequirement requirement, ImportOption option, Guid? ownerId, string newPackage)
        {
            ArgumentNullException.ThrowIfNull(requirement);

            var payload = new ElementPayload
            {
                Type = RequirementType,
                Name = requirement.Name,
                Owner = string.IsNullOrWhiteSpace(newPackage) ? ownerId?.ToString() : newPackage,
                ReqId = requirement.ReqId,
                Text = option == ImportOption.WithRewrite ? requirement.RewrittenText : requirement.Text,
                Constraint = option switch
                {
                    ImportOption.AsWritten => requirement.Constraint,
                    ImportOption.WithRewrite => requirement.RewrittenConstraint,
                    _ => null
                }
            };

            return new ModelChange { Payload = payload };
        }

        /// <summary>
        /// Builds the batch that imports the requirements chosen by the user, in the order of the proposal.
        /// </summary>
        /// <param name="proposal">The <see cref="RequirementProposal"/> kept by the server.</param>
        /// <param name="choices">The <see cref="ImportChoice"/>s of the user.</param>
        /// <returns>The changes: the new package if any, then one requirement usage per choice that is not merged.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="proposal"/> is <c>null</c>.</exception>
        /// <exception cref="McpException">
        /// Thrown when no requirement is chosen, or when a choice is not a requirement of the proposal, is chosen twice, cannot be
        /// imported, asks for a rewrite that the requirement does not have, or is merged into a requirement that this batch does
        /// not create.
        /// </exception>
        public static IReadOnlyList<ModelChange> Build(RequirementProposal proposal, IReadOnlyList<ImportChoice> choices)
        {
            ArgumentNullException.ThrowIfNull(proposal);

            if (choices == null || choices.Count == 0)
            {
                throw new McpException("No requirement is chosen: tick at least one requirement to import.");
            }

            // The first row of an identifier is the one that can be imported: a later row with the same identifier is blocked.
            var rows = proposal.Requirements.DistinctBy(row => row.Requirement.ReqId, StringComparer.Ordinal).ToDictionary(row => row.Requirement.ReqId, StringComparer.Ordinal);

            var problems = choices.Select(choice => CheckChoice(choice, rows))
                .Concat(choices.GroupBy(choice => choice?.ReqId, StringComparer.Ordinal)
                    .Where(group => group.Key != null && group.Count() > 1)
                    .Select(group => $"{group.Key} is chosen more than once."))
                .Concat(CheckMerges(choices))
                .Where(problem => problem != null)
                .ToList();

            if (problems.Count > 0)
            {
                throw new McpException($"Nothing has been imported. {string.Join(" ", problems)}");
            }

            var options = choices.ToDictionary(choice => choice.ReqId, choice => choice.Option, StringComparer.Ordinal);

            return
            [
                .. CreatePackageChanges(proposal.OwnerId, proposal.NewPackage),
                .. proposal.Requirements
                    .Where(row => !row.IsBlocked && options.TryGetValue(row.Requirement.ReqId, out var option) && option != ImportOption.MergeInto)
                    .Select(row => CreateRequirementChange(row.Requirement, options[row.Requirement.ReqId], proposal.OwnerId, proposal.NewPackage))
            ];
        }

        /// <summary>
        /// Checks a choice of the user against the proposal.
        /// </summary>
        /// <param name="choice">The <see cref="ImportChoice"/>.</param>
        /// <param name="rows">The first row of each identifier of the proposal.</param>
        /// <returns>The problem, or <c>null</c> when the choice can be imported.</returns>
        private static string CheckChoice(ImportChoice choice, Dictionary<string, ExtractedRequirement> rows)
        {
            if (choice?.ReqId == null || !rows.TryGetValue(choice.ReqId, out var row))
            {
                return $"'{choice?.ReqId}' is not a requirement of the proposal.";
            }

            if (row.IsBlocked)
            {
                var blockingFinding = row.Findings.First(finding => finding.Kind is ExtractionFindingKind.AlreadyModelled or ExtractionFindingKind.Repeated);

                return $"{choice.ReqId} cannot be imported: {blockingFinding.Message}";
            }

            if (!Enum.IsDefined(choice.Option))
            {
                return $"The option of {choice.ReqId} must be AsWritten, WithRewrite, AsGoal or MergeInto.";
            }

            var hasValidRewrite = !string.IsNullOrWhiteSpace(row.Requirement.RewrittenText) && row.Findings.All(finding => finding.Kind != ExtractionFindingKind.InvalidRewrite);

            return choice.Option == ImportOption.WithRewrite && !hasValidRewrite
                ? $"{choice.ReqId} has no valid rewrite: import it as written or as a goal."
                : null;
        }

        /// <summary>
        /// Checks the choices that merge a requirement into another one: the target must be created by the same batch.
        /// </summary>
        /// <param name="choices">The <see cref="ImportChoice"/>s of the user, some of which may be <c>null</c>.</param>
        /// <returns>One problem, or <c>null</c>, per choice that merges a requirement.</returns>
        private static IEnumerable<string> CheckMerges(IReadOnlyList<ImportChoice> choices)
        {
            var options = choices.Where(choice => choice?.ReqId != null).GroupBy(choice => choice.ReqId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Option, StringComparer.Ordinal);

            return choices.Where(choice => choice?.ReqId != null && choice.Option == ImportOption.MergeInto).Select(choice => CheckMerge(choice, options));
        }

        /// <summary>
        /// Checks a choice that merges a requirement into another one.
        /// </summary>
        /// <param name="choice">The <see cref="ImportChoice"/>.</param>
        /// <param name="options">The <see cref="ImportOption"/> of each chosen requirement.</param>
        /// <returns>The problem, or <c>null</c> when the target is created by the batch.</returns>
        private static string CheckMerge(ImportChoice choice, Dictionary<string, ImportOption> options)
        {
            if (string.IsNullOrWhiteSpace(choice.MergeInto))
            {
                return $"{choice.ReqId} must name the requirement that it is merged into.";
            }

            if (choice.MergeInto == choice.ReqId)
            {
                return $"{choice.ReqId} cannot be merged into itself.";
            }

            if (!options.TryGetValue(choice.MergeInto, out var targetOption))
            {
                return $"{choice.ReqId} is merged into {choice.MergeInto}, which is not imported.";
            }

            return targetOption == ImportOption.MergeInto ? $"{choice.ReqId} is merged into {choice.MergeInto}, which is merged too." : null;
        }
    }
}
