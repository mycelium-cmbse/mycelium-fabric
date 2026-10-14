// ------------------------------------------------------------------------------------------------
//  <copyright file="DocumentSection.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Documents
{
    using System.Collections.Generic;

    /// <summary>
    /// A section of a <see cref="TextDocument"/>: a heading and the lines of text that follow it, up to the next heading.
    /// </summary>
    /// <param name="Number">The position of the section in the document, from 1.</param>
    /// <param name="Level">
    /// The level of the heading, from 1 for <c>#</c> to 6 for <c>######</c>, or 0 for the text before the first heading.
    /// </param>
    /// <param name="Title">The text of the heading, or an empty text for the text before the first heading.</param>
    /// <param name="Lines">
    /// The lines of text of the section, without its heading, its subsections and its leading and trailing blank lines.
    /// </param>
    public sealed record DocumentSection(int Number, int Level, string Title, IReadOnlyList<string> Lines);
}
