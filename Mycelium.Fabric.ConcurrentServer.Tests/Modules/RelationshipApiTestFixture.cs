// ------------------------------------------------------------------------------------------------
//  <copyright file="RelationshipApiTestFixture.cs" company="Starion Group S.A.">
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
    /// Suite of tests for the <see cref="RelationshipApi"/> class.
    /// </summary>
    [TestFixture]
    public class RelationshipApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid RelatedElementId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        private Mock<ILogger<RelationshipApi>> logger;

        [SetUp]
        public void SetUp()
        {
            this.logger = new Mock<ILogger<RelationshipApi>>();
            this.logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        }
        

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new RelationshipApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(1));
                Assert.That(
                    templates["getRelationshipsByProjectCommitRelatedElement"],
                    Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/elements/{relatedElementId:guid}/relationships"));
                Assert.That(app.DeclaredStatusCodes("getRelationshipsByProjectCommitRelatedElement"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetRelationshipsByProjectCommitRelatedElement()
        {
            Assert.That(
                () => RelationshipApi.GetRelationshipsByProjectCommitRelatedElement(null, ProjectId, CommitId, RelatedElementId, Mock.Of<ILogger<RelationshipApi>>()),
                Throws.TypeOf<ArgumentNullException>());

            var invocation = await HandlerInvocation.CaptureAsync(
                context => RelationshipApi.GetRelationshipsByProjectCommitRelatedElement(context, ProjectId, CommitId, RelatedElementId, this.logger.Object));

            invocation.AssertNotYetImplemented("getRelationshipsByProjectCommitRelatedElement");

            HandlerInvocation.VerifyInformationLogged(this.logger, "getRelationshipsByProjectCommitRelatedElement");
        }
    }
}
