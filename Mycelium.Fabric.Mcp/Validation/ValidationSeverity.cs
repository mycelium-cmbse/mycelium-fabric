// ------------------------------------------------------------------------------------------------
//  <copyright file="ValidationSeverity.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Validation
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// How serious the issues found by an <see cref="IValidationRule"/> are, from the most to the least serious. It is written
    /// as text in JSON (for example <c>"Warning"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ValidationSeverity>))]
    public enum ValidationSeverity
    {
        /// <summary>
        /// The model is broken: an element is missing, outside the containment tree, or breaks a rule of the metamodel.
        /// </summary>
        Error,

        /// <summary>
        /// The model is well formed but probably incomplete, for example a requirement that cannot be verified.
        /// </summary>
        Warning,

        /// <summary>
        /// A point to tidy up, for example a definition that is never used.
        /// </summary>
        Information
    }
}
