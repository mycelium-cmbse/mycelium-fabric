// ------------------------------------------------------------------------------------------------
//  <copyright file="NegotiatedExceptionHandlerTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Http
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Carter;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.Serializer.Json;

    [TestFixture]
    public class NegotiatedExceptionHandlerTestFixture
    {
        private Mock<ILogger<NegotiatedExceptionHandler>> logger;

        private NegotiatedExceptionHandler handler;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<NegotiatedExceptionHandler>>();

            this.handler = new NegotiatedExceptionHandler(this.logger.Object);
        }

        private static DefaultHttpContext Context()
        {
            return new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton<IResponseNegotiator>(new JsonResponseNegotiator(new Serializer())).BuildServiceProvider(),
                Request =
                {
                    Method = "POST",
                    Path = "/projects"
                },
                Response =
                {
                    Body = new MemoryStream()
                }
            };
        }

        [Test]
        public void VerifyConstructor()
        {
            Assert.That(() => new NegotiatedExceptionHandler(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task VerifyTryHandleAsync()
        {
            var exception = new InvalidOperationException("the store is unreachable");

            Assert.That(async () => await this.handler.TryHandleAsync(null, exception, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            var context = Context();

            var handled = await this.handler.TryHandleAsync(context, exception, CancellationToken.None);

            context.Response.Body.Seek(0, SeekOrigin.Begin);

            var error = (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(handled, Is.True);
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
                Assert.That(context.Response.ContentType, Is.EqualTo(JsonResponseNegotiator.MediaType));
                Assert.That(error.GetProperty("@type").GetString(), Is.EqualTo("Error"));

                // The cause is recorded in the log, not put on the wire.
                Assert.That(error.GetProperty("description").GetString(), Does.Not.Contain("the store is unreachable"));
            }

            this.logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("/projects")), exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            // A rejected request keeps its own status and is not an unexpected failure.
            context = Context();

            handled = await this.handler.TryHandleAsync(context, new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge),
                CancellationToken.None);

            context.Response.Body.Seek(0, SeekOrigin.Begin);

            error = (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(handled, Is.True);
                Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
                Assert.That(error.GetProperty("@type").GetString(), Is.EqualTo("Error"));
            }

            // A client that has gone needs no answer.
            context = Context();

            using var aborted = new CancellationTokenSource();
            await aborted.CancelAsync();
            context.RequestAborted = aborted.Token;

            handled = await this.handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(handled, Is.True);
                Assert.That(context.Response.Body.Length, Is.EqualTo(0));
            }

            this.logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
    }
}
