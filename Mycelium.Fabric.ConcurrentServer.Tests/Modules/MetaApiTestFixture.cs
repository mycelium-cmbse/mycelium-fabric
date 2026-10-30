// ------------------------------------------------------------------------------------------------
//  <copyright file="MetaApiTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Modules
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    [TestFixture]
    public class MetaApiTestFixture
    {
        private Mock<ILogger<MetaApi>> logger;

        private const string DatatypeId = "https://a.uri";

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<MetaApi>();
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new MetaApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(2));
                Assert.That(templates["getDatatypes"], Is.EqualTo("/meta/datatypes"));
                Assert.That(templates["getDatatypeById"], Is.EqualTo("/meta/datatypes/{datatypeId}"));
                Assert.That(app.DeclaredStatusCodes("getDatatypes"), Is.EqualTo([200, 500]));
                Assert.That(app.DeclaredStatusCodes("getDatatypeById"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetDatatypes()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, "/meta/datatypes");

            invocation.AssertNotYetImplemented("getDatatypes");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getDatatypes");
        }

        [Test]
        public async Task VerifyGetDatatypeById()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/meta/datatypes/{Uri.EscapeDataString(DatatypeId)}");

            invocation.AssertNotYetImplemented("getDatatypeById");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getDatatypeById");
        }
    }
}
