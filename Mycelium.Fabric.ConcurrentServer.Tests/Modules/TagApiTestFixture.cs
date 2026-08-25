// ------------------------------------------------------------------------------------------------
//  <copyright file="TagApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="TagApi"/> class.
    /// </summary>
    [TestFixture]
    public class TagApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid TagId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        private Mock<ILogger<TagApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<TagApi>>();
            this.logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new TagApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(4));
                Assert.That(templates["getTagsByProject"], Is.EqualTo("/projects/{projectId:guid}/tags"));
                Assert.That(templates["postTagByProject"], Is.EqualTo("/projects/{projectId:guid}/tags"));
                Assert.That(templates["getTagByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/tags/{tagId:guid}"));
                Assert.That(templates["deleteTagByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/tags/{tagId:guid}"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getTagsByProject"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("postTagByProject"), Is.EqualTo([201, 500]));
                Assert.That(app.DeclaredStatusCodes("getTagByProjectAndId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("deleteTagByProjectAndId"), Is.EqualTo([200, 204, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetTagsByProject()
        {
            Assert.That(() => TagApi.GetTagsByProject(null, ProjectId, Mock.Of<ILogger<TagApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => TagApi.GetTagsByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("getTagsByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getTagsByProject");
        }

        [Test]
        public async Task VerifyPostTagByProject()
        {
            Assert.That(() => TagApi.PostTagByProject(null, ProjectId, Mock.Of<ILogger<TagApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => TagApi.PostTagByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("postTagByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "postTagByProject");
        }

        [Test]
        public async Task VerifyGetTagByProjectAndId()
        {
            Assert.That(() => TagApi.GetTagByProjectAndId(null, ProjectId, TagId, Mock.Of<ILogger<TagApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => TagApi.GetTagByProjectAndId(context, ProjectId, TagId, this.logger.Object));

            invocation.AssertNotYetImplemented("getTagByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getTagByProjectAndId");
        }

        [Test]
        public async Task VerifyDeleteTagByProjectAndId()
        {
            Assert.That(() => TagApi.DeleteTagByProjectAndId(null, ProjectId, TagId, Mock.Of<ILogger<TagApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => TagApi.DeleteTagByProjectAndId(context, ProjectId, TagId, this.logger.Object));

            invocation.AssertNotYetImplemented("deleteTagByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "deleteTagByProjectAndId");
        }
    }
}
