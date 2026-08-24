// ------------------------------------------------------------------------------------------------
//  <copyright file="BranchApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="BranchApi"/> class.
    /// </summary>
    [TestFixture]
    public class BranchApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private Mock<ILogger<BranchApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<BranchApi>>();
            this.logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new BranchApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(4));
                Assert.That(templates["getBranchesByProject"], Is.EqualTo("/projects/{projectId:guid}/branches"));
                Assert.That(templates["postBranchByProject"], Is.EqualTo("/projects/{projectId:guid}/branches"));
                Assert.That(templates["getBranchesByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/branches/{branchId:guid}"));
                Assert.That(templates["deleteBranchByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/branches/{branchId:guid}"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getBranchesByProject"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("postBranchByProject"), Is.EqualTo([201, 500]));
                Assert.That(app.DeclaredStatusCodes("getBranchesByProjectAndId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("deleteBranchByProjectAndId"), Is.EqualTo([200, 204, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetBranchesByProject()
        {
            Assert.That(() => BranchApi.GetBranchesByProject(null, ProjectId, Mock.Of<ILogger<BranchApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => BranchApi.GetBranchesByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("getBranchesByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getBranchesByProject");
        }

        [Test]
        public async Task VerifyPostBranchByProject()
        {
            Assert.That(() => BranchApi.PostBranchByProject(null, ProjectId, Mock.Of<ILogger<BranchApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => BranchApi.PostBranchByProject(context, ProjectId, this.logger.Object));

            invocation.AssertNotYetImplemented("postBranchByProject");

            HandlerInvocation.VerifyInformationLogged(this.logger, "postBranchByProject");
        }

        [Test]
        public async Task VerifyGetBranchesByProjectAndId()
        {
            Assert.That(() => BranchApi.GetBranchesByProjectAndId(null, ProjectId, BranchId, Mock.Of<ILogger<BranchApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => BranchApi.GetBranchesByProjectAndId(context, ProjectId, BranchId, this.logger.Object));

            invocation.AssertNotYetImplemented("getBranchesByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getBranchesByProjectAndId");
        }

        [Test]
        public async Task VerifyDeleteBranchByProjectAndId()
        {
            Assert.That(() => BranchApi.DeleteBranchByProjectAndId(null, ProjectId, BranchId, Mock.Of<ILogger<BranchApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => BranchApi.DeleteBranchByProjectAndId(context, ProjectId, BranchId, this.logger.Object));

            invocation.AssertNotYetImplemented("deleteBranchByProjectAndId");

            HandlerInvocation.VerifyInformationLogged(this.logger, "deleteBranchByProjectAndId");
        }
    }
}
