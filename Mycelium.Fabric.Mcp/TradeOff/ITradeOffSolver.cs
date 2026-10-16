// ------------------------------------------------------------------------------------------------
//  <copyright file="ITradeOffSolver.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System;

    /// <summary>
    /// Finds the best combinations of a <see cref="TradeOffProblem"/>, so that the AI assistant never searches them itself.
    /// </summary>
    public interface ITradeOffSolver
    {
        /// <summary>
        /// Finds the best combinations of the problem: each takes exactly one option of each decision and respects every limit
        /// and dependency. They are ranked by the objectives, one after the other by order of priority, then by the number of
        /// changes from the current state. When a decision is compared, each of its variants gets its own best combinations.
        /// </summary>
        /// <param name="problem">The <see cref="TradeOffProblem"/> to solve.</param>
        /// <returns>
        /// The <see cref="TradeOffSolution"/>, with at most <see cref="TradeOffProblem.Count"/> combinations for each alternative,
        /// or the conflicts of an alternative that has none.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="problem"/> is <c>null</c>.
        /// </exception>
        TradeOffSolution Solve(TradeOffProblem problem);
    }
}
