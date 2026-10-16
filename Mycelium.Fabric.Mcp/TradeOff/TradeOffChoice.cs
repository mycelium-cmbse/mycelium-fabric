// ------------------------------------------------------------------------------------------------
//  <copyright file="TradeOffChoice.cs" company="Starion Group S.A.">
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
    using System.ComponentModel;

    /// <summary>
    /// One choice to make in the <c>solve_trade_off</c> tool: an existing part of the model, or equipment to add, and the
    /// variants it may take. The solver takes exactly one variant for each choice.
    /// </summary>
    public sealed record TradeOffChoice
    {
        /// <summary>
        /// Gets the identifier of the existing part that the choice is about, or <c>null</c> for equipment to add.
        /// </summary>
        [Description("For an existing part: its identifier (Id), as returned by the other tools. The part must be under the root, and its sub-parts follow it "
            + "(a variant of a subsystem replaces the values of the whole subsystem). Leave it out for equipment to add.")]
        public Guid? PartId { get; init; }

        /// <summary>
        /// Gets the name of the choice, used by the rules.
        /// </summary>
        [Description("The name of the choice, unique and without '.', used by the rules ('propulsion.electric'). Required for equipment to add (for example 'fuelTank'); "
            + "for an existing part, the name of the part by default.")]
        public string Name { get; init; }

        /// <summary>
        /// Gets a value indicating whether equipment to add must be added.
        /// </summary>
        [Description("Optional, for equipment to add only: true when one of its variants must be taken. By default, the variant 'none' (not added) is also possible.")]
        public bool Required { get; init; }

        /// <summary>
        /// Gets the candidate variants of the choice.
        /// </summary>
        [Description("The candidate variants. The current variant of an existing part, or 'none' for equipment to add, is added by the server.")]
        public IReadOnlyList<TradeOffVariant> Variants { get; init; }
    }
}
