// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffSearch.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Searches the best combinations of a problem with a solver and a verifier: the solver proposes, the verifier checks
    /// every requirement of the model on what is proposed, and what the checks teach goes back to the solver as penalties,
    /// until the solver proposes nothing that the checks contradict. A requirement that the solver cannot read as a limit
    /// (a text, a condition, a product) is taken into account this way.
    /// </summary>
    public static class TradeOffSearch
    {
        /// <summary>
        /// The largest number of times that the solver is called for one search.
        /// </summary>
        public const int MaximumRounds = 25;

        /// <summary>
        /// Searches the best combinations of a problem.
        /// </summary>
        /// <param name="solver">The <see cref="ITradeOffSolver"/> that proposes combinations.</param>
        /// <param name="problem">The <see cref="TradeOffProblem"/> to solve.</param>
        /// <param name="verifier">The <see cref="TradeOffVerifier"/> that checks the requirements on the combinations.</param>
        /// <returns>
        /// The problem with the penalties that the checks have taught, and its <see cref="TradeOffSolution"/>, whose status is
        /// <see cref="TradeOffStatus.Feasible"/> rather than <see cref="TradeOffStatus.Optimal"/> when the checks still
        /// taught something after the last round: the combinations are checked, but a better one may exist.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="solver"/>, <paramref name="problem"/> or <paramref name="verifier"/> is <c>null</c>.
        /// </exception>
        public static (TradeOffProblem Problem, TradeOffSolution Solution) Solve(ITradeOffSolver solver, TradeOffProblem problem, TradeOffVerifier verifier)
        {
            ArgumentNullException.ThrowIfNull(solver);
            ArgumentNullException.ThrowIfNull(problem);
            ArgumentNullException.ThrowIfNull(verifier);

            List<TradeOffPenalty> penalties = [.. problem.Penalties];
            var knownKeys = penalties.Select(penalty => penalty.Key).ToHashSet();

            TradeOffProblem current;
            TradeOffSolution solution;
            List<TradeOffPenalty> newPenalties = [];
            var round = 0;

            do
            {
                penalties.AddRange(newPenalties);
                current = problem with { Penalties = [.. penalties] };
                solution = solver.Solve(current);

                newPenalties = solution.Alternatives
                    .SelectMany(alternative => alternative.Selections)
                    .SelectMany(verifier.FindPenalties)
                    .Where(penalty => knownKeys.Add(penalty.Key))
                    .ToList();

                round++;
            }
            while (newPenalties.Count > 0 && round < MaximumRounds);

            // The checks still taught something after the last round: a better combination may exist.
            return (current, newPenalties.Count > 0 && solution.Status == TradeOffStatus.Optimal ? solution with { Status = TradeOffStatus.Feasible } : solution);
        }
    }
}
