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
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Services;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    using SysML2.NET.Core.DTO.Core.Features;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM;
    using SysML2.NET.PSM;

    [TestFixture]
    public class RelationshipApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid RelatedElementId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        private static readonly Guid RelationshipId = Guid.Parse("88888888-8888-8888-8888-888888888888");

        private Mock<ILogger<RelationshipApi>> logger;

        private Mock<IElementNavigationService> navigationService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<RelationshipApi>();

            this.navigationService = EndpointHost.NavigationService;
        }

        private static FeatureTyping Record()
        {
            return new FeatureTyping { Id = RelationshipId };
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
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get,
                $"/projects/{ProjectId}/commits/{CommitId}/elements/{RelatedElementId}/relationships");

            invocation.AssertNotYetImplemented("getRelationshipsByProjectCommitRelatedElement");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getRelationshipsByProjectCommitRelatedElement");

            this.navigationService
                .Setup(service => service.GetRelationshipsByRelatedElementAsync(ProjectId, CommitId, RelatedElementId, It.IsAny<Direction>(), It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<IRelationship> { Records = [Record()] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get,
                $"/projects/{ProjectId}/commits/{CommitId}/elements/{RelatedElementId}/relationships?direction=out");

            invocation.AssertPayload(StatusCodes.Status200OK, RelationshipId.ToString());

            this.navigationService.Verify(service => service.GetRelationshipsByRelatedElementAsync(ProjectId, CommitId, RelatedElementId, Direction.@out,
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            // An absent direction is served as both, the widest of the three allowable values.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get,
                $"/projects/{ProjectId}/commits/{CommitId}/elements/{RelatedElementId}/relationships");

            invocation.AssertPayload(StatusCodes.Status200OK, RelationshipId.ToString());

            this.navigationService.Verify(service => service.GetRelationshipsByRelatedElementAsync(ProjectId, CommitId, RelatedElementId, Direction.both,
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            // A direction outside {in, out, both} is rejected without reaching the service.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get,
                $"/projects/{ProjectId}/commits/{CommitId}/elements/{RelatedElementId}/relationships?direction=sideways");

            invocation.AssertError(StatusCodes.Status400BadRequest, "getRelationshipsByProjectCommitRelatedElement");

            this.navigationService.Verify(service => service.GetRelationshipsByRelatedElementAsync(ProjectId, CommitId, RelatedElementId, It.IsAny<Direction>(),
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
