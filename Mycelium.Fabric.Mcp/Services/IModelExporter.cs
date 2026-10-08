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
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;

    using SysML2.NET.Core.DTO.Root.Elements;

    /// <summary>
    /// Writes a SysML v2 model to a file of an export folder chosen by the host.
    /// </summary>
    /// <remarks>
    /// Each implementation writes a single <see cref="ModelExportFormat"/>, so that other formats, such as the textual
    /// notation, can be added next to it.
    /// </remarks>
    public interface IModelExporter
    {
        /// <summary>
        /// Gets the format of the files that this exporter writes.
        /// </summary>
        ModelExportFormat Format { get; }

        /// <summary>
        /// Writes the DTOs of the given elements to a new file of the export folder. An existing file is never replaced.
        /// </summary>
        /// <param name="elements">The DTOs of the elements of the model to write.</param>
        /// <param name="fileName">
        /// The simple name of the file, without folder, or <c>null</c> for a name made of the current date and time.
        /// </param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the export, leaving no file.</param>
        /// <returns>A <see cref="Task"/> whose result is the full path of the written file.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="elements"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="fileName"/> is not a simple name, or when the export folder already has a file with
        /// this name. The message is sent back to the AI assistant.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancellationToken"/> is canceled.
        /// </exception>
        Task<string> ExportAsync(IReadOnlyCollection<IElement> elements, string fileName, CancellationToken cancellationToken);
    }
}
