// ------------------------------------------------------------------------------------------------
//  <copyright file="ThemeAssert.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    /// <summary>
    /// What every page of a Mycelium login theme carries, whichever Keycloak page it is.
    /// </summary>
    internal static class ThemeAssert
    {
        /// <summary>
        /// Asserts the brand header, the Bloom token file, the Mycelium layer and the footer.
        /// </summary>
        /// <param name="page">The Keycloak page under test.</param>
        public static void IsThemed(WebPage page)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(page.Text(".mycelium-brand__name"), Is.EqualTo(MyceliumRealm.Brand));
                Assert.That(page.Document.QuerySelector("svg.mycelium-brand__mark"), Is.Not.Null, "network mark");
                Assert.That(page.Stylesheets, Has.Some.EndsWith("/css/tokens-bloom.css"));
                Assert.That(page.Stylesheets, Has.Some.EndsWith("/css/mycelium.css"));
                Assert.That(page.Document.QuerySelector(".mycelium-footer"), Is.Not.Null, "footer");
            }
        }
    }
}
