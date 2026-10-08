// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelExportFormat.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The formats in which an <see cref="IModelExporter"/> writes a model. They are written as text in JSON (for example
    /// <c>"Json"</c>), so that the AI assistant reads names rather than numbers.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ModelExportFormat>))]
    public enum ModelExportFormat
    {
        /// <summary>
        /// The JSON of the Systems Modeling API, the format that the <see cref="InMemoryModelProvider"/> loads.
        /// </summary>
        Json
    }
}
