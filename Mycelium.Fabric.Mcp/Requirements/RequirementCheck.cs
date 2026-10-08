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
        /// Gets the constraint of the requirement, for example <c>subj.mass * 1.2 &lt;= 150</c>, or <c>null</c> when it has
        /// none or when its form is not supported.
        /// </summary>
        public string Constraint { get; init; }

        /// <summary>
        /// Gets the qualified name of the part that satisfies the requirement, or <c>null</c> when no part satisfies it.
        /// </summary>
        public string SatisfiedBy { get; init; }

        /// <summary>
        /// Gets the value of the constrained attribute for the satisfying part, computed as <c>sum_attribute</c> does, or
        /// <c>null</c> when the requirement is not evaluated.
        /// </summary>
        public double? Value { get; init; }

        /// <summary>
        /// Gets how far the value is from the limit, margin included: positive or zero when the requirement is satisfied,
        /// negative by the excess when it is not, or <c>null</c> when it is not evaluated.
        /// </summary>
        public double? Gap { get; init; }

        /// <summary>
        /// Gets the comparison that holds, for example <c>150.96 &gt; 150</c>, or why the requirement is not verifiable or not
        /// evaluated.
        /// </summary>
        public string Explanation { get; init; }
    }
}
