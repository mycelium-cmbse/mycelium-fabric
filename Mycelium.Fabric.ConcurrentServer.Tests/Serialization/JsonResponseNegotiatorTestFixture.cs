// ------------------------------------------------------------------------------------------------
//  <copyright file="JsonResponseNegotiatorTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Carter;
    using Carter.Response;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Net.Http.Headers;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PSM.DTO;
    using SysML2.NET.Serializer.Json;

    [TestFixture]
    public class JsonResponseNegotiatorTestFixture
    {
        private static readonly Guid Identifier = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private JsonResponseNegotiator negotiator;

        [SetUp]
        public void SetUp()
        {
            this.negotiator = new JsonResponseNegotiator(new Serializer());
        }

        private static HttpResponse Response()
        {
            var context = new DefaultHttpContext
            {
                Response =
                {
                    Body = new MemoryStream()
                }
            };

            return context.Response;
        }

        private static string Written(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);

            return new StreamReader(response.Body).ReadToEnd();
        }

        [Test]
        public void VerifyConstructor()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => new JsonResponseNegotiator(null), Throws.TypeOf<ArgumentNullException>());
                Assert.That(new JsonResponseNegotiator(new Serializer()), Is.Not.Null);
            }
        }

        [Test]
        public void VerifyCanHandle()
        {
            Assert.That(() => this.negotiator.CanHandle(null), Throws.TypeOf<ArgumentNullException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("application/json")), Is.True);
                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("APPLICATION/JSON")), Is.True);
                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("application/json; charset=utf-8")), Is.True);

                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("*/*")), Is.True);
                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("application/*")), Is.True);
                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("application/ld+json")), Is.False);
                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("application/xml")), Is.False);
                Assert.That(this.negotiator.CanHandle(MediaTypeHeaderValue.Parse("text/*")), Is.False);
            }
        }

        [Test]
        public async Task VerifyHandle()
        {
            var response = Response();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.negotiator.Handle(null, null, new Project(), CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => this.negotiator.Handle<Project>(null, Response(), null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            }

            // A single API response model reaches the generated PSM serializers.
            await this.negotiator.Handle(null, response, new Project { Id = Identifier, Name = "Hello" }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.ContentType, Is.EqualTo(JsonResponseNegotiator.MediaType));
                Assert.That(JsonDocument.Parse(Written(response)).RootElement.GetProperty("@type").GetString(), Is.EqualTo("Project"));
            }

            response = Response();

            await this.negotiator.Handle<IReadOnlyList<Project>>(null, response, [new Project { Id = Identifier }], CancellationToken.None);

            Assert.That(JsonDocument.Parse(Written(response)).RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));

            // A SysML Core element is an IIdentified alone and reaches the Core serializers.
            response = Response();

            await this.negotiator.Handle<IElement>(null, response, new PartDefinition { Id = Identifier }, CancellationToken.None);

            Assert.That(JsonDocument.Parse(Written(response)).RootElement.GetProperty("@type").GetString(), Is.EqualTo("PartDefinition"));

            response = Response();

            await this.negotiator.Handle<IReadOnlyList<IElement>>(null, response, [new PartDefinition { Id = Identifier }], CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(JsonDocument.Parse(Written(response)).RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(() => this.negotiator.Handle(null, Response(), "not a payload of the PSM", CancellationToken.None), Throws.TypeOf<NotSupportedException>());
            }

            // A query result may mix API responses with SysML Core elements, each written by its own serializer.
            response = Response();

            await this.negotiator.Handle<IReadOnlyList<object>>(null, response, [new PartDefinition { Id = Identifier }, new Project { Id = Identifier }], CancellationToken.None);

            var mixed = JsonDocument.Parse(Written(response)).RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(mixed.GetArrayLength(), Is.EqualTo(2));
                Assert.That(mixed[0].GetProperty("@type").GetString(), Is.EqualTo("PartDefinition"));
                Assert.That(mixed[1].GetProperty("@type").GetString(), Is.EqualTo("Project"));
            }

            Assert.That(async () => await this.negotiator.Handle<IReadOnlyList<object>>(null, Response(), [new Project(), "not a payload of the PSM"], CancellationToken.None),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public async Task VerifyNegotiate()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddRequiredServices();

            var provider = services.BuildServiceProvider();

            foreach (var accept in new[] { null, "*/*", "text/html", "application/json", "application/ld+json" })
            {
                var context = new DefaultHttpContext
                {
                    RequestServices = provider,
                    Response =
                    {
                        Body = new MemoryStream()
                    },
                    Request =
                    {
                        Headers =
                        {
                            Accept = accept
                        }
                    }
                };

                await context.Response.Negotiate(new Project { Id = Identifier, Name = "Negotiated" });

                Assert.That(JsonDocument.Parse(Written(context.Response)).RootElement.GetProperty("@type").GetString(), Is.EqualTo("Project"));
            }
        }
    }
}
