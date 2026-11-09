// ------------------------------------------------------------------------------------------------
//  <copyright file="ExtractionFindingKind.cs" company="Starion Group S.A.">
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
    /// What is wrong with a requirement proposed by the AI assistant. It is written as text in JSON (for example
    /// <c>"AlreadyModelled"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ExtractionFindingKind>))]
    public enum ExtractionFindingKind
    {
        /// <summary>
        /// The statement is not written as such in the section or its subsections. Found by the server.
        /// </summary>
        NotInSection,

        /// <summary>
        /// A requirement of the model already has this identifier: the requirement cannot be imported. Found by the server.
        /// </summary>
        AlreadyModelled,

        /// <summary>
        /// An earlier requirement of the proposal has the same identifier: the requirement cannot be imported. Found by the server.
        /// </summary>
        Repeated,

        /// <summary>
        /// The requirement, as written, would be refused by apply_changes, for example for an unknown unit. Found by the server.
        /// </summary>
        InvalidChange,

        /// <summary>
        /// The rewritten requirement would be refused by apply_changes. Found by the server.
        /// </summary>
        InvalidRewrite,

        /// <summary>
        /// The requirement that the AI assistant proposes to merge into cannot receive it. Found by the server.
        /// </summary>
        InvalidMerge,

        /// <summary>
        /// The requirement must be clarified, for the reason given by the AI assistant.
        /// </summary>
        ToClarify
    }
}
