// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectScopeMiddlewareTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Http
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Patterns;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Http;
    using Mycelium.Fabric.ConcurrentServer.Modules;
    using Mycelium.Fabric.ConcurrentServer.Persistence;

    [TestFixture]
    public class ProjectScopeMiddlewareTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly string[] UnscopedOperations =
            ["getProjects", "postProject", "getProjectById", "putProjectById", "deleteProjectById", "getDatatypes", "getDatatypeById"];

        private static readonly string[] ScopedSequence = ["enter", "next", "dispose"];

        private List<string> calls;

        private Mock<IDisposable> exit;

        private Mock<RequestDelegate> next;

        private Mock<IProjectScope> projectScope;

        [SetUp]
        public void SetUp()
        {
            this.calls = [];

            this.exit = new Mock<IDisposable>();
            this.exit.Setup(exit => exit.Dispose()).Callback(() => this.calls.Add("dispose"));

            this.projectScope = new Mock<IProjectScope>();
            this.projectScope.Setup(scope => scope.Enter(It.IsAny<Guid>())).Callback(() => this.calls.Add("enter")).Returns(this.exit.Object);

            this.next = new Mock<RequestDelegate>();
            this.next.Setup(next => next(It.IsAny<HttpContext>())).Callback(() => this.calls.Add("next")).Returns(Task.CompletedTask);
        }

        [Test]
        public async Task VerifyInvokeAsync()
        {
            var middleware = new ProjectScopeMiddleware(this.next.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => middleware.InvokeAsync(null, this.projectScope.Object), Throws.TypeOf<ArgumentNullException>());
                Assert.That(() => middleware.InvokeAsync(new DefaultHttpContext(), null), Throws.TypeOf<ArgumentNullException>());
            }

            await middleware.InvokeAsync(new DefaultHttpContext(), this.projectScope.Object);

            Assert.That(this.calls, Is.EqualTo(["next"]));

            this.calls.Clear();
            await middleware.InvokeAsync(ContextFor("/projects/{projectId:guid}"), this.projectScope.Object);

            Assert.That(this.calls, Is.EqualTo(["next"]));

            this.calls.Clear();
            await middleware.InvokeAsync(ContextFor("/projects/{projectId:guid}/branches"), this.projectScope.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(this.calls, Is.EqualTo(ScopedSequence));
                this.projectScope.Verify(scope => scope.Enter(ProjectId), Times.Once);
            }

            this.calls.Clear();

            this.next.Setup(next => next(It.IsAny<HttpContext>())).Callback(() => this.calls.Add("next")).ThrowsAsync(new InvalidOperationException());

            Assert.That(() => middleware.InvokeAsync(ContextFor("/projects/{projectId:guid}/branches"), this.projectScope.Object), Throws.TypeOf<InvalidOperationException>());
            Assert.That(this.calls, Is.EqualTo(ScopedSequence));
        }

        [Test]
        public async Task VerifyInvokeAsyncOnEveryRoute()
        {
            using var application = new WebApplicationFactory<ApiBase>();

            var endpoints = application.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
            var middleware = new ProjectScopeMiddleware(this.next.Object);
            var unscoped = new List<string>();

            foreach (var endpoint in endpoints)
            {
                this.projectScope.Invocations.Clear();

                await middleware.InvokeAsync(ContextFor(endpoint), this.projectScope.Object);

                if (this.projectScope.Invocations.Count == 0)
                {
                    unscoped.Add(endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName);
                }
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(endpoints, Has.Count.EqualTo(35));
                Assert.That(unscoped, Is.EquivalentTo(UnscopedOperations));
            }
        }

        private static DefaultHttpContext ContextFor(string template)
        {
            return ContextFor(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(template), 0, EndpointMetadataCollection.Empty, template));
        }

        private static DefaultHttpContext ContextFor(Endpoint endpoint)
        {
            var context = new DefaultHttpContext();
            context.SetEndpoint(endpoint);
            context.Request.RouteValues[ProjectScopeMiddleware.ProjectIdParameter] = ProjectId.ToString();

            return context;
        }
    }
}
