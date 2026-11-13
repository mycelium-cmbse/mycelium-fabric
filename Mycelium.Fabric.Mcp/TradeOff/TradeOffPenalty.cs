// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffPenalty.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// What the check of a requirement on a combination teaches the solver: every combination that takes the same options
    /// for the choices that the check has read breaks, or meets, one requirement more than the limits of the problem say.
    /// A requirement that the solver cannot write as a limit (a text, a condition, a product) reaches it this way.
    /// </summary>
    /// <param name="RequirementIndex">The position of the requirement in the problem.</param>
    /// <param name="Options">The index of the option taken, by index of the choice, for the choices that the check has read.</param>
    /// <param name="Weight">
    /// What to add to the number of requirements that these combinations break: 1 for a broken requirement that the limits do
    /// not see, -1 for a requirement that holds although its limit says the opposite.
    /// </param>
    public sealed record TradeOffPenalty(int RequirementIndex, IReadOnlyDictionary<int, int> Options, int Weight)
    {
        /// <summary>
        /// Gets the text that identifies the penalty: two penalties with the same key say the same thing.
        /// </summary>
        public string Key => $"{this.RequirementIndex}:{string.Join(",", this.Options.OrderBy(option => option.Key).Select(option => $"{option.Key}={option.Value}"))}";

        /// <summary>
        /// Tells whether a combination takes the options of the penalty.
        /// </summary>
        /// <param name="selection">The index of the option taken for each choice.</param>
        /// <returns><c>true</c> when the penalty applies to the combination.</returns>
        public bool AppliesTo(IReadOnlyList<int> selection)
        {
            return this.Options.All(option => selection[option.Key] == option.Value);
        }
    }
}
