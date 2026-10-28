// ------------------------------------------------------------------------------------------------
//  <copyright file="SignInTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers;

    /// <summary>
    /// Password sign-in, registration and sign out, end to end against Keycloak.
    /// </summary>
    [TestFixture]
    public class SignInTestFixture : KeycloakServerTestFixture
    {
        [Test]
        public async Task VerifyPasswordSignIn()
        {
            var userName = await KeycloakAdministration.CreateUser();
            var browser = new WebBrowser();
            var page = await browser.StartSignIn();

            var rejected = await browser.Submit(page, "#kc-form-login", KeycloakAdministration.Credentials(userName, "not-the-password"));
            var accepted = await browser.SubmitUntilApplication(rejected, "#kc-form-login", KeycloakAdministration.Credentials(userName));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(rejected.Text("#input-error-password"), Is.EqualTo("Invalid username or password."));
                Assert.That(rejected.Document.QuerySelector(".pf-v5-c-form-control.pf-m-error"), Is.Not.Null, "field in error state");
                Assert.That(rejected.Text("#kc-form-login a"), Is.EqualTo("Forgot password?"));
                Assert.That(accepted, Does.Contain("code="));
            }
        }

        [Test]
        public async Task VerifyRegistration()
        {
            var browser = new WebBrowser();
            var page = await browser.StartSignIn();
            var registration = await browser.Open(page.Attribute("#kc-registration a", "href"));
            var userName = KeycloakAdministration.NewUserName();

            var fields = new Dictionary<string, string>
            {
                ["username"] = userName,
                ["password"] = KeycloakAdministration.Password,
                ["password-confirm"] = KeycloakAdministration.Password,
                ["email"] = KeycloakAdministration.EmailOf(userName),
                ["firstName"] = "Test",
                ["lastName"] = "User",
            };

            var accepted = await browser.SubmitUntilApplication(registration, "#kc-register-form", fields);

            ThemeAssert.IsThemed(registration);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Text("#kc-registration a"), Is.EqualTo("Create account"));
                Assert.That(registration.Title, Is.EqualTo("Create your account"));
                Assert.That(accepted, Does.Contain("code="));
            }
        }

        [Test]
        public async Task VerifySignOut()
        {
            var browser = new WebBrowser();
            var page = await browser.Open($"/realms/{MyceliumRealm.Name}/protocol/openid-connect/logout");

            ThemeAssert.IsThemed(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Title, Is.EqualTo("Signing out"));
                Assert.That(page.Text(".instruction"), Is.EqualTo("Do you want to sign out?"));
                Assert.That(page.Attribute("#kc-logout", "value"), Is.EqualTo("Sign out"));
            }
        }
    }
}
