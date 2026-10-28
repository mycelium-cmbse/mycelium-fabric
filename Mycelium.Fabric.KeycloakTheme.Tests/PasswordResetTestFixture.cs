// ------------------------------------------------------------------------------------------------
//  <copyright file="PasswordResetTestFixture.cs" company="Starion Group S.A.">
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
    /// Forgot password, for both products: the page, the email and its branding (the common Mycelium one), the link, the new password.
    /// </summary>
    [TestFixture]
    public class PasswordResetTestFixture : KeycloakServerTestFixture
    {
        [Test]
        public async Task VerifyPasswordReset()
        {
            var userName = await KeycloakAdministration.CreateUser();
            var browser = new WebBrowser();
            var signIn = await browser.StartSignIn();
            var request = await browser.Open(signIn.Attribute("#kc-form-login a", "href"));
            var sent = await browser.Submit(request, "#kc-reset-password-form", new Dictionary<string, string> { ["username"] = userName });

            var email = await Mailpit.WaitFor(KeycloakAdministration.EmailOf(userName));
            var update = await browser.Open(email.Link("/login-actions/action-token"));
            var signedIn = await browser.SubmitUntilApplication(update, "#kc-passwd-update-form", KeycloakAdministration.NewPasswordFields());

            ThemeAssert.IsThemed(request);
            ThemeAssert.IsThemed(sent);
            ThemeAssert.IsThemed(update);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(request.Title, Is.EqualTo("Reset your password"));
                Assert.That(request.Text("#kc-reset-password-form [type=submit]"), Is.EqualTo("Continue"));
                Assert.That(request.Document.QuerySelector("#kc-info-wrapper"), Is.Not.Null, "instruction (moved under the title by the CSS)");
                Assert.That(email.Subject, Is.EqualTo($"Reset your {MyceliumRealm.Brand} password"));
                Assert.That(email.Html, Does.Contain(MyceliumRealm.Brand));
                Assert.That(email.Html, Does.Contain(ThemeFiles.Property("mycelium/email/theme.properties", "myceliumColourPrimary")).IgnoreCase, "the Bloom accent");
                Assert.That(email.Html, Does.Contain("viewBox=\"0 0 204 189\""), "network mark");
                Assert.That(update.Title, Is.EqualTo("Choose a new password"));
                Assert.That(signedIn, Does.Contain("code="));
            }
        }
    }
}
