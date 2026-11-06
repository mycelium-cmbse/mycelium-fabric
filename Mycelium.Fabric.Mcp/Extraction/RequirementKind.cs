// ------------------------------------------------------------------------------------------------
//  <copyright file="RequirementKind.cs" company="Starion Group S.A.">
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
    /// What a requirement found in a document is about, as the view of the extraction shows it. It is not modelled. It is
    /// written as text in JSON (for example <c>"Performance"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RequirementKind>))]
    public enum RequirementKind
    {
        /// <summary>
        /// What the system does.
        /// </summary>
        Functional,

        /// <summary>
        /// How well the system does it: a mass, a power, a duration, a data rate...
        /// </summary>
        Performance,

        /// <summary>
        /// What the system exchanges with another system.
        /// </summary>
        Interface,

        /// <summary>
        /// How the system is built: a redundancy, a material, a number of units...
        /// </summary>
        Design,

        /// <summary>
        /// How the system is operated.
        /// </summary>
        Operational,

        /// <summary>
        /// Any other requirement.
        /// </summary>
        Other
    }
}
