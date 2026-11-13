// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffRequirementCheck.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.TradeOff
{
    using System.Collections.Generic;

    using Mycelium.Fabric.Mcp.Requirements;

    /// <summary>
    /// The check of a requirement of the model on one combination of a trade-off.
    /// </summary>
    /// <param name="Requirement">The <see cref="TradeOffRequirement"/>.</param>
    /// <param name="Check">The <see cref="RequirementCheck"/>, as <c>check_requirements</c> would give it for the changed model.</param>
    /// <param name="ReadOptions">
    /// The index of the option taken, by index of the choice, for the choices that the check has read: the check is the same
    /// for every combination that takes these options, and the same for all the combinations when it has read none.
    /// </param>
    public sealed record TradeOffRequirementCheck(TradeOffRequirement Requirement, RequirementCheck Check, IReadOnlyDictionary<int, int> ReadOptions)
    {
        /// <summary>
        /// Gets a value indicating whether the check depends on the choices of the trade-off.
        /// </summary>
        public bool DependsOnChoices => this.ReadOptions.Count > 0;

        /// <summary>
        /// Gets a value indicating whether the requirement holds on the combination. A requirement that cannot be evaluated
        /// does not hold: a combination is never said to meet it.
        /// </summary>
        public bool IsMet => this.Check.Status == RequirementStatus.Satisfied;
    }
}
