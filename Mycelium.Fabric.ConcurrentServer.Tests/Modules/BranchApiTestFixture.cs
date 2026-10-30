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

    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class BranchApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid HeadId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private Mock<ILogger<BranchApi>> logger;
        private Mock<IProjectDataVersioningService> versioningService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<BranchApi>();

            this.versioningService = EndpointHost.VersioningService;
        }

        private static Branch Record()
        {
            return new Branch
            {
                Id = BranchId,
                Name = "main",
                Head = HeadId,
                OwningProject = ProjectId,
                Created = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)
            };
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
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/branches");

            invocation.AssertNotYetImplemented("getBranchesByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getBranchesByProject");

            this.versioningService
                .Setup(service => service.GetBranchesAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Branch> { Records = [Record()] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/branches");

            invocation.AssertPayload(StatusCodes.Status200OK, BranchId.ToString());

            using (Assert.EnterMultipleScope())
            {
                // §7.1.2 makes head the commit a Branch references, so both names carry it.
                Assert.That(invocation.Payload()[0].GetProperty("head").GetProperty("@id").GetString(), Is.EqualTo(HeadId.ToString()));
                Assert.That(invocation.Payload()[0].GetProperty("referencedCommit").GetProperty("@id").GetString(), Is.EqualTo(HeadId.ToString()));
                Assert.That(invocation.Link, Is.Empty);
            }

            // Every Accept value, including the *json media types the OpenAPI document does not declare, is answered by the SysML2.NET serializers.
            foreach (var accept in new[] { "*/*", "application/json", "text/html", "application/ld+json", "application/problem+json", "application/xml" })
            {
                invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/branches", accept: accept);

                invocation.AssertPayload(StatusCodes.Status200OK, BranchId.ToString());
            }

            // A path no route matches still answers with the negotiated OpenAPI Error.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, "/projects/not-a-guid/branches");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
                Assert.That(invocation.ContentType, Is.EqualTo("application/json"));
                Assert.That(invocation.Payload().GetProperty("@type").GetString(), Is.EqualTo("Error"));
                Assert.That(invocation.ErrorDescription, Does.Contain("/projects/not-a-guid/branches"));
            }

            // A method no route accepts is answered the same way.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Patch, $"/projects/{ProjectId}/branches");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.StatusCode, Is.EqualTo(StatusCodes.Status405MethodNotAllowed));
                Assert.That(invocation.ContentType, Is.EqualTo("application/json"));
                Assert.That(invocation.Payload().GetProperty("@type").GetString(), Is.EqualTo("Error"));
            }

            // An empty page is a well-formed empty array, not a 404.
            this.versioningService
                .Setup(service => service.GetBranchesAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Branch>());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/branches");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
                Assert.That(invocation.Body, Is.EqualTo("[]"));
            }
        }

        [Test]
        public async Task VerifyPostBranchByProject()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Post, $"/projects/{ProjectId}/branches");

            invocation.AssertNotYetImplemented("postBranchByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "postBranchByProject");

            this.versioningService
                .Setup(service => service.CreateBranchAsync(ProjectId, "feature", HeadId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/branches",
                $$"""{ "@type": "Branch", "name": "feature", "head": { "@id": "{{HeadId}}" } }""");

            invocation.AssertPayload(StatusCodes.Status201Created, BranchId.ToString());

            // BranchRequest requires both name and head, so a body lacking either never reaches the service.
            foreach (var incomplete in new[] { """{ "@type": "Branch", "name": "feature" }""", $$"""{ "@type": "Branch", "head": { "@id": "{{HeadId}}" } }""" })
            {
                invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/branches", incomplete);

                invocation.AssertError(StatusCodes.Status400BadRequest, "postBranchByProject");
            }

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/branches", """{ "@type": "Branch", "name": "feature" }""",
                "text/plain");

            invocation.AssertError(StatusCodes.Status415UnsupportedMediaType, "postBranchByProject");

            foreach (var malformed in new[] { "not json at all", """{ "@type": "BranchRequest", "name": "feature" }""" })
            {
                invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/branches", malformed);

                invocation.AssertError(StatusCodes.Status400BadRequest, "postBranchByProject");
            }

            this.versioningService.Verify(service => service.CreateBranchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test]
        public async Task VerifyGetBranchesByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/branches/{BranchId}");

            invocation.AssertNotYetImplemented("getBranchesByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getBranchesByProjectAndId");

            this.versioningService
                .Setup(service => service.GetBranchByIdAsync(ProjectId, BranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/branches/{BranchId}");

            invocation.AssertPayload(StatusCodes.Status200OK, BranchId.ToString());

            this.versioningService
                .Setup(service => service.GetBranchByIdAsync(ProjectId, BranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("branch.notFound", "No branch with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/branches/{BranchId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "getBranchesByProjectAndId");
        }

        [Test]
        public async Task VerifyDeleteBranchByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Delete, $"/projects/{ProjectId}/branches/{BranchId}");

            invocation.AssertNotYetImplemented("deleteBranchByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "deleteBranchByProjectAndId");

            var deleted = Record();
            deleted.Deleted = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

            this.versioningService
                .Setup(service => service.DeleteBranchAsync(ProjectId, BranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleted);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/branches/{BranchId}");

            invocation.AssertPayload(StatusCodes.Status200OK, BranchId.ToString());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/branches/{BranchId}", prefer: "return=minimal");

            invocation.AssertMinimalReturn();

            this.versioningService
                .Setup(service => service.DeleteBranchAsync(ProjectId, BranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("branch.notFound", "No branch with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/branches/{BranchId}", prefer: "return=minimal");

            invocation.AssertError(StatusCodes.Status404NotFound, "deleteBranchByProjectAndId");
        }
    }
}
