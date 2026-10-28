// ------------------------------------------------------------------------------------------------
//  <copyright file="MailMessage.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// An email Keycloak sent, as Mailpit caught it.
    /// </summary>
    /// <param name="Subject">The subject line.</param>
    /// <param name="Html">The HTML part.</param>
    /// <param name="Text">The plain-text part.</param>
    internal sealed partial record MailMessage(string Subject, string Html, string Text)
    {
        /// <summary>
        /// Finds the first link of the plain-text part whose address contains a fragment.
        /// </summary>
        /// <param name="fragment">A part of the address that identifies the link, for example <c>reset-credentials</c>.</param>
        /// <returns>The address of the link.</returns>
        /// <exception cref="AssertionException">When no link matches.</exception>
        public string Link(string fragment)
        {
            return LinkPattern().Matches(this.Text).Select(match => match.Value).FirstOrDefault(link => link.Contains(fragment, StringComparison.Ordinal))
            ?? throw new AssertionException($"no link containing {fragment} in '{this.Subject}'");
        }

        /// <summary>
        /// Matches an absolute http or https address in plain text.
        /// </summary>
        /// <returns>The compiled pattern.</returns>
        [GeneratedRegex("https?://[^\\s\"'<>]+")]
        private static partial Regex LinkPattern();
    }
}
