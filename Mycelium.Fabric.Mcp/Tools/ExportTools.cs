// ------------------------------------------------------------------------------------------------
//  <copyright file="ExportTools.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Tools
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;
    using ModelContextProtocol.Server;

    using Mycelium.Fabric.Mcp.Services;

    /// <summary>
    /// The MCP tools that let an AI assistant export the SysML v2 model, so that it is kept when the server stops.
    /// </summary>
    [McpServerToolType]
    public class ExportTools
    {
        /// <summary>
        /// The <see cref="IModelProvider"/> that gives access to the model.
        /// </summary>
        private readonly IModelProvider modelProvider;

        /// <summary>
        /// The <see cref="IModelExporter"/> that writes the model to a file.
        /// </summary>
        private readonly IModelExporter modelExporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportTools"/> class.
        /// </summary>
        /// <param name="modelProvider">The <see cref="IModelProvider"/> that gives access to the model.</param>
        /// <param name="modelExporter">The <see cref="IModelExporter"/> that writes the model to a file.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="modelProvider"/> or <paramref name="modelExporter"/> is <c>null</c>.
        /// </exception>
        public ExportTools(IModelProvider modelProvider, IModelExporter modelExporter)
        {
            ArgumentNullException.ThrowIfNull(modelProvider);
            ArgumentNullException.ThrowIfNull(modelExporter);

            this.modelProvider = modelProvider;
            this.modelExporter = modelExporter;
        }

        /// <summary>
        /// Writes the whole model to a new JSON file of the export folder.
        /// </summary>
        /// <param name="fileName">The simple name of the file, or <c>null</c> for a name made of the current date and time.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> of the request, canceled when the client cancels it. It is not a parameter of the
        /// tool.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> whose result is the <see cref="ExportResult"/> that gives the path of the file, its format and
        /// the number of exported elements.
        /// </returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="fileName"/> is not a simple name, or when the export folder already has a file with
        /// this name.
        /// </exception>
        [McpServerTool(Name = "export_model", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
        [Description("Writes the whole model to a new JSON file of the export folder, in the format that the server loads, so that it can be loaded again or shared. "
            + "The model is not modified, and an existing file is never replaced.")]
        [return: Description("The full path of the written file, its format and the number of exported elements.")]
        public async Task<ExportResult> ExportModelAsync(
            [Description("Optional: a simple file name without folder (letters, digits, '-', '_' and '.'), to which '.json' is added. By default: model-<UTC date and time>.json.")] string fileName = null,
            CancellationToken cancellationToken = default)
        {
            var elements = this.modelProvider.ElementDtos;
            var path = await this.modelExporter.ExportAsync(elements, fileName, cancellationToken);

            return new ExportResult(path, this.modelExporter.Format, elements.Count);
        }
    }
}
