// ------------------------------------------------------------------------------------------------
//  <copyright file="KeycloakAdministration.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    /// <summary>
    /// Puts Keycloak in the state a flow test needs, through the admin REST API of the development instance.
    /// </summary>
    internal static class KeycloakAdministration
    {
        /// <summary>
        /// The password of every user the tests create.
        /// </summary>
        public const string Password = "Mycelium-2026";

        /// <summary>
        /// The password the tests choose when Keycloak asks for a new one.
        /// </summary>
        public const string NewPassword = "Mycelium-2027";

        /// <summary>
        /// The client of the Keycloak admin REST API, shared by every test.
        /// </summary>
        private static readonly HttpClient Client = new() { BaseAddress = MyceliumRealm.KeycloakUrl };

        /// <summary>
        /// The admin access token in use, reused while it is fresh.
        /// </summary>
        private static string accessToken;

        /// <summary>
        /// When the admin access token stops being reused, a little before Keycloak expires it.
        /// </summary>
        private static DateTime accessTokenExpiry;

        /// <summary>
        /// Creates an enabled user with a unique name in the Mycelium realm.
        /// </summary>
        /// <param name="requiredActions">The required actions Keycloak asks for at the next sign-in, none by default.</param>
        /// <param name="temporaryPassword">Whether the password must be changed at the next sign-in.</param>
        /// <param name="emailVerified">Whether the email address counts as verified.</param>
        /// <returns>The user name; the email address is <see cref="EmailOf" />.</returns>
        public static async Task<string> CreateUser(IReadOnlyList<string> requiredActions = null, bool temporaryPassword = false, bool emailVerified = true)
        {
            var userName = NewUserName();

            var user = new JsonObject
            {
                ["username"] = userName,
                ["email"] = EmailOf(userName),
                ["firstName"] = "Test",
                ["lastName"] = "User",
                ["enabled"] = true,
                ["emailVerified"] = emailVerified,
                ["requiredActions"] = new JsonArray([.. (requiredActions ?? []).Select(action => (JsonNode)action)]),
                ["credentials"] = new JsonArray(new JsonObject { ["type"] = "password", ["value"] = Password, ["temporary"] = temporaryPassword }),
            };

            using var request = await Authorised(HttpMethod.Post, $"/admin/realms/{MyceliumRealm.Name}/users");
            request.Content = JsonContent.Create(user);
            using var response = await Client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), $"create {userName}");

            return userName;
        }

        /// <summary>
        /// Fills in the sign-in form.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <param name="password">The password, <see cref="Password" /> by default.</param>
        /// <returns>The fields of <c>#kc-form-login</c>.</returns>
        public static Dictionary<string, string> Credentials(string userName, string password = Password) =>
            new() { ["username"] = userName, ["password"] = password };

        /// <summary>
        /// Fills in a choose-a-new-password form with <see cref="NewPassword" />.
        /// </summary>
        /// <returns>The fields of <c>#kc-passwd-update-form</c>.</returns>
        public static Dictionary<string, string> NewPasswordFields()
        {
            return new() { ["password-new"] = NewPassword, ["password-confirm"] = NewPassword };
        }

        /// <summary>
        /// Gets the email address of a user the tests created.
        /// </summary>
        /// <param name="userName">The user name.</param>
        /// <returns>An address in the reserved <c>example.org</c> domain.</returns>
        public static string EmailOf(string userName)
        {
            return $"{userName}@example.org";
        }

        /// <summary>
        /// Enables a required action that Keycloak ships disabled (terms and conditions).
        /// </summary>
        /// <param name="alias">The alias of the required action, for example <c>TERMS_AND_CONDITIONS</c>.</param>
        /// <returns>A task that completes once the action is enabled.</returns>
        public static async Task EnableRequiredAction(string alias)
        {
            var path = $"/admin/realms/{MyceliumRealm.Name}/authentication/required-actions/{alias}";

            using var get = await Authorised(HttpMethod.Get, path);
            using var current = await Client.SendAsync(get);
            current.EnsureSuccessStatusCode();
            var action = (await current.Content.ReadFromJsonAsync<JsonNode>());

            if (action["enabled"].GetValue<bool>())
            {
                return;
            }

            action["enabled"] = true;

            using var put = await Authorised(HttpMethod.Put, path);
            put.Content = JsonContent.Create(action);
            using var updated = await Client.SendAsync(put);
            updated.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Makes a user name no other test run uses.
        /// </summary>
        /// <returns>A short, random user name.</returns>
        public static string NewUserName()
        {
            return $"test-{Guid.NewGuid():N}"[..13];
        }

        /// <summary>
        /// Gets an admin access token. Keycloak's lives 60 s: one is reused while it is fresh rather than one per request.
        /// </summary>
        /// <returns>The bearer token.</returns>
        private static async Task<string> AccessToken()
        {
            if (accessToken is not null && DateTime.UtcNow < accessTokenExpiry)
            {
                return accessToken;
            }

            var grant = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_USER") ?? "admin",
                ["password"] = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD") ?? "admin",
            };

            using var response = await Client.PostAsync("/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(grant));
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            accessToken = json.GetProperty("access_token").GetString();
            accessTokenExpiry = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32() - 10);

            return accessToken;
        }

        /// <summary>
        /// Builds an admin API request that carries the bearer token.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">The path under the Keycloak base address.</param>
        /// <returns>The request, which the caller disposes.</returns>
        private static async Task<HttpRequestMessage> Authorised(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessToken());

            return request;
        }
    }
}
