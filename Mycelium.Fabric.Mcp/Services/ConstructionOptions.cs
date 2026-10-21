// ------------------------------------------------------------------------------------------------
//  <copyright file="ConstructionOptions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    /// <summary>
    /// The settings of the construction of the model with apply_changes, read from the <see cref="SectionName"/> section of
    /// the configuration of the host, for example <c>{ "FabricMcp": { "ReportNewIssues": false } }</c> in its
    /// <c>appsettings.json</c> file.
    /// </summary>
    public class ConstructionOptions
    {
        /// <summary>
        /// The name of the configuration section that holds these settings.
        /// </summary>
        public const string SectionName = "FabricMcp";

        /// <summary>
        /// Gets or sets a value indicating whether apply_changes returns the validation issues that an applied batch has
        /// introduced. It is <c>true</c> unless the configuration turns it off.
        /// </summary>
        public bool ReportNewIssues { get; set; } = true;
    }
}
