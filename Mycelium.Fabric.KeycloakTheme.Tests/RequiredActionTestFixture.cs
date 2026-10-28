// ------------------------------------------------------------------------------------------------
//  <copyright file="RequiredActionTestFixture.cs" company="Starion Group S.A.">
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
    /// The pages Keycloak puts between the password and the application: each one themed, each one completed.
    /// </summary>
    [TestFixture]
    public class RequiredActionTestFixture : KeycloakServerTestFixture
    {
        [Test]
        public async Task VerifyAuthenticatorSetup()
        {
            var userName = await KeycloakAdministration.CreateUser(["CONFIGURE_TOTP"]);
            var browser = new WebBrowser();
            var setup = await SignInAs(browser, userName);

            var manual = await browser.Open(setup.Attribute("#mode-manual", "href"));
            var secret = manual.Text("#kc-totp-secret-key");
            var device = new Dictionary<string, string> { ["totp"] = OneTimePassword.Compute(secret, 0), ["userLabel"] = "Test device" };
            var configured = await browser.SubmitUntilApplication(manual, "#kc-totp-settings-form", device);

            var secondBrowser = new WebBrowser();
            var challenge = await SignInAs(secondBrowser, userName);

            // The setup code cannot be replayed; the next period's code is inside Keycloak's look-around window.
            var code = new Dictionary<string, string> { ["otp"] = OneTimePassword.Compute(secret, 1) };
            var signedIn = await secondBrowser.SubmitUntilApplication(challenge, "#kc-otp-login-form", code);

            ThemeAssert.IsThemed(setup);
            ThemeAssert.IsThemed(challenge);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(setup.Title, Is.EqualTo("Set up your authenticator app"));
                Assert.That(setup.Text("#kc-totp-settings"), Does.Contain("Continue").And.Not.Contain("Submit"), "instructions name the button the page shows");
                Assert.That(setup.Text(".pf-v5-c-alert"), Is.EqualTo("Set up an authenticator app to activate your account."));
                Assert.That(configured, Does.Contain("code="));
                Assert.That(challenge.Title, Is.EqualTo("Enter the code from your authenticator app"));
                Assert.That(signedIn, Does.Contain("code="));
            }
        }

        [Test]
        public async Task VerifyEmailVerification()
        {
            var userName = await KeycloakAdministration.CreateUser(["VERIFY_EMAIL"], emailVerified: false);
            var browser = new WebBrowser();
            var page = await SignInAs(browser, userName);

            var email = await Mailpit.WaitFor(KeycloakAdministration.EmailOf(userName));
            var signedIn = await browser.OpenUntilApplication(email.Link("/login-actions/action-token"));

            ThemeAssert.IsThemed(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Title, Is.EqualTo("Verify your email"));
                Assert.That(signedIn, Does.Contain("code="));
            }
        }

        [Test]
        public async Task VerifyProfileCompletion()
        {
            var browser = new WebBrowser();
            var page = await SignInAs(browser, await KeycloakAdministration.CreateUser(["UPDATE_PROFILE"]));

            var profile = new Dictionary<string, string> { ["firstName"] = "Updated", ["lastName"] = "Name" };
            var signedIn = await browser.SubmitUntilApplication(page, "#kc-update-profile-form", profile);

            ThemeAssert.IsThemed(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Title, Is.EqualTo("Complete your profile"));
                Assert.That(signedIn, Does.Contain("code="));
            }
        }

        [Test]
        public async Task VerifyTemporaryPassword()
        {
            var browser = new WebBrowser();
            var page = await SignInAs(browser, await KeycloakAdministration.CreateUser(temporaryPassword: true));

            var signedIn = await browser.SubmitUntilApplication(page, "#kc-passwd-update-form", KeycloakAdministration.NewPasswordFields());

            ThemeAssert.IsThemed(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Title, Is.EqualTo("Choose a new password"));
                Assert.That(signedIn, Does.Contain("code="));
            }
        }

        [Test]
        public async Task VerifyTermsAcceptance()
        {
            await KeycloakAdministration.EnableRequiredAction("TERMS_AND_CONDITIONS");
            var browser = new WebBrowser();
            var page = await SignInAs(browser, await KeycloakAdministration.CreateUser(["TERMS_AND_CONDITIONS"]));

            ThemeAssert.IsThemed(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Title, Is.EqualTo("Terms and conditions"));
                Assert.That(page.Attribute("#kc-accept", "class"), Does.Contain("pf-m-primary"));
                Assert.That(await browser.SubmitUntilApplication(page, "form", new Dictionary<string, string> { ["accept"] = string.Empty }), Does.Contain("code="));
            }
        }

        private static async Task<WebPage> SignInAs(WebBrowser browser, string userName)
        {
            return await browser.Submit(await browser.StartSignIn(), "#kc-form-login", KeycloakAdministration.Credentials(userName));
        }
    }
}
