// ------------------------------------------------------------------------------------------------
//  <copyright file="RequestBodyReaderTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Serialization
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Net.Http.Headers;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PSM.DTO;

    [TestFixture]
    public class RequestBodyReaderTestFixture
    {
        private Mock<IRequestBodyDeserializer> deserializer;

        private RequestBodyReader reader;

        [SetUp]
        public void SetUp()
        {
            this.deserializer = new Mock<IRequestBodyDeserializer>();

            this.deserializer
                .Setup(bodyDeserializer => bodyDeserializer.CanRead(It.Is<MediaTypeHeaderValue>(contentType => contentType.MediaType == "application/json")))
                .Returns(true);

            this.reader = new RequestBodyReader([this.deserializer.Object]);
        }

        private static HttpRequest Request(string contentType, long? contentLength, bool canHaveBody = true)
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    ContentType = contentType,
                    ContentLength = contentLength,
                    Body = new MemoryStream()
                }
            };

            if (!canHaveBody)
            {
                context.Features.Set(Mock.Of<IHttpRequestBodyDetectionFeature>(feature => !feature.CanHaveBody));
            }

            return context.Request;
        }

        [Test]
        public void VerifyConstructor()
        {
            Assert.That(() => new RequestBodyReader(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public async Task VerifyReadRequestAsync()
        {
            Assert.That(() => this.reader.ReadRequestAsync<BranchRequest>(null), Throws.TypeOf<ArgumentNullException>());

            var outcome = await this.reader.ReadRequestAsync<BranchRequest>(Request("application/json", 0));

            Assert.That(outcome.FirstError.Code, Is.EqualTo("body.missing"));

            outcome = await this.reader.ReadRequestAsync<BranchRequest>(Request("application/json", null, false));

            Assert.That(outcome.FirstError.Code, Is.EqualTo("body.missing"));

            outcome = await this.reader.ReadRequestAsync<BranchRequest>(Request("application/msgpack", 12));

            Assert.That(outcome.FirstError.NumericType, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));

            outcome = await this.reader.ReadRequestAsync<BranchRequest>(Request(null, 12));

            Assert.That(outcome.FirstError.NumericType, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));

            var request = new BranchRequest { Name = "feature" };

            this.deserializer
                .Setup(bodyDeserializer => bodyDeserializer.ReadRequestAsync<BranchRequest>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(request);

            outcome = await this.reader.ReadRequestAsync<BranchRequest>(Request("application/json; charset=utf-8", 12));

            Assert.That(outcome.Value, Is.SameAs(request));

            this.deserializer
                .Setup(bodyDeserializer => bodyDeserializer.ReadRequestAsync<BranchRequest>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidDataException("malformed"));

            outcome = await this.reader.ReadRequestAsync<BranchRequest>(Request("application/json", 12));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(outcome.FirstError.Type, Is.EqualTo(ErrorType.Validation));
                Assert.That(outcome.FirstError.Description, Is.EqualTo("malformed"));
            }
        }

        [Test]
        public async Task VerifyReadDataAsync()
        {
            Assert.That(() => this.reader.ReadDataAsync(null), Throws.TypeOf<ArgumentNullException>());

            var outcome = await this.reader.ReadDataAsync(Request(null, 0));

            Assert.That(outcome.Value, Has.Count.EqualTo(0));

            outcome = await this.reader.ReadDataAsync(Request(null, null, false));

            Assert.That(outcome.Value, Has.Count.EqualTo(0));

            outcome = await this.reader.ReadDataAsync(Request("application/msgpack", 12));

            Assert.That(outcome.FirstError.NumericType, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));

            var partUsage = new PartUsage { Id = Guid.NewGuid() };

            this.deserializer
                .Setup(bodyDeserializer => bodyDeserializer.ReadDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([partUsage]);

            outcome = await this.reader.ReadDataAsync(Request("application/json", 12));

            Assert.That(outcome.Value, Is.EqualTo([partUsage]));

            // A chunked body declares no Content-Length, yet is read.
            outcome = await this.reader.ReadDataAsync(Request("application/json", null));

            Assert.That(outcome.Value, Is.EqualTo([partUsage]));

            this.deserializer
                .Setup(bodyDeserializer => bodyDeserializer.ReadDataAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidDataException("malformed"));

            outcome = await this.reader.ReadDataAsync(Request("application/json", 12));

            Assert.That(outcome.FirstError.Type, Is.EqualTo(ErrorType.Validation));
        }
    }
}
