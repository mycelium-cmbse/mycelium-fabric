// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="ElementApi"/> class.
    /// </summary>
    [TestFixture]
    public class ElementApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid ElementId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        private Mock<ILogger<ElementApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<ElementApi>>();
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new ElementApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(4));
                Assert.That(templates["getElementsByProjectCommit"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/elements"));
                Assert.That(templates["getElementByProjectCommitId"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/elements/{elementId:guid}"));
                Assert.That(templates["getProjectUsageByProjectCommitElement"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/elements/{elementId:guid}/projectUsage"));
                Assert.That(templates["getRootsByProjectCommit"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/roots"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getElementsByProjectCommit"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getElementByProjectCommitId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getProjectUsageByProjectCommitElement"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getRootsByProjectCommit"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetElementsByProjectCommit()
        {
            Assert.That(() => ElementApi.GetElementsByProjectCommit(null, ProjectId, CommitId, Mock.Of<ILogger<ElementApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ElementApi.GetElementsByProjectCommit(context, ProjectId, CommitId, this.logger.Object));

            invocation.AssertNotYetImplemented("getElementsByProjectCommit");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getElementsByProjectCommit");
        }

        [Test]
        public async Task VerifyGetElementByProjectCommitId()
        {
            Assert.That(() => ElementApi.GetElementByProjectCommitId(null, ProjectId, CommitId, ElementId, Mock.Of<ILogger<ElementApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ElementApi.GetElementByProjectCommitId(context, ProjectId, CommitId, ElementId, this.logger.Object));

            invocation.AssertNotYetImplemented("getElementByProjectCommitId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getElementByProjectCommitId");
        }

        [Test]
        public async Task VerifyGetProjectUsageByProjectCommitElement()
        {
            Assert.That(() => ElementApi.GetProjectUsageByProjectCommitElement(null, ProjectId, CommitId, ElementId, Mock.Of<ILogger<ElementApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ElementApi.GetProjectUsageByProjectCommitElement(context, ProjectId, CommitId, ElementId, this.logger.Object));

            invocation.AssertNotYetImplemented("getProjectUsageByProjectCommitElement");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getProjectUsageByProjectCommitElement");
        }

        [Test]
        public async Task VerifyGetRootsByProjectCommit()
        {
            Assert.That(() => ElementApi.GetRootsByProjectCommit(null, ProjectId, CommitId, Mock.Of<ILogger<ElementApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ElementApi.GetRootsByProjectCommit(context, ProjectId, CommitId, this.logger.Object));

            invocation.AssertNotYetImplemented("getRootsByProjectCommit");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getRootsByProjectCommit");
        }
    }
}
