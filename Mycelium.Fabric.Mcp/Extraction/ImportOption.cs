// ------------------------------------------------------------------------------------------------
//  <copyright file="ImportOption.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Extraction
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// How the user imports a proposed requirement. It is written as text in JSON (for example <c>"AsGoal"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ImportOption>))]
    public enum ImportOption
    {
        /// <summary>
        /// The statement as written in the document, with its constraint if it has one.
        /// </summary>
        AsWritten,

        /// <summary>
        /// The statement rewritten by the AI assistant, with the constraint of the rewrite if it has one.
        /// </summary>
        WithRewrite,

        /// <summary>
        /// The statement as written, without constraint: a design goal, which is not checked.
        /// </summary>
        AsGoal
    }
}
