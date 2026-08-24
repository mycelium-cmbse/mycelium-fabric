// ------------------------------------------------------------------------------------------------
//  <copyright file="QueryApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="QueryApi"/> class.
    /// </summary>
    [TestFixture]
    public class QueryApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid QueryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        private Mock<ILogger<QueryApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<QueryApi>>();
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new QueryApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(8));
                Assert.That(templates["getQueriesByProject"], Is.EqualTo("/projects/{projectId:guid}/queries"));
                Assert.That(templates["postQueryByProject"], Is.EqualTo("/projects/{projectId:guid}/queries"));
                Assert.That(templates["getQueryByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}"));
                Assert.That(templates["putQueryByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}"));
                Assert.That(templates["deleteQueryByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}"));
                Assert.That(templates["getQueryResultsByProjectIdQueryId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}/results"));
                Assert.That(templates["getQueryResultsByProjectIdQuery"], Is.EqualTo("/projects/{projectId:guid}/query-results"));
                Assert.That(templates["getQueryResultsByProjectIdQueryPost"], Is.EqualTo("/projects/{projectId:guid}/query-results"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getQueriesByProject"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("postQueryByProject"), Is.EqualTo([201, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryByProjectAndId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("putQueryByProjectAndId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("deleteQueryByProjectAndId"), Is.EqualTo([200, 204, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryResultsByProjectIdQueryId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryResultsByProjectIdQuery"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryResultsByProjectIdQueryPost"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetQueriesByProject()
        {
            Assert.That(() => QueryApi.GetQueriesByProject(null, ProjectId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.GetQueriesByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("getQueriesByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getQueriesByProject");
        }

        [Test]
        public async Task VerifyPostQueryByProject()
        {
            Assert.That(() => QueryApi.PostQueryByProject(null, ProjectId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.PostQueryByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("postQueryByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "postQueryByProject");
        }

        [Test]
        public async Task VerifyGetQueryByProjectAndId()
        {
            Assert.That(() => QueryApi.GetQueryByProjectAndId(null, ProjectId, QueryId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.GetQueryByProjectAndId(context, ProjectId, QueryId, this.logger.Object));

            invocation.AssertNotYetImplemented("getQueryByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getQueryByProjectAndId");
        }

        [Test]
        public async Task VerifyPutQueryByProjectAndId()
        {
            Assert.That(() => QueryApi.PutQueryByProjectAndId(null, ProjectId, QueryId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.PutQueryByProjectAndId(context, ProjectId, QueryId, this.logger.Object));

            invocation.AssertNotYetImplemented("putQueryByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "putQueryByProjectAndId");
        }

        [Test]
        public async Task VerifyDeleteQueryByProjectAndId()
        {
            Assert.That(() => QueryApi.DeleteQueryByProjectAndId(null, ProjectId, QueryId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.DeleteQueryByProjectAndId(context, ProjectId, QueryId, this.logger.Object));

            invocation.AssertNotYetImplemented("deleteQueryByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "deleteQueryByProjectAndId");
        }

        [Test]
        public async Task VerifyGetQueryResultsByProjectIdQueryId()
        {
            Assert.That(() => QueryApi.GetQueryResultsByProjectIdQueryId(null, ProjectId, QueryId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.GetQueryResultsByProjectIdQueryId(context, ProjectId, QueryId, this.logger.Object));

            invocation.AssertNotYetImplemented("getQueryResultsByProjectIdQueryId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getQueryResultsByProjectIdQueryId");
        }

        [Test]
        public async Task VerifyGetQueryResultsByProjectIdQuery()
        {
            Assert.That(() => QueryApi.GetQueryResultsByProjectIdQuery(null, ProjectId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.GetQueryResultsByProjectIdQuery(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("getQueryResultsByProjectIdQuery");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getQueryResultsByProjectIdQuery");
        }

        [Test]
        public async Task VerifyGetQueryResultsByProjectIdQueryPost()
        {
            Assert.That(() => QueryApi.GetQueryResultsByProjectIdQueryPost(null, ProjectId, Mock.Of<ILogger<QueryApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => QueryApi.GetQueryResultsByProjectIdQueryPost(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("getQueryResultsByProjectIdQueryPost");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getQueryResultsByProjectIdQueryPost");
        }
    }
}
