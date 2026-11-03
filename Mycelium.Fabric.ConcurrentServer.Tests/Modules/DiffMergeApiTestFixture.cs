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

    using SysML2.NET.Common;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class DiffMergeApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid TargetBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid CompareCommitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid BaseCommitId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        private static readonly Guid MergeCommitId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        private static readonly Guid ConflictId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        private Mock<ILogger<DiffMergeApi>> logger;

        private Mock<IProjectDataVersioningService> versioningService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<DiffMergeApi>();

            this.versioningService = EndpointHost.VersioningService;
        }

        private void SetUpProjectScope()
        {
            this.versioningService
                .Setup(service => service.GetBranchByIdAsync(ProjectId, TargetBranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Branch { Id = TargetBranchId, OwningProject = ProjectId });

            foreach (var commitId in new[] { CompareCommitId, BaseCommitId })
            {
                this.versioningService
                    .Setup(service => service.GetCommitByIdAsync(ProjectId, commitId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Commit { Id = commitId, OwningProject = ProjectId });
            }
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
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Post, $"/projects/{ProjectId}/branches/{TargetBranchId}/merge");

            invocation.AssertNotYetImplemented("merge");

            EndpointInvocation.VerifyInformationLogged(this.logger, "merge");

            this.SetUpProjectScope();

            this.versioningService
                .Setup(service => service.MergeIntoBranchAsync(TargetBranchId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<IReadOnlyList<IData>>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MergeResult { MergeCommit = MergeCommitId });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post,
                $"/projects/{ProjectId}/branches/{TargetBranchId}/merge?sourceCommitId={CompareCommitId}&description=Reconciled");

            // §8.1.2 maps MergeResult.mergeCommit onto a Commit, which is the body of a successful merge.
            invocation.AssertPayload(StatusCodes.Status201Created, MergeCommitId.ToString());

            this.versioningService.Verify(service => service.MergeIntoBranchAsync(TargetBranchId,
                It.Is<IReadOnlyList<Guid>>(commits => commits.Count == 1 && commits[0] == CompareCommitId), It.Is<IReadOnlyList<IData>>(resolution => resolution.Count == 0),
                "Reconciled", It.IsAny<CancellationToken>()), Times.Once);

            // sourceCommitId is a comma-separated array, and a JSON null body carries no resolution.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post,
                $"/projects/{ProjectId}/branches/{TargetBranchId}/merge?sourceCommitId={CompareCommitId},{BaseCommitId}", "null");

            invocation.AssertPayload(StatusCodes.Status201Created, MergeCommitId.ToString());

            this.versioningService.Verify(service => service.MergeIntoBranchAsync(TargetBranchId,
                It.Is<IReadOnlyList<Guid>>(commits => commits.Count == 2 && commits[0] == CompareCommitId && commits[1] == BaseCommitId),
                It.Is<IReadOnlyList<IData>>(resolution => resolution.Count == 0), null, It.IsAny<CancellationToken>()), Times.Once);

            // An unsuccessful merge reports MergeResult.conflict as DataIdentity records.
            this.versioningService
                .Setup(service => service.MergeIntoBranchAsync(TargetBranchId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<IReadOnlyList<IData>>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MergeResult { Conflict = [ConflictId] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post,
                $"/projects/{ProjectId}/branches/{TargetBranchId}/merge?sourceCommitId={CompareCommitId}");

            invocation.AssertPayload(StatusCodes.Status409Conflict, ConflictId.ToString());

            Assert.That(invocation.Payload()[0].GetProperty("@type").GetString(), Is.EqualTo("DataIdentity"));

            foreach (var query in new[] { string.Empty, $"?commitId={CompareCommitId}", "?sourceCommitId=not-a-guid" })
            {
                invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/branches/{TargetBranchId}/merge{query}");

                invocation.AssertError(StatusCodes.Status400BadRequest, "merge");
            }

            // mergeIntoBranch takes no project, so a commit or branch outside the project of the route is not found.
            this.versioningService
                .Setup(service => service.GetCommitByIdAsync(ProjectId, BaseCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("commit.notFound", "No commit with the given id exists in the project."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post,
                $"/projects/{ProjectId}/branches/{TargetBranchId}/merge?sourceCommitId={BaseCommitId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "merge");

            this.versioningService
                .Setup(service => service.GetBranchByIdAsync(ProjectId, TargetBranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("branch.notFound", "No branch with the given id exists in the project."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post,
                $"/projects/{ProjectId}/branches/{TargetBranchId}/merge?sourceCommitId={CompareCommitId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "merge");

            this.versioningService.Verify(service => service.MergeIntoBranchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<IReadOnlyList<IData>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Test]
        public async Task VerifyDiff()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CompareCommitId}/diff");

            invocation.AssertNotYetImplemented("diff");

            EndpointInvocation.VerifyInformationLogged(this.logger, "diff");

            this.SetUpProjectScope();

            this.versioningService
                .Setup(service => service.DiffCommitsAsync(BaseCommitId, CompareCommitId, It.IsAny<IReadOnlyList<ChangeType>>(), It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<DataDifference> { Records = [new DataDifference { CompareData = ConflictId }] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get,
                $"/projects/{ProjectId}/commits/{CompareCommitId}/diff?baseCommitId={BaseCommitId}&changeTypes=CREATED");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.StatusCode, Is.EqualTo(StatusCodes.Status200OK));

                // §7.2.3 leaves baseData empty for data created in the compare commit only.
                Assert.That(invocation.Payload()[0].GetProperty("compareData").GetProperty("@id").GetString(), Is.EqualTo(ConflictId.ToString()));
                Assert.That(invocation.Payload()[0].GetProperty("baseData").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
            }

            this.versioningService.Verify(service => service.DiffCommitsAsync(BaseCommitId, CompareCommitId,
                    It.Is<IReadOnlyList<ChangeType>>(types => types.Count == 1 && types[0] == ChangeType.CREATED), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // baseCommit is a required input of diffCommits, and a change type is named, never numbered.
            foreach (var query in new[] { string.Empty, $"?baseCommitId={BaseCommitId}&changeTypes=1" })
            {
                invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CompareCommitId}/diff{query}");

                invocation.AssertError(StatusCodes.Status400BadRequest, "diff");
            }

            // diffCommits takes no project, so a commit outside the project of the route is not found.
            this.versioningService
                .Setup(service => service.GetCommitByIdAsync(ProjectId, BaseCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("commit.notFound", "No commit with the given id exists in the project."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get,
                $"/projects/{ProjectId}/commits/{CompareCommitId}/diff?baseCommitId={BaseCommitId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "diff");

            this.versioningService.Verify(service => service.DiffCommitsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ChangeType>>(),
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
