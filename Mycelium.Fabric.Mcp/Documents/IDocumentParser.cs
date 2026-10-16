// ------------------------------------------------------------------------------------------------
//  <copyright file="IDocumentParser.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Documents
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Splits the content of a document of one format into <see cref="DocumentSection"/>s. Every format gives the same shape:
    /// sections at the headings, lines of text, and a table row as one line <c>| cell | cell |</c>.
    /// </summary>
    public interface IDocumentParser
    {
        /// <summary>
        /// Gets the name of the format, for the messages sent to the AI assistant, for example <c>CSV</c>.
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// Gets the extensions of the files of this format, with their dot, for example <c>.csv</c>.
        /// </summary>
        IReadOnlyCollection<string> Extensions { get; }

        /// <summary>
        /// Splits the content of a document into sections.
        /// </summary>
        /// <param name="content">The content of the file, a readable and seekable <see cref="Stream"/>.</param>
        /// <returns>The sections of the document, numbered from 1 in order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidDataException">Thrown when the content is not a document of this format.</exception>
        IReadOnlyList<DocumentSection> Parse(Stream content);
    }
}
