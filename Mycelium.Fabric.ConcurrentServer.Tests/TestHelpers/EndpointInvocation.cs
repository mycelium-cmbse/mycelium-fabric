// ------------------------------------------------------------------------------------------------
//  <copyright file="EndpointInvocation.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    internal sealed record EndpointInvocation(string Path, int StatusCode, string ContentType, string Body, string Link, string PreferenceApplied)
    {
        internal static async Task<EndpointInvocation> SendAsync(HttpClient client, HttpMethod method, string pathAndQuery, string requestBody = null,
            string requestContentType = JsonResponseNegotiator.MediaType, string accept = null, string prefer = null)
        {
            using var request = new HttpRequestMessage(method, pathAndQuery);

            if (accept != null)
            {
                request.Headers.Accept.ParseAdd(accept);
            }

            if (prefer != null)
            {
                request.Headers.TryAddWithoutValidation("Prefer", prefer);
            }

            if (requestBody != null)
            {
                request.Content = new StringContent(requestBody, Encoding.UTF8);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(requestContentType);
            }

            using var response = await client.SendAsync(request);

            var link = response.Headers.TryGetValues(Microsoft.Net.Http.Headers.HeaderNames.Link, out var values) ? string.Join(", ", values) : string.Empty;
            var preferenceApplied = response.Headers.TryGetValues("Preference-Applied", out var applied) ? string.Join(", ", applied) : string.Empty;

            return new EndpointInvocation(PathString.FromUriComponent(response.RequestMessage.RequestUri).Value, (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                await response.Content.ReadAsStringAsync(), link, preferenceApplied);
        }

#pragma warning disable CA1873
        internal static void VerifyInformationLogged<TModule>(Mock<ILogger<TModule>> logger, string operationId)
        {
            logger.Verify(log => log.Log(LogLevel.Information, It.IsAny<EventId>(), It.Is<It.IsAnyType>((state, _) => state.ToString().Contains(operationId)),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        internal static void VerifyErrorLogged<TModule>(Mock<ILogger<TModule>> logger, string operationId)
        {
            logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.Is<It.IsAnyType>((state, _) => state.ToString().Contains(operationId)), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
#pragma warning restore CA1873

        internal string ErrorDescription => this.Payload().GetProperty("description").GetString();

        internal JsonElement Payload()
        {
            return JsonDocument.Parse(this.Body).RootElement;
        }

        internal void AssertNotYetImplemented(string operationId)
        {
            this.AssertError(StatusCodes.Status500InternalServerError, operationId);

            Assert.That(this.ErrorDescription, Does.Contain("not yet implemented"));
        }

        internal void AssertError(int statusCode, string operationId)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.StatusCode, Is.EqualTo(statusCode));
                Assert.That(this.ContentType, Is.EqualTo(JsonResponseNegotiator.MediaType));
                Assert.That(this.Payload().GetProperty("@type").GetString(), Is.EqualTo("Error"));
                Assert.That(this.ErrorDescription, Does.StartWith($"{operationId}: "));
            }
        }

        internal void AssertMinimalReturn()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.StatusCode, Is.EqualTo(StatusCodes.Status204NoContent));
                Assert.That(this.Body, Is.Empty);
                Assert.That(this.PreferenceApplied, Is.EqualTo("return=minimal"));
            }
        }

        internal void AssertPayload(int statusCode, params string[] identifiers)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.StatusCode, Is.EqualTo(statusCode));
                Assert.That(this.ContentType, Is.EqualTo(JsonResponseNegotiator.MediaType));
                Assert.That(this.PayloadIdentifiers(), Is.EqualTo(identifiers));
            }
        }

        private IReadOnlyList<string> PayloadIdentifiers()
        {
            var payload = this.Payload();

            if (payload.ValueKind != JsonValueKind.Array)
            {
                return [payload.GetProperty("@id").GetString()];
            }

            return [.. payload.EnumerateArray().Select(record => record.GetProperty("@id").GetString())];
        }
    }
}
