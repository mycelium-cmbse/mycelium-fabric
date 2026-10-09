// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementCheck.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Requirements
{
    using System;

    using Mycelium.Fabric.Mcp.Values;

    /// <summary>
    /// The check of a requirement against one part that satisfies it, or against none when no part satisfies it.
    /// </summary>
    public sealed record RequirementCheck
    {
        /// <summary>
        /// Gets the identifier of the requirement.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Gets the identifier of the requirement in its specification, for example <c>REQ-SYS-001</c>, or <c>null</c>.
        /// </summary>
        public string ReqId { get; init; }

        /// <summary>
        /// Gets the declared name of the requirement.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the outcome of the check.
        /// </summary>
        public RequirementStatus Status { get; init; }

        /// <summary>
        /// Gets the required constraints of the requirement, joined by <c>and</c>, for example <c>subj.mass * 1.2 &lt;= 150</c>,
        /// with <c>(unsupported)</c> for a constraint that is not evaluated, or <c>null</c> when it has none.
        /// </summary>
        public string Constraint { get; init; }

        /// <summary>
        /// Gets the assumptions of the requirement (its <c>assume constraint</c>s), joined by <c>and</c>, for example
        /// <c>subj.fuelLevel &gt;= subj.fuelTankCapacity</c>, or <c>null</c> when it has none.
        /// </summary>
        public string Assumption { get; init; }

        /// <summary>
        /// Gets the qualified name of the part that satisfies the requirement, or <c>null</c> when no part satisfies it.
        /// </summary>
        public string SatisfiedBy { get; init; }

        /// <summary>
        /// Gets the value of the constrained attribute for the satisfying part, computed as <c>sum_attribute</c> does, when
        /// the required constraints read a single attribute, or <c>null</c>.
        /// </summary>
        public double? Value { get; init; }

        /// <summary>
        /// Gets the unit of <see cref="Value"/>, or <c>null</c> when it has none.
        /// </summary>
        public Unit Unit { get; init; }

        /// <summary>
        /// Gets how far the value is from the limit, margin included, when the requirement has a single required constraint:
        /// positive or zero when this constraint holds, negative by the excess when it does not, or <c>null</c> when it is
        /// not evaluated.
        /// </summary>
        public double? Gap { get; init; }

        /// <summary>
        /// Gets the comparison that holds, for example <c>150.96 &gt; 150</c>, or why the requirement is not verifiable or not
        /// evaluated. When the requirement has assumptions or several required constraints, it gives this for each of them,
        /// for example <c>assume subj.cargoMass &lt;= 500: 450 &lt;= 500; require subj.range &gt;= 600: 580 &lt; 600</c>.
        /// </summary>
        public string Explanation { get; init; }
    }
}
