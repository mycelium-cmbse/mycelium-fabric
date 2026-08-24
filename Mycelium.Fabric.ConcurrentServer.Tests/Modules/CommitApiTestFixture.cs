// ------------------------------------------------------------------------------------------------
//  <copyright file="CommitApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="CommitApi"/> class.
    /// </summary>
    [TestFixture]
    public class CommitApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid ChangeId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        private Mock<ILogger<CommitApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<CommitApi>>();
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new CommitApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(5));
                Assert.That(templates["getCommitsByProject"], Is.EqualTo("/projects/{projectId:guid}/commits"));
                Assert.That(templates["postCommitByProject"], Is.EqualTo("/projects/{projectId:guid}/commits"));
                Assert.That(templates["getCommitByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}"));
                Assert.That(templates["getChangesByProjectCommit"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/changes"));
                Assert.That(templates["getChangeByProjectCommitId"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/changes/{changeId:guid}"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getCommitsByProject"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("postCommitByProject"), Is.EqualTo([201, 500]));
                Assert.That(app.DeclaredStatusCodes("getCommitByProjectAndId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getChangesByProjectCommit"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getChangeByProjectCommitId"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetCommitsByProject()
        {
            Assert.That(() => CommitApi.GetCommitsByProject(null, ProjectId, Mock.Of<ILogger<CommitApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => CommitApi.GetCommitsByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("getCommitsByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getCommitsByProject");
        }

        [Test]
        public async Task VerifyPostCommitByProject()
        {
            Assert.That(() => CommitApi.PostCommitByProject(null, ProjectId, Mock.Of<ILogger<CommitApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => CommitApi.PostCommitByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("postCommitByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "postCommitByProject");
        }

        [Test]
        public async Task VerifyGetCommitByProjectAndId()
        {
            Assert.That(() => CommitApi.GetCommitByProjectAndId(null, ProjectId, CommitId, Mock.Of<ILogger<CommitApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => CommitApi.GetCommitByProjectAndId(context, ProjectId, CommitId, this.logger.Object));

            invocation.AssertNotYetImplemented("getCommitByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getCommitByProjectAndId");
        }

        [Test]
        public async Task VerifyGetChangesByProjectCommit()
        {
            Assert.That(() => CommitApi.GetChangesByProjectCommit(null, ProjectId, CommitId, Mock.Of<ILogger<CommitApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => CommitApi.GetChangesByProjectCommit(context, ProjectId, CommitId, this.logger.Object));

            invocation.AssertNotYetImplemented("getChangesByProjectCommit");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getChangesByProjectCommit");
        }

        [Test]
        public async Task VerifyGetChangeByProjectCommitId()
        {
            Assert.That(() => CommitApi.GetChangeByProjectCommitId(null, ProjectId, CommitId, ChangeId, Mock.Of<ILogger<CommitApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => CommitApi.GetChangeByProjectCommitId(context, ProjectId, CommitId, ChangeId, this.logger.Object));

            invocation.AssertNotYetImplemented("getChangeByProjectCommitId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getChangeByProjectCommitId");
        }
    }
}
