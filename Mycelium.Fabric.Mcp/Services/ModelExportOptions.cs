// ------------------------------------------------------------------------------------------------
//  <copyright file="ModelExportOptions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    /// <summary>
    /// The settings of the export of the model, read from the <see cref="SectionName"/> section of the configuration of the
    /// host, for example <c>{ "FabricMcp": { "ExportDirectory": "D:\\Exports" } }</c> in its <c>appsettings.json</c> file.
    /// </summary>
    public class ModelExportOptions
    {
        /// <summary>
        /// The name of the configuration section that holds these settings.
        /// </summary>
        public const string SectionName = "FabricMcp";

        /// <summary>
        /// Gets or sets the folder in which the model is exported: an absolute path, or a path relative to the folder of the
        /// model file. When it is <c>null</c>, the <c>exports</c> folder next to the model file is used.
        /// </summary>
        public string ExportDirectory { get; set; }
    }
}
