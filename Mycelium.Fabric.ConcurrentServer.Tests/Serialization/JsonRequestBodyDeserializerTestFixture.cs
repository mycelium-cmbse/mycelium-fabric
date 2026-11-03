// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonRequestBodyDeserializerTestFixture.cs" company="Starion Group S.A.">
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
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Net.Http.Headers;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    [TestFixture]
    public class JsonRequestBodyDeserializerTestFixture
    {
        private Mock<IDeSerializer> deSerializer;

        private JsonRequestBodyDeserializer requestBodyDeserializer;

        private MemoryStream body;

        [SetUp]
        public void SetUp()
        {
            this.deSerializer = new Mock<IDeSerializer>();
            this.requestBodyDeserializer = new JsonRequestBodyDeserializer(this.deSerializer.Object);
            this.body = new MemoryStream();
        }

        [TearDown]
        public void TearDown()
        {
            this.body.Dispose();
        }

        [Test]
        public void VerifyConstructor()
        {
            Assert.That(() => new JsonRequestBodyDeserializer(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void VerifyCanRead()
        {
            Assert.That(() => this.requestBodyDeserializer.CanRead(null), Throws.TypeOf<ArgumentNullException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.requestBodyDeserializer.CanRead(MediaTypeHeaderValue.Parse("application/json")), Is.True);
                Assert.That(this.requestBodyDeserializer.CanRead(MediaTypeHeaderValue.Parse("Application/JSON; charset=utf-8")), Is.True);
                Assert.That(this.requestBodyDeserializer.CanRead(MediaTypeHeaderValue.Parse("application/msgpack")), Is.False);
                Assert.That(this.requestBodyDeserializer.CanRead(MediaTypeHeaderValue.Parse("text/plain")), Is.False);
            }
        }

        [Test]
        public async Task VerifyReadRequestAsync()
        {
            Assert.That(() => this.requestBodyDeserializer.ReadRequestAsync<BranchRequest>(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            var request = new BranchRequest { Name = "feature" };

            this.deSerializer
                .Setup(jsonDeSerializer => jsonDeSerializer.DeSerializeRequestAsync<BranchRequest>(this.body, SerializationModeKind.JSON, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(request);

            Assert.That(await this.requestBodyDeserializer.ReadRequestAsync<BranchRequest>(this.body, CancellationToken.None), Is.SameAs(request));

            this.deSerializer
                .Setup(jsonDeSerializer => jsonDeSerializer.DeSerializeRequestAsync<BranchRequest>(this.body, SerializationModeKind.JSON, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((BranchRequest)null);

            Assert.That(() => this.requestBodyDeserializer.ReadRequestAsync<BranchRequest>(this.body, CancellationToken.None), Throws.TypeOf<InvalidDataException>());

            foreach (var failure in new Exception[]
                     {
                         new JsonException("malformed"), new SerializationException("no @type"), new InvalidOperationException("wrong family"), new FormatException("bad @id")
                     })
            {
                this.deSerializer
                    .Setup(jsonDeSerializer => jsonDeSerializer.DeSerializeRequestAsync<BranchRequest>(this.body, SerializationModeKind.JSON, false, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(failure);

                Assert.That(() => this.requestBodyDeserializer.ReadRequestAsync<BranchRequest>(this.body, CancellationToken.None),
                    Throws.TypeOf<InvalidDataException>().With.InnerException.SameAs(failure));
            }
        }

        [Test]
        public async Task VerifyReadDataAsync()
        {
            Assert.That(() => this.requestBodyDeserializer.ReadDataAsync(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            var partUsage = new PartUsage { Id = Guid.NewGuid() };

            this.deSerializer
                .Setup(jsonDeSerializer =>
                    jsonDeSerializer.DeSerializeAsync(It.IsAny<Stream>(), SerializationModeKind.JSON, SerializationTargetKind.PSM, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync([partUsage]);

            var data = await this.requestBodyDeserializer.ReadDataAsync(this.body, CancellationToken.None);

            Assert.That(data, Is.EqualTo([partUsage]));

            this.deSerializer
                .Setup(jsonDeSerializer =>
                    jsonDeSerializer.DeSerializeAsync(It.IsAny<Stream>(), SerializationModeKind.JSON, SerializationTargetKind.PSM, false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new JsonException("malformed"));

            Assert.That(() => this.requestBodyDeserializer.ReadDataAsync(this.body, CancellationToken.None), Throws.TypeOf<InvalidDataException>());

            // The OpenAPI document allows a JSON null in place of the array, which carries no Data.
            data = await this.requestBodyDeserializer.ReadDataAsync(new MemoryStream(" null "u8.ToArray()), CancellationToken.None);

            Assert.That(data, Is.Empty);

            this.deSerializer.Verify(jsonDeSerializer => jsonDeSerializer.DeSerializeAsync(It.IsAny<Stream>(), It.IsAny<SerializationModeKind>(),
                It.IsAny<SerializationTargetKind>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
