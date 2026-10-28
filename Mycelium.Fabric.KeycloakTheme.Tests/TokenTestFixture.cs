// ------------------------------------------------------------------------------------------------
//  <copyright file="TokenTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers;

    /// <summary>
    /// The theme carries tokens only: the stylesheet uses var(--token), the email spells out the same values. Needs no Keycloak.
    /// </summary>
    [TestFixture]
    public partial class TokenTestFixture
    {
        private const string EmailColourPrefix = "myceliumColour";

        private static readonly string ThemeDirectory = Path.Combine(ThemeFiles.ThemesDirectory, "mycelium");

        private static readonly string CssDirectory = Path.Combine(ThemeDirectory, "login", "resources", "css");

        private static readonly string[] TokenFiles = ["tokens-bloom.css"];

        private static readonly string[] EmailColourTokens = ["background", "border", "card", "foreground", "muted-foreground", "primary", "primary-foreground"];

        [Test]
        public void VerifyEmailMatchesLoginTheme()
        {
            var loginMark = MarkPathPattern().Match(File.ReadAllText(Path.Combine(ThemeDirectory, "login", "mark.ftl"))).Groups[1].Value;
            var emailMark = MarkPathPattern().Match(File.ReadAllText(Path.Combine(ThemeDirectory, "email", "html", "mark.ftl"))).Groups[1].Value;

            // The email is the common Mycelium one, in the Bloom (Mycelium) light tokens.
            var tokens = LightTokens(File.ReadAllText(Path.Combine(CssDirectory, "tokens-bloom.css")));
            var colours = EmailColours();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(emailMark, Is.Not.Empty.And.EqualTo(loginMark), "email and login draw the same mark");
                Assert.That(colours.Keys, Is.EquivalentTo(EmailColourTokens), "every email colour is declared");

                foreach (var (token, value) in colours)
                {
                    Assert.That(value, Is.EqualTo(Resolve(tokens, token)).IgnoreCase, $"--{token}");
                }
            }
        }

        [Test]
        public void VerifyStylesheetUsesTokensOnly()
        {
            var stylesheet = CommentPattern().Replace(File.ReadAllText(Path.Combine(CssDirectory, "mycelium.css")), string.Empty);
            var lines = stylesheet.Split('\n');
            var used = TokenUsePattern().Matches(stylesheet).Select(match => match.Groups[1].Value).Distinct().ToList();
            var ownProperties = Declared(stylesheet);

            using (Assert.EnterMultipleScope())
            {
                foreach (var tokenFile in TokenFiles)
                {
                    var tokens = Declared(File.ReadAllText(Path.Combine(CssDirectory, tokenFile)));
                    var unknown = used.Where(token => !tokens.Contains(token) && !ownProperties.Contains(token) && !token.StartsWith("--pf-v5-", StringComparison.Ordinal));
                    Assert.That(unknown, Is.Empty, $"tokens missing from {tokenFile}");
                }

                Assert.That(lines.Where(line => LiteralColourPattern().IsMatch(line)), Is.Empty, "literal colours");
                Assert.That(lines.Where(line => LiteralSpacingPattern().IsMatch(line)), Is.Empty, "literal radius, padding, margin, gap or font size");
            }
        }

        [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
        private static partial Regex CommentPattern();

        private static HashSet<string> Declared(string css)
        {
            return [.. DeclarationPattern().Matches(css).Select(match => match.Groups[1].Value)];
        }

        [GeneratedRegex(@"(--[\w-]+)\s*:")]
        private static partial Regex DeclarationPattern();

        [GeneratedRegex(@"(--[\w-]+)\s*:\s*([^;]+);")]
        private static partial Regex DeclarationValuePattern();

        private static Dictionary<string, string> EmailColours()
        {
            return ThemeFiles.Properties("mycelium/email/theme.properties")
                .Where(property => property.Key.StartsWith(EmailColourPrefix, StringComparison.Ordinal))
                .ToDictionary(property => KebabPattern().Replace(property.Key[EmailColourPrefix.Length..], "-$1").ToLowerInvariant(), property => property.Value);
        }

        [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
        private static partial Regex KebabPattern();

        private static Dictionary<string, string> LightTokens(string css)
        {
            return DeclarationValuePattern().Matches(css[..css.IndexOf(".dark", StringComparison.Ordinal)])
                .GroupBy(match => match.Groups[1].Value)
                .ToDictionary(group => group.Key, group => group.First().Groups[2].Value.Trim());
        }

        [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(")]
        private static partial Regex LiteralColourPattern();

        // Widths, heights and border widths are not tokens, as in Figma; these properties are.
        [GeneratedRegex(@"^\s*(border-radius|padding[\w-]*|margin[\w-]*|gap|row-gap|column-gap|font-size)\s*:[^;]*(?<![\w-])[1-9]\d*(\.\d+)?px")]
        private static partial Regex LiteralSpacingPattern();

        [GeneratedRegex("<path d=\"([^\"]+)\"")]
        private static partial Regex MarkPathPattern();

        private static string Resolve(Dictionary<string, string> tokens, string token)
        {
            var value = tokens[$"--{token}"];

            while (VarReferencePattern().Match(value) is { Success: true } reference)
            {
                value = tokens[reference.Groups[1].Value];
            }

            return value;
        }

        [GeneratedRegex(@"var\((--[\w-]+)")]
        private static partial Regex TokenUsePattern();

        [GeneratedRegex(@"^var\((--[\w-]+)\)$")]
        private static partial Regex VarReferencePattern();
    }
}
