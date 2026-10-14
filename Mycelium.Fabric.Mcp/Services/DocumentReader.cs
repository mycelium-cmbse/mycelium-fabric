// ------------------------------------------------------------------------------------------------
//  <copyright file="DocumentReader.cs" company="Starion Group S.A.">
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
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Documents;

    /// <summary>
    /// The <see cref="IDocumentReader"/> that reads the documents of a folder with the <see cref="IDocumentParser"/> of their
    /// format, chosen by the extension of the file: Markdown and text, Word, Excel.
    /// </summary>
    public partial class DocumentReader : IDocumentReader
    {
        /// <summary>
        /// The maximum number of file names listed in the message sent when a document does not exist.
        /// </summary>
        private const int MaximumListedFileCount = 20;

        /// <summary>
        /// The <see cref="IDocumentParser"/>s of the supported formats, by extension.
        /// </summary>
        private readonly Dictionary<string, IDocumentParser> parsersByExtension;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentReader"/> class.
        /// </summary>
        /// <param name="documentDirectory">The <see cref="Uri"/> of the folder from which the documents are read.</param>
        /// <param name="parsers">The <see cref="IDocumentParser"/>s of the supported formats.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="documentDirectory"/> or <paramref name="parsers"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="documentDirectory"/> is not an absolute file <see cref="Uri"/>, or when two parsers
        /// read the same extension.
        /// </exception>
        public DocumentReader(Uri documentDirectory, IEnumerable<IDocumentParser> parsers)
        {
            ArgumentNullException.ThrowIfNull(documentDirectory);
            ArgumentNullException.ThrowIfNull(parsers);

            if (!documentDirectory.IsAbsoluteUri || !documentDirectory.IsFile)
            {
                throw new ArgumentException("The document directory must be an absolute file URI.", nameof(documentDirectory));
            }

            this.parsersByExtension = new Dictionary<string, IDocumentParser>(StringComparer.OrdinalIgnoreCase);

            foreach (var parser in parsers)
            {
                foreach (var extension in parser.Extensions)
                {
                    if (!this.parsersByExtension.TryAdd(extension, parser))
                    {
                        throw new ArgumentException($"Two parsers read the '{extension}' files.", nameof(parsers));
                    }
                }
            }

            this.DocumentDirectory = documentDirectory;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the folder from which the documents are read.
        /// </summary>
        public Uri DocumentDirectory { get; }

        /// <summary>
        /// Gets the extensions of the files that can be read, in alphabetical order.
        /// </summary>
        public IReadOnlyList<string> SupportedExtensions => [.. this.parsersByExtension.Keys.Order(StringComparer.OrdinalIgnoreCase)];

        /// <summary>
        /// Reads a document of the document folder with the parser of its format, and splits it into sections.
        /// </summary>
        /// <param name="fileName">The simple name of the file, without folder, with its extension.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the reading.</param>
        /// <returns>A <see cref="Task"/> whose result is the <see cref="TextDocument"/> that was read.</returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="fileName"/> is not a simple name, is not a supported document, does not exist in the
        /// document folder, or cannot be read in its format. The message is sent back to the AI assistant.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancellationToken"/> is canceled.
        /// </exception>
        public async Task<TextDocument> ReadAsync(string fileName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !SimpleFileName().IsMatch(fileName))
            {
                throw new McpException($"The file name '{fileName}' is not a simple name: use at most 100 letters, digits, '-', '_' and '.', without folder, not starting with '.'.");
            }

            if (!this.parsersByExtension.TryGetValue(Path.GetExtension(fileName), out var parser))
            {
                throw new McpException($"The file '{fileName}' is not a supported document ({string.Join(", ", this.SupportedExtensions)}): convert it to one of these formats first.");
            }

            var path = Path.Combine(this.DocumentDirectory.LocalPath, fileName);

            if (!File.Exists(path))
            {
                throw new McpException($"The document folder ({this.DocumentDirectory.LocalPath}) has no file named '{fileName}'. {this.DescribeDocuments()}");
            }

            using var content = new MemoryStream(await File.ReadAllBytesAsync(path, cancellationToken), false);

            try
            {
                return new TextDocument(fileName, parser.Parse(content));
            }
            catch (InvalidDataException exception)
            {
                throw new McpException($"The file '{fileName}' cannot be read as a {parser.FormatName} document: {exception.Message}");
            }
        }

        /// <summary>
        /// Describes the documents of the document folder that can be read, so that the AI assistant can correct a file name.
        /// </summary>
        /// <returns>A sentence that lists the first documents, in alphabetical order.</returns>
        private string DescribeDocuments()
        {
            var directory = this.DocumentDirectory.LocalPath;

            var fileNames = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory).Select(Path.GetFileName).Where(name => this.parsersByExtension.ContainsKey(Path.GetExtension(name)))
                    .Order(StringComparer.OrdinalIgnoreCase).Take(MaximumListedFileCount).ToList()
                : [];

            return fileNames.Count == 0
                ? "The folder has no supported document."
                : $"Its documents are: {string.Join(", ", fileNames)}.";
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
