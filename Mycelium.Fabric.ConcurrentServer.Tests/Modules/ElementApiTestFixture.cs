// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementApiTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PSM;

    [TestFixture]
    public class ElementApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid CommitId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid ElementId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private Mock<ILogger<ElementApi>> logger;

        private Mock<IElementNavigationService> navigationService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<ElementApi>();

            this.navigationService = EndpointHost.NavigationService;
        }

        private static ErrorOr<IElement> Record()
        {
            return new PartDefinition { Id = ElementId, DeclaredName = "Vehicle" };
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new ElementApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(4));
                Assert.That(templates["getElementsByProjectCommit"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/elements"));
                Assert.That(templates["getElementByProjectCommitId"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/elements/{elementId:guid}"));

                Assert.That(templates["getProjectUsageByProjectCommitElement"],
                    Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/elements/{elementId:guid}/projectUsage"));

                Assert.That(templates["getRootsByProjectCommit"], Is.EqualTo("/projects/{projectId:guid}/commits/{commitId:guid}/roots"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getElementsByProjectCommit"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getElementByProjectCommitId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getProjectUsageByProjectCommitElement"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getRootsByProjectCommit"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetElementsByProjectCommit()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/elements");

            invocation.AssertNotYetImplemented("getElementsByProjectCommit");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getElementsByProjectCommit");

            this.navigationService
                .Setup(service => service.GetElementsAsync(ProjectId, CommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<IElement> { Records = [Record().Value] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/elements");

            invocation.AssertPayload(StatusCodes.Status200OK, ElementId.ToString());

            // §8.1.2 pairs Element with itself, so the Core serializer emits its own metaclass name.
            Assert.That(invocation.Payload()[0].GetProperty("@type").GetString(), Is.EqualTo("PartDefinition"));

            this.navigationService
                .Setup(service => service.GetElementsAsync(ProjectId, CommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("commit.notFound", "No commit with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/elements");

            invocation.AssertError(StatusCodes.Status404NotFound, "getElementsByProjectCommit");
        }

        [Test]
        public async Task VerifyGetElementByProjectCommitId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/elements/{ElementId}");

            invocation.AssertNotYetImplemented("getElementByProjectCommitId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getElementByProjectCommitId");

            this.navigationService
                .Setup(service => service.GetElementByIdAsync(ProjectId, CommitId, ElementId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/elements/{ElementId}");

            invocation.AssertPayload(StatusCodes.Status200OK, ElementId.ToString());

            this.navigationService
                .Setup(service => service.GetElementByIdAsync(ProjectId, CommitId, ElementId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("element.notFound", "No element with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/elements/{ElementId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "getElementByProjectCommitId");
        }

        [Test]
        public async Task VerifyGetProjectUsageByProjectCommitElement()
        {
            // This route has no counterpart in §8.1.3, so it stays unimplemented even with the
            // ProjectUsageService registered.
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/elements/{ElementId}/projectUsage");

            invocation.AssertNotYetImplemented("getProjectUsageByProjectCommitElement");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getProjectUsageByProjectCommitElement");
        }

        [Test]
        public async Task VerifyGetRootsByProjectCommit()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/roots");

            invocation.AssertNotYetImplemented("getRootsByProjectCommit");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getRootsByProjectCommit");

            this.navigationService
                .Setup(service => service.GetRootElementsAsync(ProjectId, CommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<IElement> { Records = [Record().Value] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/roots");

            invocation.AssertPayload(StatusCodes.Status200OK, ElementId.ToString());

            this.navigationService
                .Setup(service => service.GetRootElementsAsync(ProjectId, CommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<IElement>());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/commits/{CommitId}/roots");

            Assert.That(invocation.Body, Is.EqualTo("[]"));
        }
    }
}
