// ------------------------------------------------------------------------------------------------
//  <copyright file="MyceliumRealm.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;

    /// <summary>
    /// The development realm of docker/keycloak/mycelium-realm.json and the services the tests reach.
    /// </summary>
    internal static class MyceliumRealm
    {
        /// <summary>
        /// The realm Bloom signs in with.
        /// </summary>
        public const string Name = "mycelium";

        /// <summary>
        /// The brand of the sign-in pages and emails.
        /// </summary>
        public const string Brand = "Mycelium";

        /// <summary>
        /// The OIDC client of Bloom.
        /// </summary>
        public const string ClientId = "bloom-web";

        /// <summary>
        /// The name of the Bloom client, shown in "to continue to".
        /// </summary>
        public const string ClientName = "Mycelium Bloom";

        /// <summary>
        /// Where Keycloak hands the authorization code back to Bloom.
        /// </summary>
        public const string RedirectUri = "http://localhost:5209/";

        /// <summary>
        /// Gets the base address of the Keycloak under test, <c>KEYCLOAK_URL</c> or the development default.
        /// </summary>
        public static Uri KeycloakUrl { get; } = new(Environment.GetEnvironmentVariable("KEYCLOAK_URL") ?? "http://localhost:8081");

        /// <summary>
        /// Gets the base address of the Mailpit that catches Keycloak's email, <c>MAILPIT_URL</c> or the development default.
        /// </summary>
        public static Uri MailpitUrl { get; } = new(Environment.GetEnvironmentVariable("MAILPIT_URL") ?? "http://localhost:8025");
    }
}
