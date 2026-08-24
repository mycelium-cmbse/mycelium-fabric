// ------------------------------------------------------------------------------------------------
//  <copyright file="HandlerInvocation.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Moq;

    /// <summary>
    /// Captures the response a request handler writes to its <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="StatusCode">The status code written to the response.</param>
    /// <param name="ContentType">The content type written to the response.</param>
    /// <param name="Problem">The RFC 7807 payload deserialized from the response body.</param>
    internal sealed record HandlerInvocation(int StatusCode, string ContentType, ProblemDetails Problem)
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new (JsonSerializerDefaults.Web);
        
        /// <summary>
        /// The request path the handlers are invoked against.
        /// </summary>
        internal const string RequestPath = "/mycelium-fabric-test";

        /// <summary>
        /// Invokes the supplied handler against a fresh <see cref="DefaultHttpContext"/> and captures what
        /// it wrote.
        /// </summary>
        /// <param name="handler">The handler under test, closed over its route parameters.</param>
        /// <returns>The captured status code, content type and payload.</returns>
        internal static async Task<HandlerInvocation> CaptureAsync(Func<HttpContext, Task> handler)
        {
            var services = new ServiceCollection();
            services.AddLogging();

            var context = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
                Request =
                {
                    Path = RequestPath
                },
                Response =
                {
                    Body = new MemoryStream()
                }
            };

            await handler(context);

            context.Response.Body.Seek(0, SeekOrigin.Begin);

            var problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
                context.Response.Body,
                JsonSerializerOptions);

            return new HandlerInvocation(context.Response.StatusCode, context.Response.ContentType, problem);
        }

#pragma warning disable CA1873
        /// <summary>
        /// Verifies that the handler emitted exactly one <see cref="LogLevel.Information"/> entry naming
        /// the supplied operation.
        /// </summary>
        /// <typeparam name="TModule">The Carter module the logger is typed by.</typeparam>
        /// <param name="logger">The logger mock passed to the handler.</param>
        /// <param name="operationId">The operation id the log entry is expected to name.</param>
        internal static void VerifyInformationLogged<TModule>(Mock<ILogger<TModule>> logger, string operationId)
        {
            logger.Verify(
                log => log.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains(operationId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
#pragma warning restore CA1873
        
        /// <summary>
        /// Gets the <c>operationId</c> extension member carried by the RFC 7807 payload.
        /// </summary>
        /// <returns>The operation id, or <c>null</c> when the payload does not carry one.</returns>
        internal string OperationId()
        {
            return this.Problem.Extensions.TryGetValue("operationId", out var operationId)
                ? operationId?.ToString()
                : null;
        }

        /// <summary>
        /// Asserts that the captured response is the complete not-yet-implemented placeholder for the
        /// supplied operation.
        /// </summary>
        /// <param name="operationId">The operation id the handler is registered under.</param>
        /// <remarks>
        /// Replace the call with the operation's real assertions when the handler is implemented — see
        /// <c>TESTING.md</c> §9.
        /// </remarks>
        internal void AssertNotYetImplemented(string operationId)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
                Assert.That(this.ContentType, Does.Contain("application/problem+json"));
                Assert.That(this.Problem.Title, Is.EqualTo("Not yet implemented"));
                Assert.That(this.Problem.Status, Is.EqualTo(StatusCodes.Status500InternalServerError));
                Assert.That(this.Problem.Instance, Is.EqualTo(RequestPath));
                Assert.That(this.Problem.Detail, Does.Contain(operationId));
                Assert.That(this.OperationId(), Is.EqualTo(operationId));
            }
        }
    }
}
