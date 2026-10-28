// ------------------------------------------------------------------------------------------------
//  <copyright file="ErrorPageTestFixture.cs" company="Starion Group S.A.">
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
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web;

    using Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers;

    /// <summary>
    /// The error, identity-provider error and expired pages, and the way out of each.
    /// </summary>
    [TestFixture]
    public class ErrorPageTestFixture : KeycloakServerTestFixture
    {
        private static readonly string[] AlertUnderTitle = ["pf-v5-c-login__main-header", "pf-v5-c-alert pf-m-inline pf-v5-u-mb-md pf-m-danger"];

        [Test]
        public async Task VerifyErrorPage()
        {
            var browser = new WebBrowser();
            var beforeSignIn = await browser.Open(WebBrowser.SignInAddress("http://unregistered.example.org/"));

            // An unknown broker during a sign-in: Keycloak renders error.ftl without naming the authentication session.
            var signIn = await browser.StartSignIn();
            var duringSignIn = await browser.Open(signIn.Attribute("#social-github", "href").Replace("/broker/github/", "/broker/unknown-provider/"));

            ThemeAssert.IsThemed(beforeSignIn);
            ThemeAssert.IsThemed(duringSignIn);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(beforeSignIn.Text(".mycelium-error__title"), Is.EqualTo("Sign-in was not completed"));
                Assert.That(beforeSignIn.Document.QuerySelector(".mycelium-error__badge svg"), Is.Not.Null, "warning badge");
                AssertSingleWayOut(beforeSignIn);
                Assert.That(duringSignIn.Text(".mycelium-error__title"), Is.EqualTo("Sign-in was not completed"));
                AssertSingleWayOut(duringSignIn);
            }
        }

        [Test]
        public async Task VerifyExpiredPage()
        {
            // Back to the sign-in form after the password moved the session on to a required action (SessionCodeChecks).
            var userName = await KeycloakAdministration.CreateUser(temporaryPassword: true);
            var browser = new WebBrowser();
            var signIn = await browser.StartSignIn();
            await browser.Submit(signIn, "#kc-form-login", KeycloakAdministration.Credentials(userName));

            var expired = await browser.Open(signIn.Attribute("#kc-form-login", "action"));

            ThemeAssert.IsThemed(expired);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(expired.Title, Is.EqualTo("This page has expired"));
                Assert.That(expired.Document.QuerySelector("#loginContinueLink"), Is.Not.Null, "a way to carry on");
            }
        }

        [Test]
        public async Task VerifyIdentityProviderError()
        {
            // The provider sends the user back with an error: Keycloak shows the sign-in page again, with an alert.
            var browser = new WebBrowser();
            var signIn = await browser.StartSignIn();
            using var toProvider = await browser.Get(signIn.Attribute("#social-partner-oidc", "href"));
            var state = HttpUtility.ParseQueryString(toProvider.Headers.Location.Query)["state"];

            var page = await browser.Open($"/realms/{MyceliumRealm.Name}/broker/partner-oidc/endpoint?error=access_denied&state={Uri.EscapeDataString(state)}");
            var main = page.Document.QuerySelector("main");

            ThemeAssert.IsThemed(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Title, Is.EqualTo($"Sign in to {MyceliumRealm.Brand}"));
                Assert.That(page.Text(".pf-v5-c-alert.pf-m-danger"), Is.Not.Empty);
                Assert.That(main.Children.Select(child => child.ClassName).Take(2), Is.EqualTo(AlertUnderTitle), "the alert sits under the title, above the providers");
            }
        }

        private static void AssertSingleWayOut(WebPage page)
        {
            var actions = page.Document.QuerySelectorAll(".mycelium-actions a").ToList();
            var action = actions.FirstOrDefault();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(actions, Has.Count.EqualTo(1), "one way out");
                Assert.That(action?.Id, Is.EqualTo("tryAgain"));
                Assert.That(action?.GetAttribute("href"), Is.EqualTo(MyceliumRealm.RedirectUri));
                Assert.That(action?.ClassList.Contains("pf-m-primary"), Is.True, "the only action is primary");
            }
        }
    }
}
