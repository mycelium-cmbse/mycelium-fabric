// ------------------------------------------------------------------------------------------------
//  <copyright file="QueryApiTestFixture.cs" company="Starion Group S.A.">
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
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class QueryApiTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid QueryId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid CommitId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid ResultId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        private const string CompositeWhereBody = """
                                                  { "@type": "Query", "name": "Parts", "select": [ "name" ],
                                                    "where": { "@type": "CompositeConstraint", "operator": "and", "constraint": [] } }
                                                  """;

        private const string PrimitiveWhereBody = """
                                                  { "@type": "Query", "name": "Parts", "select": [ "name" ],
                                                    "where": { "@type": "PrimitiveConstraint", "operator": "=", "property": "@type", "value": [ "PartDefinition" ] } }
                                                  """;

        private Mock<ILogger<QueryApi>> logger;

        private Mock<IQueryService> queryService;

        [SetUp]
        public void SetUp()
        {
            EndpointHost.ResetMocks();

            this.logger = EndpointHost.Logger<QueryApi>();

            this.queryService = EndpointHost.QueryService;
        }

        private static Query Record()
        {
            return new Query
            {
                Id = QueryId,
                Project = ProjectId,
                Name = "Parts",
                Select = ["name"],
                Where = new CompositeConstraint { Operator = JoinOperator.AND }
            };
        }

        private static PagedResult<IData> Results()
        {
            return new PagedResult<IData> { Records = [new PartDefinition { Id = ResultId, DeclaredName = "Vehicle" }] };
        }

        [Test]
        public void VerifyAddRoutes()
        {
            var app = new TestEndpointRouteBuilder();

            Assert.That(app.RouteEndpoints(), Is.Empty);

            new QueryApi().AddRoutes(app);

            var templates = app.TemplatesByEndpointName();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates, Has.Count.EqualTo(8));
                Assert.That(templates["getQueriesByProject"], Is.EqualTo("/projects/{projectId:guid}/queries"));
                Assert.That(templates["postQueryByProject"], Is.EqualTo("/projects/{projectId:guid}/queries"));
                Assert.That(templates["getQueryByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}"));
                Assert.That(templates["putQueryByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}"));
                Assert.That(templates["deleteQueryByProjectAndId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}"));
                Assert.That(templates["getQueryResultsByProjectIdQueryId"], Is.EqualTo("/projects/{projectId:guid}/queries/{queryId:guid}/results"));
                Assert.That(templates["getQueryResultsByProjectIdQuery"], Is.EqualTo("/projects/{projectId:guid}/query-results"));
                Assert.That(templates["getQueryResultsByProjectIdQueryPost"], Is.EqualTo("/projects/{projectId:guid}/query-results"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(app.DeclaredStatusCodes("getQueriesByProject"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("postQueryByProject"), Is.EqualTo([201, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryByProjectAndId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("putQueryByProjectAndId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("deleteQueryByProjectAndId"), Is.EqualTo([200, 204, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryResultsByProjectIdQueryId"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryResultsByProjectIdQuery"), Is.EqualTo([200, 404, 500]));
                Assert.That(app.DeclaredStatusCodes("getQueryResultsByProjectIdQueryPost"), Is.EqualTo([200, 404, 500]));
            }
        }

        [Test]
        public async Task VerifyGetQueriesByProject()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/queries");

            invocation.AssertNotYetImplemented("getQueriesByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getQueriesByProject");

            this.queryService
                .Setup(service => service.GetQueriesAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Query> { Records = [Record()] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries");

            invocation.AssertPayload(StatusCodes.Status200OK, QueryId.ToString());

            // The PIM record carries no owningProject, so the route supplies it.
            Assert.That(invocation.Payload()[0].GetProperty("owningProject").GetProperty("@id").GetString(), Is.EqualTo(ProjectId.ToString()));

            this.queryService
                .Setup(service => service.GetQueriesAsync(ProjectId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Query>());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries");

            Assert.That(invocation.Body, Is.EqualTo("[]"));
        }

        [Test]
        public async Task VerifyPostQueryByProject()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Post, $"/projects/{ProjectId}/queries");

            invocation.AssertNotYetImplemented("postQueryByProject");

            EndpointInvocation.VerifyInformationLogged(this.logger, "postQueryByProject");

            this.queryService
                .Setup(service => service.CreateQueryAsync("Parts", ProjectId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<IData>>(), It.IsAny<Constraint>(),
                    It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/queries", CompositeWhereBody);

            invocation.AssertPayload(StatusCodes.Status201Created, QueryId.ToString());

            // scope and orderBy have no counterpart in the QueryRequest JSON model of the PSM.
            this.queryService.Verify(service => service.CreateQueryAsync("Parts", ProjectId, It.Is<IReadOnlyList<string>>(select => select.Count == 1 && select[0] == "name"),
                It.Is<IReadOnlyList<IData>>(scope => scope.Count == 0), It.IsAny<CompositeConstraint>(), It.Is<IReadOnlyList<string>>(orderBy => orderBy.Count == 0),
                It.IsAny<CancellationToken>()), Times.Once);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/queries", PrimitiveWhereBody);

            invocation.AssertPayload(StatusCodes.Status201Created, QueryId.ToString());

            this.queryService.Verify(service => service.CreateQueryAsync("Parts", ProjectId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<IData>>(),
                It.IsAny<PrimitiveConstraint>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/queries", "not json at all");

            invocation.AssertError(StatusCodes.Status400BadRequest, "postQueryByProject");

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/queries", CompositeWhereBody, "application/msgpack");

            invocation.AssertError(StatusCodes.Status415UnsupportedMediaType, "postQueryByProject");
        }

        [Test]
        public async Task VerifyGetQueryByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}");

            invocation.AssertNotYetImplemented("getQueryByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getQueryByProjectAndId");

            this.queryService
                .Setup(service => service.GetQueryByIdAsync(ProjectId, QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}");

            invocation.AssertPayload(StatusCodes.Status200OK, QueryId.ToString());

            this.queryService
                .Setup(service => service.GetQueryByIdAsync(ProjectId, QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("query.notFound", "No query with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}");

            invocation.AssertError(StatusCodes.Status404NotFound, "getQueryByProjectAndId");
        }

        [Test]
        public async Task VerifyPutQueryByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Put, $"/projects/{ProjectId}/queries/{QueryId}");

            invocation.AssertNotYetImplemented("putQueryByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "putQueryByProjectAndId");

            this.queryService
                .Setup(service => service.UpdateQueryAsync(ProjectId, It.IsAny<Query>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Put, $"/projects/{ProjectId}/queries/{QueryId}", CompositeWhereBody);

            invocation.AssertPayload(StatusCodes.Status200OK, QueryId.ToString());

            // The route's queryId identifies the record the body replaces.
            this.queryService.Verify(service => service.UpdateQueryAsync(ProjectId, It.Is<Query>(query => query.Id == QueryId && query.Name == "Parts"),
                It.IsAny<CancellationToken>()), Times.Once);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Put, $"/projects/{ProjectId}/queries/{QueryId}", PrimitiveWhereBody);

            invocation.AssertPayload(StatusCodes.Status200OK, QueryId.ToString());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Put, $"/projects/{ProjectId}/queries/{QueryId}", "not json at all");

            invocation.AssertError(StatusCodes.Status400BadRequest, "putQueryByProjectAndId");
        }

        [Test]
        public async Task VerifyDeleteQueryByProjectAndId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Delete, $"/projects/{ProjectId}/queries/{QueryId}");

            invocation.AssertNotYetImplemented("deleteQueryByProjectAndId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "deleteQueryByProjectAndId");

            this.queryService
                .Setup(service => service.DeleteQueryAsync(ProjectId, QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/queries/{QueryId}");

            invocation.AssertPayload(StatusCodes.Status200OK, QueryId.ToString());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/queries/{QueryId}", prefer: "return=minimal");

            invocation.AssertMinimalReturn();

            this.queryService
                .Setup(service => service.DeleteQueryAsync(ProjectId, QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("query.notFound", "No query with the given id exists."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Delete, $"/projects/{ProjectId}/queries/{QueryId}", prefer: "return=minimal");

            invocation.AssertError(StatusCodes.Status404NotFound, "deleteQueryByProjectAndId");
        }

        [Test]
        public async Task VerifyGetQueryResultsByProjectIdQueryId()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}/results");

            invocation.AssertNotYetImplemented("getQueryResultsByProjectIdQueryId");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getQueryResultsByProjectIdQueryId");

            this.queryService
                .Setup(service => service.GetQueryByIdAsync(ProjectId, QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Record());

            this.queryService
                .Setup(service => service.ExecuteQueryByIdAsync(QueryId, It.IsAny<Guid?>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Results());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}/results?commitId={CommitId}");

            invocation.AssertPayload(StatusCodes.Status200OK, ResultId.ToString());

            this.queryService.Verify(service => service.ExecuteQueryByIdAsync(QueryId, CommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            // An omitted commitId leaves the service to fall back to the head of the default branch.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}/results");

            invocation.AssertPayload(StatusCodes.Status200OK, ResultId.ToString());

            this.queryService.Verify(service => service.ExecuteQueryByIdAsync(QueryId, null, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}/results?commitId=not-a-guid");

            invocation.AssertError(StatusCodes.Status400BadRequest, "getQueryResultsByProjectIdQueryId");

            // §8.1.3 realizes the ProjectUsage and ExternalRelationship reads through this endpoint, so a
            // result may be PIM API data, which is written as its PSM counterpart.
            this.queryService
                .Setup(service => service.ExecuteQueryByIdAsync(QueryId, It.IsAny<Guid?>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<IData> { Records = [new ProjectUsage { Id = ResultId, UsedProject = ProjectId, UsedProjectCommit = CommitId }] });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}/results");

            invocation.AssertPayload(StatusCodes.Status200OK, ResultId.ToString());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.Payload()[0].GetProperty("@type").GetString(), Is.EqualTo("ProjectUsage"));
                Assert.That(invocation.Payload()[0].GetProperty("usedProject").GetProperty("@id").GetString(), Is.EqualTo(ProjectId.ToString()));
            }

            // executeQueryById takes no project, so a query outside the project of the route is not found.
            this.queryService
                .Setup(service => service.GetQueryByIdAsync(ProjectId, QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("query.notFound", "No query with the given id exists in the project."));

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/queries/{QueryId}/results");

            invocation.AssertError(StatusCodes.Status404NotFound, "getQueryResultsByProjectIdQueryId");

            this.queryService.Verify(service => service.ExecuteQueryByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        [Test]
        public async Task VerifyGetQueryResultsByProjectIdQuery()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Get, $"/projects/{ProjectId}/query-results");

            invocation.AssertNotYetImplemented("getQueryResultsByProjectIdQuery");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getQueryResultsByProjectIdQuery");

            this.queryService
                .Setup(service => service.ExecuteQueryAsync(It.IsAny<Query>(), It.IsAny<Guid?>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Results());

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/query-results?commitId={CommitId}", CompositeWhereBody);

            invocation.AssertPayload(StatusCodes.Status200OK, ResultId.ToString());

            // The query is supplied in the body, so no Query record is created.
            this.queryService.Verify(service => service.ExecuteQueryAsync(It.Is<Query>(query => query.Id == Guid.Empty && query.Name == "Parts"), CommitId,
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            // §7.2.4 returns Data [0..*], so SysML Core elements and PIM API data may share one result.
            var usageId = Guid.NewGuid();

            this.queryService
                .Setup(service => service.ExecuteQueryAsync(It.IsAny<Query>(), It.IsAny<Guid?>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<IData>
                {
                    Records =
                    [
                        new PartDefinition { Id = ResultId, DeclaredName = "Vehicle" },
                        new ProjectUsage { Id = usageId, UsedProject = ProjectId, UsedProjectCommit = CommitId }
                    ]
                });

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/query-results", CompositeWhereBody);

            invocation.AssertPayload(StatusCodes.Status200OK, ResultId.ToString(), usageId.ToString());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invocation.Payload()[0].GetProperty("@type").GetString(), Is.EqualTo("PartDefinition"));
                Assert.That(invocation.Payload()[1].GetProperty("@type").GetString(), Is.EqualTo("ProjectUsage"));
            }

            foreach (var invalid in new[] { "not json at all", """{ "@type": "Query", "select": [ "name" ] }""" })
            {
                invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Get, $"/projects/{ProjectId}/query-results", invalid);

                invocation.AssertError(StatusCodes.Status400BadRequest, "getQueryResultsByProjectIdQuery");
            }
        }

        [Test]
        public async Task VerifyGetQueryResultsByProjectIdQueryPost()
        {
            var invocation = await EndpointInvocation.SendAsync(EndpointHost.WithoutServices, HttpMethod.Post, $"/projects/{ProjectId}/query-results");

            invocation.AssertNotYetImplemented("getQueryResultsByProjectIdQueryPost");

            EndpointInvocation.VerifyInformationLogged(this.logger, "getQueryResultsByProjectIdQueryPost");

            this.queryService
                .Setup(service => service.ExecuteQueryAsync(It.IsAny<Query>(), It.IsAny<Guid?>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Results());

            // §8.1.3 declares the POST binding interchangeable with the GET one.
            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/query-results", CompositeWhereBody);

            invocation.AssertPayload(StatusCodes.Status200OK, ResultId.ToString());

            this.queryService.Verify(service => service.ExecuteQueryAsync(It.IsAny<Query>(), null, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            invocation = await EndpointInvocation.SendAsync(EndpointHost.Client, HttpMethod.Post, $"/projects/{ProjectId}/query-results", """{ "name": "no discriminator" }""");

            invocation.AssertError(StatusCodes.Status400BadRequest, "getQueryResultsByProjectIdQueryPost");
        }
    }
}
