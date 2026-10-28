// ------------------------------------------------------------------------------------------------
//  <copyright file="Mailpit.cs" company="Starion Group S.A.">
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
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads the email Keycloak sends, from the Mailpit of the compose file.
    /// </summary>
    internal static class Mailpit
    {
        /// <summary>
        /// The client of the Mailpit REST API, shared by every test.
        /// </summary>
        private static readonly HttpClient Client = new() { BaseAddress = MyceliumRealm.MailpitUrl };

        /// <summary>
        /// How long an email may take to arrive before the test fails.
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The pause between two searches of the mailbox.
        /// </summary>
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Waits for the first email sent to an address.
        /// </summary>
        /// <param name="recipient">The address the email is sent to.</param>
        /// <returns>The email, with its subject, HTML and plain-text parts.</returns>
        /// <exception cref="AssertionException">When none arrives in time.</exception>
        public static async Task<MailMessage> WaitFor(string recipient)
        {
            var deadline = DateTime.UtcNow + Timeout;

            while (DateTime.UtcNow < deadline)
            {
                var search = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/search?query={Uri.EscapeDataString($"to:\"{recipient}\"")}");
                var messages = search.GetProperty("messages");

                if (messages.GetArrayLength() > 0)
                {
                    // Mailpit names the parts Subject, HTML and Text: the web defaults bind them case-insensitively.
                    return (await Client.GetFromJsonAsync<MailMessage>($"/api/v1/message/{messages[0].GetProperty("ID").GetString()}"));
                }

                await Task.Delay(PollInterval);
            }

            throw new AssertionException($"no email to {recipient} within {Timeout.TotalSeconds} s");
        }
    }
}
