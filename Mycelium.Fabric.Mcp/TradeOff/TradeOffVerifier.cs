// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffVerifier.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// Checks the requirements of the model on the combinations of a trade-off, with the evaluation of
    /// <c>check_requirements</c>, so that what the solver proposes is verified rather than trusted. A check is kept and used
    /// again for every combination that takes the same options for the choices that it has read.
    /// </summary>
    public sealed class TradeOffVerifier
    {
        /// <summary>
        /// The position of each choice, by the key that a check reports to have read.
        /// </summary>
        private readonly Dictionary<Guid, int> decisionIndexes;

        /// <summary>
        /// The checks done so far, for each requirement.
        /// </summary>
        private readonly List<List<TradeOffRequirementCheck>> knownChecks;

        /// <summary>
        /// Initializes a new instance of the <see cref="TradeOffVerifier"/> class.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/> whose combinations are checked.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="problem"/> is <c>null</c>.
        /// </exception>
        public TradeOffVerifier(TradeOffProblem problem)
        {
            ArgumentNullException.ThrowIfNull(problem);

            this.Problem = problem;
            this.decisionIndexes = (problem.Space?.Keys ?? []).Select((key, index) => (key, index)).ToDictionary(pair => pair.key, pair => pair.index);
            this.knownChecks = [.. problem.Requirements.Select(_ => new List<TradeOffRequirementCheck>())];
        }

        /// <summary>
        /// Gets the <see cref="TradeOffProblem"/> whose combinations are checked.
        /// </summary>
        public TradeOffProblem Problem { get; }

        /// <summary>
        /// Checks the requirements of the problem on a combination.
        /// </summary>
        /// <param name="selection">The index of the option taken for each choice.</param>
        /// <param name="withIgnored">Whether the requirements that the user turned off are checked too.</param>
        /// <returns>The <see cref="TradeOffRequirementCheck"/>s, in the order of the requirements.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <c>null</c>.
        /// </exception>
        public IReadOnlyList<TradeOffRequirementCheck> Verify(IReadOnlyList<int> selection, bool withIgnored = false)
        {
            ArgumentNullException.ThrowIfNull(selection);

            return
            [
                .. this.Problem.Requirements
                    .Select((requirement, index) => (requirement, index))
                    .Where(pair => withIgnored || !pair.requirement.IsIgnored)
                    .Select(pair => this.Verify(pair.index, selection))
            ];
        }

        /// <summary>
        /// Counts what a combination does not meet: the constraints of the request that it breaks, and the requirements
        /// that depend on the choices and do not hold on it.
        /// </summary>
        /// <param name="selection">The index of the option taken for each choice.</param>
        /// <returns>The number of constraints and requirements that the combination does not meet.</returns>
        public int CountBroken(IReadOnlyList<int> selection)
        {
            return this.Problem.Limits.Count(limit => !limit.IsFromModel && !this.Problem.IsSatisfied(limit, selection))
                + this.Verify(selection).Count(check => check.DependsOnChoices && !check.IsMet);
        }

        /// <summary>
        /// Finds what the checks of a combination teach the solver: for each requirement that depends on the choices, the
        /// difference between what the check says and what the limits of the problem say.
        /// </summary>
        /// <param name="selection">The index of the option taken for each choice.</param>
        /// <returns>The <see cref="TradeOffPenalty"/>s, empty when the limits already tell everything.</returns>
        public IReadOnlyList<TradeOffPenalty> FindPenalties(IReadOnlyList<int> selection)
        {
            return
            [
                .. this.Problem.Requirements
                    .Select((requirement, index) => (requirement, index))
                    .Where(pair => !pair.requirement.IsIgnored)
                    .Select(pair => (pair.index, check: this.Verify(pair.index, selection)))
                    .Where(pair => pair.check.DependsOnChoices)
                    .Select(pair => (pair.index, pair.check, weight: (pair.check.IsMet ? 0 : 1) - (this.IsBrokenForTheSolver(pair.check.Requirement, selection) ? 1 : 0)))
                    .Where(pair => pair.weight != 0)
                    .Select(pair => new TradeOffPenalty(pair.index, pair.check.ReadOptions, pair.weight))
            ];
        }

        /// <summary>
        /// Tells whether the limit of a requirement says that a combination breaks it.
        /// </summary>
        /// <param name="requirement">The requirement.</param>
        /// <param name="selection">The index of the option taken for each choice.</param>
        /// <returns><c>true</c> when the requirement has a limit that the combination breaks.</returns>
        private bool IsBrokenForTheSolver(TradeOffRequirement requirement, IReadOnlyList<int> selection)
        {
            return requirement.LimitIndex is { } limitIndex && !this.Problem.IsSatisfied(this.Problem.Limits[limitIndex], selection);
        }

        /// <summary>
        /// Checks one requirement on a combination, or takes the check of a combination that holds the same thing in the
        /// parts that this check has read.
        /// </summary>
        /// <param name="requirementIndex">The position of the requirement in the problem.</param>
        /// <param name="selection">The index of the option taken for each choice.</param>
        /// <returns>The <see cref="TradeOffRequirementCheck"/>.</returns>
        private TradeOffRequirementCheck Verify(int requirementIndex, IReadOnlyList<int> selection)
        {
            var known = this.knownChecks[requirementIndex].Find(check => check.ReadOptions.All(option => selection[option.Key] == option.Value));

            if (known != null)
            {
                return known;
            }

            var requirement = this.Problem.Requirements[requirementIndex];
            var combinationCheck = RequirementChecker.CheckRequirement(requirement.Source, this.Problem.Space?.CreateSelection(selection));

            var readOptions = combinationCheck.ReadPartIds
                .Select(key => this.decisionIndexes[key])
                .ToDictionary(decisionIndex => decisionIndex, decisionIndex => selection[decisionIndex]);

            var check = new TradeOffRequirementCheck(requirement, combinationCheck.Check, readOptions);
            this.knownChecks[requirementIndex].Add(check);

            return check;
        }
    }
}
