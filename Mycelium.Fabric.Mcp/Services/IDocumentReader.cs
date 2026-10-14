// ------------------------------------------------------------------------------------------------
//  <copyright file="IDocumentReader.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ModelContextProtocol;

    using Mycelium.Fabric.Mcp.Documents;

    /// <summary>
    /// Reads the text documents of a folder, such as a specification whose requirements are to be modelled, and splits them
    /// into sections so that a long document can be read one piece at a time.
    /// </summary>
    public interface IDocumentReader
    {
        /// <summary>
        /// Gets the <see cref="Uri"/> of the folder from which the documents are read.
        /// </summary>
        Uri DocumentDirectory { get; }

        /// <summary>
        /// Reads a document of the document folder and splits it into sections.
        /// </summary>
        /// <param name="fileName">The simple name of the file, without folder.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the reading.</param>
        /// <returns>A <see cref="Task"/> whose result is the <see cref="TextDocument"/> that was read.</returns>
        /// <exception cref="McpException">
        /// Thrown when <paramref name="fileName"/> is not a simple name, is not a supported document, or does not exist in the
        /// document folder. The message is sent back to the AI assistant.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancellationToken"/> is canceled.
        /// </exception>
        Task<TextDocument> ReadAsync(string fileName, CancellationToken cancellationToken);
    }
}
