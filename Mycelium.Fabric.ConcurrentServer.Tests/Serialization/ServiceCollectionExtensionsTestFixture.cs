// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceCollectionExtensionsTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Serialization
{
    using System;

    using Carter;

    using Microsoft.Extensions.DependencyInjection;

    using Mycelium.Fabric.ConcurrentServer.Serialization;

    using SysML2.NET.Serializer.Json;

    [TestFixture]
    public class ServiceCollectionExtensionsTestFixture
    {
        [Test]
        public void VerifyAddRequiredServices()
        {
            Assert.That(() => ((IServiceCollection)null).AddRequiredServices(), Throws.TypeOf<ArgumentNullException>());

            var services = new ServiceCollection();

            Assert.That(services.AddRequiredServices(), Is.SameAs(services));

            using var provider = services.AddLogging().BuildServiceProvider();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(provider.GetService<ISerializer>(), Is.TypeOf<Serializer>());
                Assert.That(provider.GetService<IDeSerializer>(), Is.TypeOf<DeSerializer>());
                Assert.That(provider.GetServices<IRequestBodyDeserializer>(), Has.One.TypeOf<JsonRequestBodyDeserializer>());
                Assert.That(provider.GetService<IRequestBodyReader>(), Is.TypeOf<RequestBodyReader>());

                // Carter's own default negotiator writes any *json media type without the SysML2.NET serializers, so none of its negotiators survives.
                Assert.That(provider.GetServices<IResponseNegotiator>(), Has.Exactly(1).Items.And.All.TypeOf<JsonResponseNegotiator>());
                Assert.That(provider.GetServices<ICarterModule>(), Is.Not.Empty);
            }
        }
    }
}
