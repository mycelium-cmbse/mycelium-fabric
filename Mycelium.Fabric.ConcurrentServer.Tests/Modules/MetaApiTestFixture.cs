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
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    /// <summary>
    /// Suite of tests for the <see cref="MetaApi"/> class.
    /// </summary>
    [TestFixture]
    public class MetaApiTestFixture
    {
        private Mock<ILogger<MetaApi>> logger;

        private const string DatatypeId = "https://a.uri";
        
        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<MetaApi>>();
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
            Assert.That(() => MetaApi.GetDatatypes(null, Mock.Of<ILogger<MetaApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => MetaApi.GetDatatypes(context, this.logger.Object));

            invocation.AssertNotYetImplemented("getDatatypes");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getDatatypes");
        }

        [Test]
        public async Task VerifyGetDatatypeById()
        {
            Assert.That(() => MetaApi.GetDatatypeById(null, Mock.Of<ILogger<MetaApi>>(), DatatypeId), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => MetaApi.GetDatatypeById(context, this.logger.Object, DatatypeId));

            invocation.AssertNotYetImplemented("getDatatypeById");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getDatatypeById");
        }
    }
}
