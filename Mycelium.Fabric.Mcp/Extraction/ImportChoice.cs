// ------------------------------------------------------------------------------------------------
//  <copyright file="ImportChoice.cs" company="Starion Group S.A.">
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
    /// A requirement that the user imports from the view of the extraction, and how.
    /// </summary>
    public sealed record ImportChoice
    {
        /// <summary>
        /// Gets the identifier of the requirement in the proposal.
        /// </summary>
        [Description("The reqId of a requirement of the proposal.")]
        public string ReqId { get; init; }

        /// <summary>
        /// Gets how the requirement is imported.
        /// </summary>
        [Description("AsWritten, WithRewrite, AsGoal (without constraint) or MergeInto (not created).")]
        public ImportOption Option { get; init; }

        /// <summary>
        /// Gets the identifier of the requirement that receives this one, with <see cref="ImportOption.MergeInto"/>, or <c>null</c>.
        /// </summary>
        [Description("With MergeInto: the reqId of the requirement of the same import that receives this one.")]
        public string MergeInto { get; init; }
    }
}
