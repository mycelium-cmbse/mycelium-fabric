// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffDecision.cs" company="Starion Group S.A.">
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
    /// A checked <see cref="TradeOffChoice"/> of a <see cref="TradeOffProblem"/>: the options among which the solver takes
    /// exactly one.
    /// </summary>
    /// <param name="Name">The name of the choice, used by the rules.</param>
    /// <param name="PartId">The <c>Id</c> of the existing part, or <c>null</c> for equipment to add.</param>
    /// <param name="QualifiedName">The qualified name of the existing part, or <c>null</c> for equipment to add.</param>
    /// <param name="Options">The options of the choice, exactly one of which is the current state.</param>
    /// <param name="Required">
    /// Whether equipment to add must be added: its current option, <c>none</c>, is then never selected.
    /// </param>
    public sealed record TradeOffDecision(string Name, Guid? PartId, string QualifiedName, IReadOnlyList<TradeOffOption> Options, bool Required)
    {
        /// <summary>
        /// Gets the index of the option that is the current state.
        /// </summary>
        public int CurrentOptionIndex => this.Options.Select((option, index) => (option, index)).First(pair => pair.option.IsCurrent).index;

        /// <summary>
        /// Gets the indexes of the options of a variant, one for each number of units.
        /// </summary>
        /// <param name="variant">The name of the variant.</param>
        /// <returns>The indexes of the options of the variant, empty when the choice has no such variant.</returns>
        public IReadOnlyList<int> GetOptionIndexes(string variant)
        {
            return [.. this.Options.Select((option, index) => (option, index)).Where(pair => pair.option.Variant == variant).Select(pair => pair.index)];
        }
    }
}
