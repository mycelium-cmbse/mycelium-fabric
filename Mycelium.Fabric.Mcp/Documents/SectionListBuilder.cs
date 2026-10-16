// ------------------------------------------------------------------------------------------------
//  <copyright file="SectionListBuilder.cs" company="Starion Group S.A.">
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

    /// <summary>
    /// Builds the <see cref="DocumentSection"/>s of a document line by line, in the same way for every format: the lines
    /// added before the first heading form a section of level 0, and every heading starts a new section.
    /// </summary>
    public sealed class SectionListBuilder
    {
        /// <summary>
        /// The highest level of a heading.
        /// </summary>
        private const int MaximumLevel = 6;

        /// <summary>
        /// The sections built so far.
        /// </summary>
        private readonly List<DocumentSection> sections = [];

        /// <summary>
        /// The lines of the current section.
        /// </summary>
        private List<string> lines = [];

        /// <summary>
        /// The level of the heading of the current section, or 0 before the first heading.
        /// </summary>
        private int level;

        /// <summary>
        /// The title of the current section.
        /// </summary>
        private string title = string.Empty;

        /// <summary>
        /// Ends the current section and starts a new one at a heading.
        /// </summary>
        /// <param name="headingLevel">The level of the heading, from 1 to 6.</param>
        /// <param name="headingTitle">The text of the heading.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="headingLevel"/> is not between 1 and 6.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="headingTitle"/> is <c>null</c>.
        /// </exception>
        public void StartSection(int headingLevel, string headingTitle)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(headingLevel, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(headingLevel, MaximumLevel);
            ArgumentNullException.ThrowIfNull(headingTitle);

            this.AddCurrentSection();

            this.level = headingLevel;
            this.title = headingTitle.Trim();
            this.lines = [];
        }

        /// <summary>
        /// Adds a line of text to the current section.
        /// </summary>
        /// <param name="line">The line, without line break.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="line"/> is <c>null</c>.</exception>
        public void AddLine(string line)
        {
            ArgumentNullException.ThrowIfNull(line);

            this.lines.Add(line);
        }

        /// <summary>
        /// Ends the current section and gets all the sections, numbered from 1 in order.
        /// </summary>
        /// <returns>The sections of the document.</returns>
        public IReadOnlyList<DocumentSection> Build()
        {
            this.AddCurrentSection();

            this.level = 0;
            this.title = string.Empty;
            this.lines = [];

            return [.. this.sections];
        }

        /// <summary>
        /// Adds the current section to the sections, without its leading and trailing blank lines. Text before the first
        /// heading is only added when it is not blank.
        /// </summary>
        private void AddCurrentSection()
        {
            var firstLine = this.lines.FindIndex(line => !string.IsNullOrWhiteSpace(line));

            if (firstLine < 0 && this.level == 0)
            {
                return;
            }

            List<string> trimmedLines = firstLine < 0 ? [] : this.lines[firstLine..(this.lines.FindLastIndex(line => !string.IsNullOrWhiteSpace(line)) + 1)];

            this.sections.Add(new DocumentSection(this.sections.Count + 1, this.level, this.title, trimmedLines));
        }
    }
}
