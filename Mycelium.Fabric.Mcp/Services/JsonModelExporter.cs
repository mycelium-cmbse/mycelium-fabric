// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonModelExporter.cs" company="Starion Group S.A.">
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
    using System.Globalization;
    using System.IO;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Serializer.Json;

    /// <summary>
    /// The <see cref="IModelExporter"/> that writes a model to a JSON file of the Systems Modeling API, the format that the
    /// <see cref="InMemoryModelProvider"/> loads.
    /// </summary>
    /// <remarks>
    /// The DTOs of the model are written as they are by the <see cref="ISerializer"/> of SysML2.NET.Serializer.Json, without
    /// their derived properties, as in the files that the <see cref="InMemoryModelProvider"/> loads.
    /// </remarks>
    public partial class JsonModelExporter : IModelExporter
    {
        /// <summary>
        /// The extension of the written files.
        /// </summary>
        private const string FileExtension = ".json";

        /// <summary>
        /// The size, in bytes, of the buffer of the written file.
        /// </summary>
        private const int BufferSize = 4096;

        /// <summary>
        /// The options of the JSON writer, shared by every export: indented, so that the written files can be read.
        /// </summary>
        private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

        /// <summary>
        /// The <see cref="ISerializer"/> that writes the DTOs to JSON.
        /// </summary>
        private readonly ISerializer serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonModelExporter"/> class.
        /// </summary>
        /// <param name="serializer">The <see cref="ISerializer"/> that writes the DTOs to JSON.</param>
        /// <param name="exportDirectory">The <see cref="Uri"/> of the folder in which the files are written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serializer"/> or <paramref name="exportDirectory"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="exportDirectory"/> is not an absolute file <see cref="Uri"/>.
        /// </exception>
        public JsonModelExporter(ISerializer serializer, Uri exportDirectory)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(exportDirectory);

            if (!exportDirectory.IsAbsoluteUri || !exportDirectory.IsFile)
            {
                throw new ArgumentException("The export directory must be an absolute file URI.", nameof(exportDirectory));
            }

            this.serializer = serializer;
            this.ExportDirectory = exportDirectory;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the folder in which the files are written.
        /// </summary>
        public Uri ExportDirectory { get; }

        /// <summary>
        /// Gets the format of the files that this exporter writes: <see cref="ModelExportFormat.Json"/>.
        /// </summary>
        public ModelExportFormat Format => ModelExportFormat.Json;

        /// <summary>
        /// Writes the DTOs of the given elements to a new JSON file of the export folder, which is created when it does not
        /// exist. An existing file is never replaced, and a canceled export leaves no file.
        /// </summary>
        /// <param name="elements">The DTOs of the elements of the model to write.</param>
        /// <param name="fileName">
        /// The simple name of the file, without folder, to which <c>.json</c> is added when it is missing, or <c>null</c>
        /// for <c>model-</c> followed by the current UTC date and time.
        /// </param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the export.</param>
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
        public async Task<string> ExportAsync(IReadOnlyCollection<IElement> elements, string fileName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(elements);

            var path = Path.Combine(this.ExportDirectory.LocalPath, GetFileName(fileName));

            if (File.Exists(path))
            {
                throw new McpException($"The export folder already has a file named '{Path.GetFileName(path)}': choose another name.");
            }

            Directory.CreateDirectory(this.ExportDirectory.LocalPath);

            try
            {
                await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous);
                await this.serializer.SerializeAsync(elements, SerializationModeKind.JSON, false, stream, WriterOptions, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                File.Delete(path);
                throw;
            }

            return path;
        }

        /// <summary>
        /// Gets the name of the file to write: the given simple name, with the <c>.json</c> extension, or a name made of the
        /// current UTC date and time when no name is given.
        /// </summary>
        /// <param name="fileName">The simple name of the file, or <c>null</c>.</param>
        /// <returns>The name of the file, with its extension.</returns>
        /// <exception cref="McpException">Thrown when <paramref name="fileName"/> is not a simple name.</exception>
        private static string GetFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return $"model-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}{FileExtension}";
            }

            if (!SimpleFileName().IsMatch(fileName))
            {
                throw new McpException($"The file name '{fileName}' is not a simple name: use at most 100 letters, digits, '-', '_' and '.', without folder, not starting with '.'.");
            }

            return fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase) ? fileName : fileName + FileExtension;
        }

        /// <summary>
        /// Matches a simple file name: letters, digits, <c>-</c>, <c>_</c> and <c>.</c>, without folder separator, not
        /// starting with <c>.</c>, at most 100 characters.
        /// </summary>
        /// <returns>The <see cref="Regex"/> of a simple file name.</returns>
        [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,99}$", RegexOptions.None, 1000)]
        private static partial Regex SimpleFileName();
    }
}
