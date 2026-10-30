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
    public class TagApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid TagId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid TaggedCommitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private Mock<ILogger<TagApi>> logger;

        private Mock<IProjectDataVersioningService> versioningService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<TagApi>();

            this.versioningService = EndpointHost.VersioningService;
        }

        private static Tag Record()
        {
            return new Tag
            {
                Id = TagId,
                Name = "Release 1",
                TaggedCommit = TaggedCommitId,
                OwningProject = ProjectId,
                Created = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)
            };
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
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/tags");

            invocation.AssertNotYetImplemented("getTagsByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getTagsByProject");

            this.versioningService
                .Setup(service => service.GetTagsAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Tag> { Records = [Record()], Previous = new Page { Epoch = 2, Identifier = TagId } });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/tags");

            invocation.AssertPayload(StatusCodes.Status200OK, TagId.ToString());

            using (Assert.EnterMultipleScope())
            {
                // §7.1.2 makes taggedCommit the commit a Tag references, so both names carry it.
                Assert.That(invocation.Payload()[0].GetProperty("taggedCommit").GetProperty("@id").GetString(), Is.EqualTo(TaggedCommitId.ToString()));
                Assert.That(invocation.Payload()[0].GetProperty("referencedCommit").GetProperty("@id").GetString(), Is.EqualTo(TaggedCommitId.ToString()));
                Assert.That(invocation.Link, Does.Contain("rel=\"prev\""));
                Assert.That(invocation.Link, Does.Not.Contain("rel=\"next\""));
            }

            this.versioningService
                .Setup(service => service.GetTagsAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Tag>());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/tags");

            Assert.That(invocation.Body, Is.EqualTo("[]"));
        }

        [Test]
        public async Task VerifyPostTagByProject()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Post, $"/projects/{ProjectId}/tags");

            invocation.AssertNotYetImplemented("postTagByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "postTagByProject");

            this.versioningService
                .Setup(service => service.CreateTagAsync(ProjectId, "Release 1", TaggedCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/tags",
                $$"""{ "@type": "Tag", "name": "Release 1", "taggedCommit": { "@id": "{{TaggedCommitId}}" } }""");

            invocation.AssertPayload(StatusCodes.Status201Created, TagId.ToString());

            this.versioningService.Verify(service => service.CreateTagAsync(ProjectId, "Release 1", TaggedCommitId, It.IsAny<CancellationToken>()), Times.Once);

            // A tag name already taken in the project is a conflict, not a validation failure.
            this.versioningService
                .Setup(service => service.CreateTagAsync(ProjectId, "Release 1", TaggedCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Conflict("tag.duplicate", "A tag with the given name already exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/tags",
                $$"""{ "@type": "Tag", "name": "Release 1", "taggedCommit": { "@id": "{{TaggedCommitId}}" } }""");

            invocation.AssertError(StatusCodes.Status409Conflict, "postTagByProject");
        }

        [Test]
        public async Task VerifyGetTagByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/tags/{TagId}");

            invocation.AssertNotYetImplemented("getTagByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getTagByProjectAndId");

            this.versioningService
                .Setup(service => service.GetTagByIdAsync(ProjectId, TagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/tags/{TagId}");

            invocation.AssertPayload(StatusCodes.Status200OK, TagId.ToString());

            this.versioningService
                .Setup(service => service.GetTagByIdAsync(ProjectId, TagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("tag.notFound", "No tag with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/tags/{TagId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "getTagByProjectAndId");
        }

        [Test]
        public async Task VerifyDeleteTagByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Delete, $"/projects/{ProjectId}/tags/{TagId}");

            invocation.AssertNotYetImplemented("deleteTagByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "deleteTagByProjectAndId");

            this.versioningService
                .Setup(service => service.DeleteTagAsync(ProjectId, TagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/tags/{TagId}");

            invocation.AssertPayload(StatusCodes.Status200OK, TagId.ToString());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/tags/{TagId}", prefer: "return=minimal");

            invocation.AssertMinimalReturn();

            this.versioningService
                .Setup(service => service.DeleteTagAsync(ProjectId, TagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("tag.notFound", "No tag with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/tags/{TagId}", prefer: "return=minimal");

            invocation.AssertError(StatusCodes.Status404NotFound, "deleteTagByProjectAndId");
        }
    }
}
