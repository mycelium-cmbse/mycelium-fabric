// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="ProjectApi"/> class.
    /// </summary>
    [TestFixture]
    public class ProjectApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private Mock<ILogger<ProjectApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<ProjectApi>>();
            this.logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new ProjectApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(5));
                Assert.That(templates["getProjects"], Is.EqualTo("/projects"));
                Assert.That(templates["postProject"], Is.EqualTo("/projects"));
                Assert.That(templates["getProjectById"], Is.EqualTo("/projects/{projectId:guid}"));
                Assert.That(templates["putProjectById"], Is.EqualTo("/projects/{projectId:guid}"));
                Assert.That(templates["deleteProjectById"], Is.EqualTo("/projects/{projectId:guid}"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getProjects"), Is.EqualTo([200, 500]));
                Assert.That(app.DeclaredStatusCodes("postProject"), Is.EqualTo([201, 500]));
                Assert.That(app.DeclaredStatusCodes("getProjectById"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("putProjectById"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("deleteProjectById"), Is.EqualTo([200, 204, 500]));
            }
        }

        [Test]
        public async Task VerifyGetProjects()
        {
            Assert.That(() => ProjectApi.GetProjects(null, Mock.Of<ILogger<ProjectApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ProjectApi.GetProjects(context, this.logger.Object));

            invocation.AssertNotYetImplemented("getProjects");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getProjects");
        }

        [Test]
        public async Task VerifyPostProject()
        {
            Assert.That(() => ProjectApi.PostProject(null, Mock.Of<ILogger<ProjectApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ProjectApi.PostProject(context, this.logger.Object));

            invocation.AssertNotYetImplemented("postProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "postProject");
        }

        [Test]
        public async Task VerifyGetProjectById()
        {
            Assert.That(() => ProjectApi.GetProjectById(null, ProjectId, Mock.Of<ILogger<ProjectApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ProjectApi.GetProjectById(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("getProjectById");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getProjectById");
        }

        [Test]
        public async Task VerifyPutProjectById()
        {
            Assert.That(() => ProjectApi.PutProjectById(null, ProjectId, Mock.Of<ILogger<ProjectApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ProjectApi.PutProjectById(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("putProjectById");

            HandlerInvocation.VerifyInformationLogged(this.logger, "putProjectById");
        }

        [Test]
        public async Task VerifyDeleteProjectById()
        {
            Assert.That(() => ProjectApi.DeleteProjectById(null, ProjectId, Mock.Of<ILogger<ProjectApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => ProjectApi.DeleteProjectById(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("deleteProjectById");

            HandlerInvocation.VerifyInformationLogged(this.logger, "deleteProjectById");
        }
    }
}
