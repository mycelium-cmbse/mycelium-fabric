// ------------------------------------------------------------------------------------------------
//  <copyright file="StandardLibraryOptions.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    /// <summary>
    /// The settings of the standard library, read from the <see cref="SectionName"/> section of the configuration of the
    /// host, for example <c>{ "FabricMcp": { "LibraryDirectory": "D:\\SysML\\sysml.library.xmi" } }</c> in its
    /// <c>appsettings.json</c> file.
    /// </summary>
    public class StandardLibraryOptions
    {
        /// <summary>
        /// The name of the configuration section that holds these settings.
        /// </summary>
        public const string SectionName = "FabricMcp";

        /// <summary>
        /// Gets or sets the folder that holds the XMI files of the standard library: an absolute path, or a path relative to
        /// the folder of the server. When it is <c>null</c>, the <c>StandardLibrary</c> folder next to the server is used. The
        /// server does not come with the library: the <c>sysml.library.xmi</c> folder of the SysML v2 release of the OMG holds it.
        /// </summary>
        public string LibraryDirectory { get; set; }
    }
}
