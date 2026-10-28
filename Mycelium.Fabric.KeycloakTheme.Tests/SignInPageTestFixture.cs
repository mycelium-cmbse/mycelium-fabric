// ------------------------------------------------------------------------------------------------
//  <copyright file="SignInPageTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests
{
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers;

    /// <summary>
    /// The sign-in page of both products, as Keycloak renders it with the Mycelium themes.
    /// </summary>
    [TestFixture]
    public class SignInPageTestFixture : KeycloakServerTestFixture
    {
        private static readonly string[] ProviderAliases = ["corporate-sso", "partner-oidc", "microsoft", "github"];

        private static readonly string[] ProviderProtocols = ["SAML 2.0", "OIDC", "OIDC · login.microsoftonline.com", "OAuth · github.com"];

        private static readonly string[] LetterGlyphs = ["C", "P"];

        private static readonly string[] FooterItems = ["Documentation", "·", "Support", "·", ThemeFiles.Property("mycelium/login/theme.properties", "myceliumVersion")];

        [Test]
        public async Task VerifyIdentityProviders()
        {
            var browser = new WebBrowser();
            var page = await browser.StartSignIn();
            var rows = page.Document.QuerySelectorAll(".mycelium-provider").ToList();
            var lastUsedBadgesHidden = rows.Select(row => row.QuerySelector(".mycelium-provider__last").HasAttribute("hidden"));
            using var redirect = await browser.Get(page.Attribute("#social-partner-oidc", "href"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Text(".mycelium-sso__intro"), Is.EqualTo("Choose your identity provider to continue."));
                Assert.That(rows.Select(row => row.GetAttribute("data-alias")), Is.EqualTo(ProviderAliases));
                Assert.That(rows.Select(row => WebPage.Normalise(row.QuerySelector(".mycelium-provider__meta").TextContent)), Is.EqualTo(ProviderProtocols));
                Assert.That(rows.Take(2).Select(row => WebPage.Normalise(row.QuerySelector(".mycelium-provider__glyph").TextContent)), Is.EqualTo(LetterGlyphs));
                Assert.That(lastUsedBadgesHidden, Has.All.True, "'Last used' waits for the browser to remember one");
                Assert.That(page.Text(".mycelium-divider"), Is.EqualTo("or"));
                Assert.That((int)redirect.StatusCode, Is.InRange(300, 399));
                Assert.That(redirect.Headers.Location.ToString(), Does.StartWith("https://login.partner.example.org/oauth2/authorize"));
            }
        }

        [Test]
        public async Task VerifySignInPage()
        {
            var browser = new WebBrowser();
            var page = await browser.StartSignIn();
            var mark = page.Document.QuerySelector("svg.mycelium-brand__mark");
            var favicon = page.Attribute("link[rel=icon]", "href");
            var lastStylesheets = page.Stylesheets.TakeLast(3).Select(href => href[(href.LastIndexOf('/') + 1)..]);

            ThemeAssert.IsThemed(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Document.Title, Is.EqualTo($"Sign in to {MyceliumRealm.Brand}"));
                Assert.That(page.Title, Is.EqualTo($"Sign in to {MyceliumRealm.Brand}"));
                Assert.That(page.Text(".mycelium-continue"), Is.EqualTo($"to continue to {MyceliumRealm.ClientName}"), "the application asking for the sign-in");
                Assert.That(page.Document.Body.ClassName, Is.EqualTo("mycelium"));
                Assert.That(lastStylesheets, Is.EqualTo((string[])["styles.css", "tokens-bloom.css", "mycelium.css"]), "keycloak.v2, then the tokens, then the Mycelium layer");
                Assert.That(mark.GetAttribute("viewBox"), Is.EqualTo("0 0 204 189"));
                Assert.That(mark.GetAttribute("fill"), Is.EqualTo("currentColor"));
                Assert.That(page.Document.DocumentElement.OuterHtml, Does.Not.Contain("bloom-mark").And.Not.Contain("logo-bloom"));
                Assert.That(favicon, Does.EndWith("favicon.svg"));
                Assert.That(page.Document.QuerySelectorAll(".mycelium-footer > *").Select(item => WebPage.Normalise(item.TextContent)), Is.EqualTo(FooterItems));
            }
        }

        [Test]
        public async Task VerifyThemeResources()
        {
            var browser = new WebBrowser();
            var page = await browser.StartSignIn();

            var resources = page.Stylesheets
                .Concat(page.Document.QuerySelectorAll("script[src]").Select(script => script.GetAttribute("src")))
                .Where(address => address.Contains("/login/mycelium/"))
                .Append(page.Attribute("link[rel=icon]", "href"))
                .Append(page.Attribute("link[rel=preload][as=font]", "href"))
                .ToList();

            Assert.That(resources, Has.Count.GreaterThanOrEqualTo(6));

            using (Assert.EnterMultipleScope())
            {
                foreach (var resource in resources)
                {
                    using var response = await browser.Get(resource);
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), resource);
                }
            }
        }
    }
}
