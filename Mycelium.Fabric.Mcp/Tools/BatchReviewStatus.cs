// ------------------------------------------------------------------------------------------------
//  <copyright file="BatchReviewStatus.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Where a batch of changes stands, as the view of the batch shows it. It is written as text in JSON (for example
    /// <c>"Pending"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<BatchReviewStatus>))]
    public enum BatchReviewStatus
    {
        /// <summary>
        /// The batch is valid and waits for the decision of the user; the model is unchanged.
        /// </summary>
        Pending,

        /// <summary>
        /// The batch has been applied to the model.
        /// </summary>
        Applied,

        /// <summary>
        /// The user has rejected the batch; the model is unchanged.
        /// </summary>
        Rejected,

        /// <summary>
        /// The server has refused the batch, because it is invalid or another batch is waiting; the model is unchanged.
        /// </summary>
        Refused
    }
}
