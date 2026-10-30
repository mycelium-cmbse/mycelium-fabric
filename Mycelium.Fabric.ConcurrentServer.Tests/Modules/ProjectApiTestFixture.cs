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
    public class ProjectApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private Mock<ILogger<ProjectApi>> logger;

        private Mock<IProjectService> projectService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<ProjectApi>();

            this.projectService = EndpointHost.ProjectService;
        }

        private static Project Record()
        {
            return new Project
            {
                Id = ProjectId,
                Name = "Hello SysML v2",
                Description = "A project",
                DefaultBranch = BranchId,
                Created = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc)
            };
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
            // No service registered: the route still answers the generated surface's placeholder.
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, "/projects");

            invocation.AssertNotYetImplemented("getProjects");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getProjects");

            this.projectService
                .Setup(service => service.GetProjectsAsync(It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Project> { Records = [Record()], Next = new Page { Epoch = 1, Identifier = ProjectId } });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, "/projects?page[size]=3");

            invocation.AssertPayload(StatusCodes.Status200OK, ProjectId.ToString());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.Payload()[0].GetProperty("@type").GetString(), Is.EqualTo("Project"));
                Assert.That(invocation.Payload()[0].GetProperty("defaultBranch").GetProperty("@id").GetString(), Is.EqualTo(BranchId.ToString()));
                Assert.That(invocation.Link, Does.Contain("rel=\"next\"").And.Contain("page[size]=3"));
                Assert.That(invocation.Link, Does.Not.Contain("rel=\"prev\""));
            }

            this.projectService.Verify(service => service.GetProjectsAsync(It.Is<QueryParameters>(parameters => parameters.PageSize == 3), It.IsAny<CancellationToken>()),
                Times.Once);

            // A malformed cursor is rejected without reaching the service.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, "/projects?page[after]=not-a-cursor");

            invocation.AssertError(StatusCodes.Status400BadRequest, "getProjects");

            this.projectService.Verify(service => service.GetProjectsAsync(It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            // A failure the handler did not anticipate is answered 500 rather than escaping to the host.
            this.projectService
                .Setup(service => service.GetProjectsAsync(It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("the store is unreachable"));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, "/projects");

            invocation.AssertError(StatusCodes.Status500InternalServerError, "getProjects");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.ErrorDescription, Does.Contain("The server did not complete the operation."));

                // The cause is recorded in the log, not put on the wire.
                Assert.That(invocation.ErrorDescription, Does.Not.Contain("the store is unreachable"));
            }

            EndpointInvocation.VerifyErrorLogged(this.logger, "getProjects");

            // A request the server rejects while handling it keeps the status of that rejection.
            this.projectService
                .Setup(service => service.GetProjectsAsync(It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, "/projects");

            invocation.AssertError(StatusCodes.Status413PayloadTooLarge, "getProjects");
        }

        [Test]
        public async Task VerifyPostProject()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Post, "/projects");

            invocation.AssertNotYetImplemented("postProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "postProject");

            this.projectService
                .Setup(service => service.CreateProjectAsync("SysML v2 API Start", "Getting started", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, "/projects",
                """{ "@type": "Project", "name": "SysML v2 API Start", "description": "Getting started" }""");

            invocation.AssertPayload(StatusCodes.Status201Created, ProjectId.ToString());

            this.projectService.Verify(service => service.CreateProjectAsync("SysML v2 API Start", "Getting started", It.IsAny<CancellationToken>()), Times.Once);

            // A body without the @type discriminator cannot be resolved to a request model.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, "/projects", """{ "name": "No discriminator" }""");

            invocation.AssertError(StatusCodes.Status400BadRequest, "postProject");
        }

        [Test]
        public async Task VerifyGetProjectById()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}");

            invocation.AssertNotYetImplemented("getProjectById");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getProjectById");

            this.projectService
                .Setup(service => service.GetProjectByIdAsync(ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}");

            invocation.AssertPayload(StatusCodes.Status200OK, ProjectId.ToString());

            // An unknown project is reported as a 404 rather than an empty 200.
            this.projectService
                .Setup(service => service.GetProjectByIdAsync(ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("project.notFound", "No project with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "getProjectById");

            Assert.That(invocation.ErrorDescription, Does.Contain("No project with the given id exists."));
        }

        [Test]
        public async Task VerifyPutProjectById()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Put, $"/projects/{ProjectId}");

            invocation.AssertNotYetImplemented("putProjectById");

            EndpointInvocation.VerifyInformationLogged(this.logger, "putProjectById");

            this.projectService
                .Setup(service => service.UpdateProjectAsync(ProjectId, "Renamed", null, BranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            // The body also carries setDefaultBranch, which §8.1.3 maps onto this same endpoint.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Put, $"/projects/{ProjectId}",
                $$"""{ "@type": "Project", "name": "Renamed", "defaultBranch": { "@id": "{{BranchId}}" } }""");

            invocation.AssertPayload(StatusCodes.Status200OK, ProjectId.ToString());

            this.projectService.Verify(service => service.UpdateProjectAsync(ProjectId, "Renamed", null, BranchId, It.IsAny<CancellationToken>()), Times.Once);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Put, $"/projects/{ProjectId}", "not json at all");

            invocation.AssertError(StatusCodes.Status400BadRequest, "putProjectById");
        }

        [Test]
        public async Task VerifyDeleteProjectById()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Delete, $"/projects/{ProjectId}");

            invocation.AssertNotYetImplemented("deleteProjectById");

            EndpointInvocation.VerifyInformationLogged(this.logger, "deleteProjectById");

            this.projectService
                .Setup(service => service.DeleteProjectAsync(ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}");

            invocation.AssertPayload(StatusCodes.Status200OK, ProjectId.ToString());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}", prefer: "return=minimal");

            invocation.AssertMinimalReturn();

            // §7.2.1 leaves a project still used through a project usage undeletable, which is a conflict.
            this.projectService
                .Setup(service => service.DeleteProjectAsync(ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Conflict("project.inUse", "The project is used by another project."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}", prefer: "return=minimal");

            invocation.AssertError(StatusCodes.Status409Conflict, "deleteProjectById");
        }
    }
}
