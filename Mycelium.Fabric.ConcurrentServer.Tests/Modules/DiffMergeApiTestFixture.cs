// ------------------------------------------------------------------------------------------------
//  <copyright file="DiffMergeApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="DiffMergeApi"/> class.
    /// </summary>
    [TestFixture]
    public class DiffMergeApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid TargetBranchId = Guid.Parse("88888888-8888-8888-8888-888888888888");

        private static readonly Guid CompareCommitId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        private Mock<ILogger<DiffMergeApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<DiffMergeApi>>();
            this.logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new DiffMergeApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(2));
                Assert.That(templates["merge"], Is.EqualTo("/projects/{projectId:guid}/branches/{targetBranchId:guid}/merge"));
                Assert.That(templates["diff"], Is.EqualTo("/projects/{projectId:guid}/commits/{compareCommitId:guid}/diff"));
                Assert.That(app.DeclaredStatusCodes("merge"), Is.EqualTo([201, 404, 409, 500]));
                Assert.That(app.DeclaredStatusCodes("diff"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyMerge()
        {
            Assert.That(() => DiffMergeApi.Merge(null, ProjectId, TargetBranchId, Mock.Of<ILogger<DiffMergeApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => DiffMergeApi.Merge(context, ProjectId, TargetBranchId, this.logger.Object));

            invocation.AssertNotYetImplemented("merge");

            HandlerInvocation.VerifyInformationLogged(this.logger, "merge");
        }

        [Test]
        public async Task VerifyDiff()
        {
            Assert.That(() => DiffMergeApi.Diff(null, ProjectId, CompareCommitId, Mock.Of<ILogger<DiffMergeApi>>()), Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(context => DiffMergeApi.Diff(context, ProjectId, CompareCommitId, this.logger.Object));

            invocation.AssertNotYetImplemented("diff");

            HandlerInvocation.VerifyInformationLogged(this.logger, "diff");
        }
    }
}
