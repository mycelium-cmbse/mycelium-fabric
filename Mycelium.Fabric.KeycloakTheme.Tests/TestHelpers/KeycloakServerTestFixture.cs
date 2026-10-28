// ------------------------------------------------------------------------------------------------
//  <copyright file="KeycloakServerTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Base of the fixtures that drive a running Keycloak. The CI of this repository runs every test and starts no
    /// Keycloak, so these fixtures are ignored, with the reason, when the development realm does not answer.
    /// </summary>
    public abstract class KeycloakServerTestFixture
    {
        /// <summary>
        /// Whether the development realm answers, probed once for the whole test run.
        /// </summary>
        private static readonly Lazy<Task<bool>> RealmAvailable = new(IsRealmAvailable);

        /// <summary>
        /// Ignores the fixture when the development realm is not reachable.
        /// </summary>
        /// <returns>A task that completes once the realm answered, or after the fixture is marked ignored.</returns>
        [OneTimeSetUp]
        public async Task IgnoreWithoutKeycloak()
        {
            if (!await RealmAvailable.Value)
            {
                Assert.Ignore($"No Keycloak realm '{MyceliumRealm.Name}' at {MyceliumRealm.KeycloakUrl}: start the development Keycloak to run these tests.");
            }
        }

        /// <summary>
        /// Asks the development realm for its OpenID configuration.
        /// </summary>
        /// <returns><see langword="true" /> when the realm answers within five seconds.</returns>
        private static async Task<bool> IsRealmAvailable()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            try
            {
                using var response = await client.GetAsync(new Uri(MyceliumRealm.KeycloakUrl, $"/realms/{MyceliumRealm.Name}/.well-known/openid-configuration"));
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }
    }
}
