// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffAxes.cs" company="Starion Group S.A.">
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
    using System.Linq;

    /// <summary>
    /// Chooses the totals that the view of a trade-off draws on its chart: the first objective, then the totals that change
    /// the most between the combinations that the user compares.
    /// </summary>
    public static class TradeOffAxes
    {
        /// <summary>
        /// The largest number of axes of the chart.
        /// </summary>
        public const int MaximumAxes = 2;

        /// <summary>
        /// Chooses the axes of the chart. The first objective comes first. The other totals follow by how much they change,
        /// compared to their largest value, between the compared combinations, or between all the combinations when none
        /// changes there; a total that changes nothing, or that is equal to an axis already chosen on every combination, is
        /// left out. When no total changes, the first one is drawn alone.
        /// </summary>
        /// <param name="attributes">The names of the totals, in their order in the study.</param>
        /// <param name="objectives">The names of the attributes of the objectives, by order of priority.</param>
        /// <param name="compared">The totals of the compared combinations: the current state and the best ones.</param>
        /// <param name="others">The totals of the other combinations of the chart.</param>
        /// <returns>The names of the totals drawn, at most <see cref="MaximumAxes"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
        public static IReadOnlyList<string> Choose(IReadOnlyList<string> attributes, IReadOnlyList<string> objectives,
            IReadOnlyList<IReadOnlyDictionary<string, double>> compared, IReadOnlyList<IReadOnlyDictionary<string, double>> others)
        {
            ArgumentNullException.ThrowIfNull(attributes);
            ArgumentNullException.ThrowIfNull(objectives);
            ArgumentNullException.ThrowIfNull(compared);
            ArgumentNullException.ThrowIfNull(others);

            var rows = attributes.Any(attribute => GetSpread(compared, attribute) > 0) ? compared : [.. compared, .. others];
            var axes = objectives.Where(attributes.Contains).Take(1).ToList();

            var candidates = attributes
                .Where(attribute => !axes.Contains(attribute))
                .Select(attribute => (Attribute: attribute, Spread: GetSpread(rows, attribute)))
                .Where(candidate => candidate.Spread > 0)
                .OrderByDescending(candidate => candidate.Spread);

            foreach (var candidate in candidates)
            {
                if (axes.Count == MaximumAxes)
                {
                    break;
                }

                if (!axes.Exists(axis => AreEqual(rows, axis, candidate.Attribute)))
                {
                    axes.Add(candidate.Attribute);
                }
            }

            if (axes.Count == 0 && attributes.Count > 0)
            {
                axes.Add(attributes[0]);
            }

            return axes;
        }

        /// <summary>
        /// Gets how much a total changes between combinations, compared to its largest value: 0 when it does not change, 1
        /// when it goes from 0 to a value.
        /// </summary>
        /// <param name="rows">The totals of the combinations.</param>
        /// <param name="attribute">The name of the total.</param>
        /// <returns>The spread, from 0 to 1.</returns>
        private static double GetSpread(IReadOnlyList<IReadOnlyDictionary<string, double>> rows, string attribute)
        {
            var values = rows.Where(row => row.ContainsKey(attribute)).Select(row => row[attribute]).ToList();

            if (values.Count == 0)
            {
                return 0;
            }

            var largest = values.Max(Math.Abs);
            return largest == 0 ? 0 : (values.Max() - values.Min()) / largest;
        }

        /// <summary>
        /// Tells whether two totals are equal on every combination, as the total of a root and the same total named
        /// otherwise.
        /// </summary>
        /// <param name="rows">The totals of the combinations.</param>
        /// <param name="first">The name of the first total.</param>
        /// <param name="second">The name of the second total.</param>
        /// <returns><c>true</c> when they are equal on every combination that has both.</returns>
        private static bool AreEqual(IReadOnlyList<IReadOnlyDictionary<string, double>> rows, string first, string second)
        {
            return rows
                .Where(row => row.ContainsKey(first) && row.ContainsKey(second))
                .All(row => Math.Abs(row[first] - row[second]) < 1e-9);
        }
    }
}
