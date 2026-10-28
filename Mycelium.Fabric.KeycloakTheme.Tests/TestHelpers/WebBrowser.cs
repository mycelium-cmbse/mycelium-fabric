// ------------------------------------------------------------------------------------------------
//  <copyright file="WebBrowser.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.KeycloakTheme.Tests.TestHelpers
{
    using System;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using AngleSharp.Html.Parser;

    /// <summary>
    /// Behaves like a browser for the sign-in flows: cookies per host, forms, redirects across Keycloak and the application.
    /// </summary>
    internal sealed class WebBrowser
    {
        /// <summary>
        /// The longest redirect chain a flow may take before the test fails.
        /// </summary>
        private const int MaximumRedirects = 10;

        /// <summary>
        /// The HTML parser, stateless and shared.
        /// </summary>
        private static readonly HtmlParser Parser = new();

        /// <summary>
        /// The HTTP client, shared by every browser: redirects and cookies are handled here, per browser, the way a browser would.
        /// </summary>
        private static readonly HttpClient Client = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            DefaultRequestHeaders = { { "Accept", "text/html,application/xhtml+xml" } },
        };

        /// <summary>
        /// The cookie jar, one per host. Keycloak 26 marks its session cookies SameSite=None; Secure: a browser still sends
        /// them to http://localhost, HttpClient's CookieContainer does not. No path or expiry rules are needed.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, string>> cookies = [];

        /// <summary>
        /// Sends a GET without following redirects. A relative address is relative to Keycloak.
        /// </summary>
        /// <param name="address">The address, absolute or relative to Keycloak.</param>
        /// <param name="headers">Extra request headers, none by default.</param>
        /// <returns>The response, which the caller disposes.</returns>
        public Task<HttpResponseMessage> Get(string address, IReadOnlyDictionary<string, string> headers = null) =>
            this.Send(HttpMethod.Get, Absolute(address), headers: headers);

        /// <summary>
        /// Opens an address, follows every redirect, and reads the page it ends on.
        /// </summary>
        /// <param name="address">The address, absolute or relative to Keycloak.</param>
        /// <returns>The page the redirects end on.</returns>
        public async Task<WebPage> Open(string address)
        {
            return await this.AutoSubmit(await Read(await this.Follow(await this.Get(address), stopAt: null)));
        }

        /// <summary>
        /// Opens an address and follows Keycloak, past any "proceed" information page, until it hands over to Bloom.
        /// </summary>
        /// <param name="address">The address, absolute or relative to Keycloak.</param>
        /// <returns>The address Keycloak redirected to.</returns>
        /// <exception cref="AssertionException">When a page offers no way to proceed.</exception>
        public async Task<string> OpenUntilApplication(string address)
        {
            var response = await this.Follow(await this.Get(address), MyceliumRealm.RedirectUri);

            while (!IsHandOver(response))
            {
                var page = await Read(response);
                var proceed = page.Document.QuerySelector("#kc-info-message a[href]")?.GetAttribute("href")
                              ?? throw new AssertionException($"stuck on '{page.Document.QuerySelector("#kc-page-title")?.TextContent.Trim()}'");
                response = await this.Follow(await this.Get(proceed), MyceliumRealm.RedirectUri);
            }

            return response.Headers.Location.ToString();
        }

        /// <summary>
        /// Sends the authorization request Bloom sends to start a sign-in (OIDC code flow with PKCE).
        /// </summary>
        /// <returns>The first page Keycloak shows.</returns>
        public Task<WebPage> StartSignIn()
        {
            return this.Open(SignInAddress());
        }

        /// <summary>
        /// Builds the authorization request Bloom sends to start a sign-in (OIDC code flow with PKCE).
        /// </summary>
        /// <param name="redirectUri">The redirect URI to send, Bloom's by default.</param>
        /// <returns>The authorization address, relative to Keycloak.</returns>
        public static string SignInAddress(string redirectUri = null)
        {
            var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

            return $"/realms/{MyceliumRealm.Name}/protocol/openid-connect/auth?client_id={MyceliumRealm.ClientId}"
                   + $"&redirect_uri={Uri.EscapeDataString(redirectUri ?? MyceliumRealm.RedirectUri)}&response_type=code&scope=openid"
                   + $"&state={Guid.NewGuid():N}&code_challenge={challenge}&code_challenge_method=S256";
        }

        /// <summary>
        /// Posts a form with its hidden fields plus the given values, follows every redirect and reads the page it ends on.
        /// </summary>
        /// <param name="page">The page holding the form.</param>
        /// <param name="formSelector">The CSS selector of the form.</param>
        /// <param name="values">The fields to fill in, added to or replacing the form's own.</param>
        /// <returns>The page the redirects end on.</returns>
        public async Task<WebPage> Submit(WebPage page, string formSelector, IReadOnlyDictionary<string, string> values)
        {
            return await this.AutoSubmit(await Read(await this.Follow(await this.Post(page, formSelector, values), stopAt: null)));
        }

        /// <summary>
        /// Posts a form and follows Keycloak until it hands over to Bloom, which is not running in these tests.
        /// </summary>
        /// <param name="page">The page holding the form.</param>
        /// <param name="formSelector">The CSS selector of the form.</param>
        /// <param name="values">The fields to fill in, added to or replacing the form's own.</param>
        /// <returns>The address Keycloak redirected to.</returns>
        /// <exception cref="AssertionException">When Keycloak answers with a page instead.</exception>
        public async Task<string> SubmitUntilApplication(WebPage page, string formSelector, IReadOnlyDictionary<string, string> values)
        {
            var response = await this.Follow(await this.Post(page, formSelector, values), MyceliumRealm.RedirectUri);

            if (IsHandOver(response))
            {
                return response.Headers.Location.ToString();
            }

            var pageTitle = (await Read(response)).Document.QuerySelector("#kc-page-title")?.TextContent.Trim();

            throw new AssertionException($"expected a hand-over to {MyceliumRealm.RedirectUri}, got '{pageTitle}'");
        }

        /// <summary>
        /// Resolves an address against Keycloak.
        /// </summary>
        /// <param name="address">The address, absolute or relative to Keycloak.</param>
        /// <returns>The absolute address.</returns>
        private static Uri Absolute(string address)
        {
            return new(MyceliumRealm.KeycloakUrl, address);
        }

        /// <summary>
        /// Tells whether Keycloak redirects to Bloom.
        /// </summary>
        /// <param name="response">The Keycloak response.</param>
        /// <returns><see langword="true" /> when the response redirects to Bloom.</returns>
        private static bool IsHandOver(HttpResponseMessage response)
        {
            return response.Headers.Location?.ToString().StartsWith(MyceliumRealm.RedirectUri, StringComparison.Ordinal) == true;
        }

        /// <summary>
        /// Parses a response into a page and disposes the response.
        /// </summary>
        /// <param name="response">The HTML response.</param>
        /// <returns>The parsed page.</returns>
        private static async Task<WebPage> Read(HttpResponseMessage response)
        {
            using (response)
            {
                return new WebPage(response.RequestMessage.RequestUri, await Parser.ParseDocumentAsync(await response.Content.ReadAsStringAsync()));
            }
        }

        /// <summary>
        /// Submits the page's form when it submits itself on load, as for OIDC response_mode=form_post (the code never sits in a URL).
        /// </summary>
        /// <param name="page">The page Keycloak answered with.</param>
        /// <returns>The page after the submission, or the same page.</returns>
        private async Task<WebPage> AutoSubmit(WebPage page)
        {
            return page.Document.Body?.GetAttribute("onload")?.Contains("submit()", StringComparison.Ordinal) == true
                ? await this.Submit(page, "form", new Dictionary<string, string>())
                : page;
        }

        /// <summary>
        /// Follows redirects, carrying cookies across hosts.
        /// </summary>
        /// <param name="response">The first response.</param>
        /// <param name="stopAt">An address prefix to stop at without following it, or <see langword="null" /> to follow every redirect.</param>
        /// <returns>The first response that is not followed.</returns>
        /// <exception cref="AssertionException">When the chain passes <see cref="MaximumRedirects" />.</exception>
        private async Task<HttpResponseMessage> Follow(HttpResponseMessage response, string stopAt)
        {
            for (var redirect = 0; (int)response.StatusCode is >= 300 and < 400; redirect++)
            {
                var location = new Uri(response.RequestMessage.RequestUri, response.Headers.Location);

                if (stopAt is not null && location.ToString().StartsWith(stopAt, StringComparison.Ordinal))
                {
                    return response;
                }

                if (redirect == MaximumRedirects)
                {
                    throw new AssertionException($"more than {MaximumRedirects} redirects, the last to {location}");
                }

                response.Dispose();
                response = await this.Send(HttpMethod.Get, location);
            }

            return response;
        }

        /// <summary>
        /// Posts a form with its hidden fields plus the given values, without following redirects.
        /// </summary>
        /// <param name="page">The page holding the form.</param>
        /// <param name="formSelector">The CSS selector of the form.</param>
        /// <param name="values">The fields to fill in, added to or replacing the form's own.</param>
        /// <returns>The response, which the caller disposes.</returns>
        /// <exception cref="AssertionException">When the page has no such form.</exception>
        private Task<HttpResponseMessage> Post(WebPage page, string formSelector, IReadOnlyDictionary<string, string> values)
        {
            var form = page.Document.QuerySelector(formSelector) ?? throw new AssertionException($"no form {formSelector} in {page.Address}");

            var fields = form.QuerySelectorAll("input[name]")
                .Where(input => input.GetAttribute("type") is not ("checkbox" or "submit"))
                .ToDictionary(input => input.GetAttribute("name"), input => input.GetAttribute("value") ?? string.Empty);

            foreach (var (name, value) in values)
            {
                fields[name] = value;
            }

            return this.Send(HttpMethod.Post, new Uri(page.Address, form.GetAttribute("action")), new FormUrlEncodedContent(fields));
        }

        /// <summary>
        /// Sends one request with the cookies of its host and stores the cookies the response sets.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="address">The absolute address.</param>
        /// <param name="content">The request body, none by default.</param>
        /// <param name="headers">Extra request headers, none by default.</param>
        /// <returns>The response, which the caller disposes.</returns>
        private async Task<HttpResponseMessage> Send(HttpMethod method, Uri address, HttpContent content = null, IReadOnlyDictionary<string, string> headers = null)
        {
            using var request = new HttpRequestMessage(method, address) { Content = content };

            foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            {
                request.Headers.Add(name, value);
            }

            var jar = this.cookies.TryGetValue(address.Authority, out var existing) ? existing : this.cookies[address.Authority] = [];

            if (jar.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", jar.Select(cookie => $"{cookie.Key}={cookie.Value}")));
            }

            var response = await Client.SendAsync(request);

            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var pair in setCookies.Select(setCookie => setCookie.Split(';', 2)[0].Split('=', 2)))
                {
                    if (pair[1].Length == 0)
                    {
                        jar.Remove(pair[0]);
                    }
                    else
                    {
                        jar[pair[0]] = pair[1];
                    }
                }
            }

            return response;
        }
    }
}
