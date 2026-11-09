// ------------------------------------------------------------------------------------------------
//  <copyright file="SavedRow.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    using System.ComponentModel;

    /// <summary>
    /// A row of the view of the extraction as the user has left it, saved without importing anything.
    /// </summary>
    public sealed record SavedRow
    {
        /// <summary>
        /// Gets the identifier of the requirement.
        /// </summary>
        [Description("The reqId of a requirement of the proposal.")]
        public string ReqId { get; init; }

        /// <summary>
        /// Gets the option that the user has applied to the row, or <c>null</c> when none is applied.
        /// </summary>
        [Description("Optional: the option applied to the row: AsWritten, WithRewrite, AsGoal or MergeInto.")]
        public ImportOption? Option { get; init; }

        /// <summary>
        /// Gets the identifier of the requirement that receives this one, with <see cref="ImportOption.MergeInto"/>, or <c>null</c>.
        /// </summary>
        [Description("With MergeInto: the reqId of the requirement that receives this one.")]
        public string MergeInto { get; init; }

        /// <summary>
        /// Gets a value indicating whether the row is ticked.
        /// </summary>
        [Description("Whether the row is ticked.")]
        public bool Ticked { get; init; }
    }
}
