// ------------------------------------------------------------------------------------------------
//  <copyright file="WebPage.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using AngleSharp.Dom;

    /// <summary>
    /// A page Keycloak rendered, parsed.
    /// </summary>
    /// <param name="Address">The address the page was served from.</param>
    /// <param name="Document">The parsed document.</param>
    internal sealed record WebPage(Uri Address, IDocument Document)
    {
        /// <summary>
        /// Gets the addresses of the stylesheets, in document order.
        /// </summary>
        public IReadOnlyList<string> Stylesheets =>
            [.. this.Document.QuerySelectorAll("link[rel=stylesheet]").Select(link => link.GetAttribute("href") ?? string.Empty)];

        /// <summary>
        /// Gets the title of the card, the page's heading.
        /// </summary>
        public string Title => this.Text("#kc-page-title");

        /// <summary>
        /// Collapses runs of whitespace into single spaces and trims the result.
        /// </summary>
        /// <param name="text">The text to normalise.</param>
        /// <returns>The normalised text.</returns>
        public static string Normalise(string text)
        {
            return string.Join(' ', text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Reads an attribute of the first element matching a selector.
        /// </summary>
        /// <param name="selector">The CSS selector.</param>
        /// <param name="attribute">The attribute name.</param>
        /// <returns>The attribute value.</returns>
        /// <exception cref="AssertionException">When no element matches or the attribute is missing.</exception>
        public string Attribute(string selector, string attribute)
        {
            return this.Document.QuerySelector(selector)?.GetAttribute(attribute) ?? throw new AssertionException($"no {attribute} on {selector} in {this.Address}");
        }

        /// <summary>
        /// Reads the normalised text of the first element matching a selector.
        /// </summary>
        /// <param name="selector">The CSS selector.</param>
        /// <returns>The normalised text.</returns>
        /// <exception cref="AssertionException">When no element matches.</exception>
        public string Text(string selector)
        {
            return Normalise(this.Document.QuerySelector(selector)?.TextContent ?? throw new AssertionException($"no {selector} in {this.Address}"));
        }
    }
}
