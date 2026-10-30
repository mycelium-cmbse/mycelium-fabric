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
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Services;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class CommitApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid ChangeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid BranchId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        private static readonly Guid PreviousCommitId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        private Mock<ILogger<CommitApi>> logger;

        private Mock<IProjectDataVersioningService> versioningService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<CommitApi>();

            this.versioningService = EndpointHost.VersioningService;
        }

        private static Commit CommitRecord()
        {
            return new Commit
            {
                Id = CommitId,
                OwningProject = ProjectId,
                PreviousCommits = [PreviousCommitId],
                Created = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        private static DataVersion ChangeRecord()
        {
            return new DataVersion
            {
                Id = ChangeId,
                Commit = CommitId,
                Identity = new DataIdentity { Id = Guid.Parse("66666666-6666-6666-6666-666666666666") }
            };
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
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits");

            invocation.AssertNotYetImplemented("getCommitsByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getCommitsByProject");

            this.versioningService
                .Setup(service => service.GetCommitsAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Commit> { Records = [CommitRecord()] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits");

            invocation.AssertPayload(StatusCodes.Status200OK, CommitId.ToString());

            Assert.That(invocation.Payload()[0].GetProperty("previousCommit").GetArrayLength(), Is.EqualTo(1));

            var initial = CommitRecord();
            initial.PreviousCommits = [];

            this.versioningService
                .Setup(service => service.GetCommitsAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Commit> { Records = [initial] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits");

            Assert.That(invocation.Payload()[0].GetProperty("previousCommit").GetArrayLength(), Is.EqualTo(0));
        }

        [Test]
        public async Task VerifyPostCommitByProject()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Post, $"/projects/{ProjectId}/commits");

            invocation.AssertNotYetImplemented("postCommitByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "postCommitByProject");

            this.versioningService
                .Setup(service => service.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<Guid>>(), ProjectId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommitRecord());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/commits?branchId={BranchId}",
                """{ "@type": "Commit", "change": [ { "@type": "DataVersion", "payload": { "@type": "PartDefinition", "name": "Vehicle" } } ] }""");

            invocation.AssertPayload(StatusCodes.Status201Created, CommitId.ToString());

            // The branch is taken from the query string and the change set from the body.
            this.versioningService.Verify(service => service.CreateCommitAsync(It.Is<IReadOnlyList<DataVersion>>(change => change.Count == 1 && change[0].Payload != null),
                BranchId, It.Is<IReadOnlyList<Guid>>(previous => previous.Count == 0), ProjectId, It.IsAny<CancellationToken>()), Times.Once);

            // §7.2.3 takes change as DataVersion [1..*], and §8.1.3 deletes the identity named by a version without payload.
            foreach (var invalid in new[]
                     {
                         """{ "@type": "Commit", "change": [] }""",
                         """{ "@type": "Commit", "change": [ { "@type": "DataVersion", "payload": null } ] }""",
                         """{ "@type": "Commit", "change": [ { "@type": "DataVersion", "identity": { "@type": "DataIdentity" }, "payload": null } ] }"""
                     })
            {
                invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/commits", invalid);

                invocation.AssertError(StatusCodes.Status400BadRequest, "postCommitByProject");
            }

            this.versioningService.Verify(service => service.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

            // A malformed branchId is rejected before the body is read.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/commits?branchId=not-a-guid",
                """{ "@type": "Commit" }""");

            invocation.AssertError(StatusCodes.Status400BadRequest, "postCommitByProject");
        }

        [Test]
        public async Task VerifyGetCommitByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}");

            invocation.AssertNotYetImplemented("getCommitByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getCommitByProjectAndId");

            this.versioningService
                .Setup(service => service.GetCommitByIdAsync(ProjectId, CommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CommitRecord());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}");

            invocation.AssertPayload(StatusCodes.Status200OK, CommitId.ToString());

            this.versioningService
                .Setup(service => service.GetCommitByIdAsync(ProjectId, CommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("commit.notFound", "No commit with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "getCommitByProjectAndId");
        }

        [Test]
        public async Task VerifyGetChangesByProjectCommit()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/changes");

            invocation.AssertNotYetImplemented("getChangesByProjectCommit");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getChangesByProjectCommit");

            this.versioningService
                .Setup(service => service.GetCommitChangeAsync(ProjectId, CommitId, It.IsAny<IReadOnlyList<ChangeType>>(), It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<DataVersion> { Records = [ChangeRecord()] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/changes?changeTypes=CREATED,DELETED");

            invocation.AssertPayload(StatusCodes.Status200OK, ChangeId.ToString());

            this.versioningService.Verify(service => service.GetCommitChangeAsync(ProjectId, CommitId,
                It.Is<IReadOnlyList<ChangeType>>(types => types.Count == 2 && types[0] == ChangeType.CREATED && types[1] == ChangeType.DELETED), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // An absent changeTypes is an empty collection, which §7.2.3 reads as "no filter".
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/changes");

            invocation.AssertPayload(StatusCodes.Status200OK, ChangeId.ToString());

            this.versioningService.Verify(service => service.GetCommitChangeAsync(ProjectId, CommitId, It.Is<IReadOnlyList<ChangeType>>(types => types.Count == 0),
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/changes?changeTypes=RENAMED");

            invocation.AssertError(StatusCodes.Status400BadRequest, "getChangesByProjectCommit");
        }

        [Test]
        public async Task VerifyGetChangeByProjectCommitId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/changes/{ChangeId}");

            invocation.AssertNotYetImplemented("getChangeByProjectCommitId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getChangeByProjectCommitId");

            this.versioningService
                .Setup(service => service.GetCommitChangeByIdAsync(ProjectId, CommitId, ChangeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ChangeRecord());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/changes/{ChangeId}");

            invocation.AssertPayload(StatusCodes.Status200OK, ChangeId.ToString());

            this.versioningService
                .Setup(service => service.GetCommitChangeByIdAsync(ProjectId, CommitId, ChangeId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("change.notFound", "No change with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/changes/{ChangeId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "getChangeByProjectCommitId");
        }
    }
}
