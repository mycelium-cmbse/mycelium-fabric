// ------------------------------------------------------------------------------------------------
//  <copyright file="IModelExporter.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System;
    using System.Collections.Generic;

    using ModelContextProtocol;

    using SysML2.NET.Core.POCO.Root.Elements;

    /// <summary>
    /// Writes a SysML v2 model to a file of an export folder chosen by the host.
    /// </summary>
    public interface IModelExporter
    {
        /// <summary>
        /// Writes the given elements to a new file of the export folder. An existing file is never replaced.
        /// </summary>
        /// <param name="elements">The elements of the model to write.</param>
        /// <param name="fileName">
        /// The simple name of the file, without folder, or <c>null</c> for a name made of the current date and time.
        /// </param>
        /// <returns>The full path of the written file.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="elements"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="fileName"/> is not a simple name, or when the export folder already has a file with
        /// this name. The message is sent back to the AI assistant.
        /// </exception>
        string Export(IReadOnlyCollection<IElement> elements, string fileName);
    }
}
