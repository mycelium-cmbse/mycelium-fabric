// ------------------------------------------------------------------------------------------------
//  <copyright file="QueryServiceTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;
    using Mycelium.Fabric.ConcurrentServer.Services;
    using Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers;

    using SysML2.NET.Common;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class QueryServiceTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid HeadCommitId = Guid.Parse("44444444-4444-4444-4444-111111111111");

        private static readonly Guid ForeignCommitId = Guid.Parse("44444444-4444-4444-4444-333333333333");

        private static readonly Guid QueryId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        private static readonly Guid ScopedElementId = Guid.Parse("55555555-5555-5555-5555-000000000001");

        private ServiceMocks serviceMocks;

        private Mock<ILogger<QueryService>> logger;

        private Mock<IQueryRepository> queryRepository;

        private Mock<IProjectRepository> projectRepository;

        private Mock<IDataRepository> dataRepository;

        private Mock<IProjectDataVersioningService> versioningService;

        private QueryService service;

        [SetUp]
        public void SetUp()
        {
            this.serviceMocks = new ServiceMocks(ProjectId);
            this.logger = new Mock<ILogger<QueryService>>();
            this.logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            this.queryRepository = new Mock<IQueryRepository>();
            this.projectRepository = new Mock<IProjectRepository>();
            this.dataRepository = new Mock<IDataRepository>();
            this.versioningService = new Mock<IProjectDataVersioningService>();

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Project { Id = ProjectId });

            this.queryRepository.Setup(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Query>());

            this.queryRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Query>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Query created, CancellationToken _) => created);

            this.versioningService.Setup(x => x.GetHeadCommitAsync(ProjectId, null, It.IsAny<CancellationToken>())).ReturnsAsync(new Commit { Id = HeadCommitId });
            this.serviceMocks.AddCommit(ForeignCommitId, Guid.NewGuid());

            this.dataRepository.Setup(x => x.ReadAsync<IData>(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<IData>());

            this.dataRepository.Setup(x => x.ReadMatchingAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new PagedResult<IData>());

            this.service = new QueryService(this.serviceMocks.DatabaseHandler.Object, this.serviceMocks.ProjectScope.Object, this.queryRepository.Object,
                this.projectRepository.Object, this.serviceMocks.CommitRepository.Object, this.dataRepository.Object, this.versioningService.Object, this.logger.Object);
        }

        [Test]
        public async Task VerifyExecuteQueryAsync()
        {
            Assert.That(() => this.service.ExecuteQueryAsync(null, null, new QueryParameters(), CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => this.service.ExecuteQueryAsync(new Query { Project = Guid.NewGuid() }, null, new QueryParameters(), CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => this.service.ExecuteQueryAsync(new Query { Project = ProjectId, Select = ["declaredName"] }, null, new QueryParameters(), CancellationToken.None),
                Throws.TypeOf<NotSupportedException>());

            this.serviceMocks.Reset();

            var unconstrained = await this.service.ExecuteQueryAsync(new Query { Project = ProjectId }, null, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unconstrained.IsError, Is.False);
                this.dataRepository.Verify(x => x.ReadAsync<IData>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()), Times.Once);
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadOnly);
            }

            var constrained = new Query
            {
                Project = ProjectId,
                Where = new PrimitiveConstraint { Property = "declaredName", Operator = Operator.equalto, Value = ["the part"] },
                Scope = [ScopedElementId]
            };

            await this.service.ExecuteQueryAsync(constrained, null, new QueryParameters(), CancellationToken.None);

            this.dataRepository.Verify(x => x.ReadMatchingAsync(It.IsAny<IDataStoreTransaction>(), HeadCommitId,
                It.Is<CompositeSearchFilter>(filter => filter.Operator == JoinOperator.AND && filter.Filters.Count == 2
                                                       && ((PrimitiveSearchFilter)filter.Filters[0]).Property == "declaredName"
                                                       && ((PrimitiveSearchFilter)filter.Filters[1]).Property == "id"),
                It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);

            var lonelyComposite = new Query { Project = ProjectId, Where = new CompositeConstraint { Constraint = [constrained.Where], Operator = JoinOperator.OR } };

            Assert.That((await this.service.ExecuteQueryAsync(lonelyComposite, null, new QueryParameters(), CancellationToken.None)).FirstError.Code,
                Is.EqualTo("query.where.composite.invalid"));

            var foreignCommit = await this.service.ExecuteQueryAsync(new Query { Project = ProjectId }, ForeignCommitId, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(foreignCommit.FirstError.Type, Is.EqualTo(ErrorType.Validation));
                this.dataRepository.Verify(x => x.ReadAsync<IData>(It.IsAny<IDataStoreTransaction>(), ForeignCommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()),
                    Times.Never);
            }
        }

        [Test]
        public async Task VerifyExecuteQueryByIdAsync()
        {
            this.queryRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Query { Id = QueryId, Project = Guid.NewGuid() });

            var foreignQuery = await this.service.ExecuteQueryByIdAsync(QueryId, null, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(foreignQuery.FirstError.Code, Is.EqualTo("query.notFound"));
                this.versioningService.Verify(x => x.GetHeadCommitAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            this.queryRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Query { Id = QueryId, Project = ProjectId });

            var executed = await this.service.ExecuteQueryByIdAsync(QueryId, null, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(executed.IsError, Is.False);
                this.dataRepository.Verify(x => x.ReadAsync<IData>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task VerifyCreateQueryAsync()
        {
            Assert.That(() => this.service.CreateQueryAsync(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            var created = await this.service.CreateQueryAsync(new Query { Project = ProjectId, Name = "parts", Select = ["declaredName"] }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(created.Value.Id, Is.Not.EqualTo(Guid.Empty));
                Assert.That(created.Value.Project, Is.EqualTo(ProjectId));
                Assert.That(created.Value.Alias, Does.Contain("parts"));
                Assert.That(created.Value.Select, Is.EqualTo(["declaredName"]));
            }

            this.queryRepository.Setup(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Query> { Records = [created.Value] });

            var homonym = await this.service.CreateQueryAsync(new Query { Project = ProjectId, Name = "parts" }, CancellationToken.None);

            Assert.That(homonym.FirstError.Type, Is.EqualTo(ErrorType.Conflict));

            var invalidWhere = await this.service.CreateQueryAsync(new Query { Project = ProjectId, Where = new PrimitiveConstraint() }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(invalidWhere.FirstError.Code, Is.EqualTo("query.where.property.missing"));
                this.queryRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Query>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task VerifyUpdateQueryAsync()
        {
            var stored = new Query { Id = QueryId, Project = ProjectId, Name = "parts" };
            var other = new Query { Id = Guid.NewGuid(), Project = ProjectId, Name = "ports" };

            this.queryRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), QueryId, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

            this.queryRepository.Setup(x => x.UpdateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Query>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Query updated, CancellationToken _) => updated);

            this.queryRepository.Setup(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Query> { Records = [stored] });

            var sameName = await this.service.UpdateQueryAsync(ProjectId, new Query { Id = QueryId, Project = ProjectId, Name = "parts", Select = ["declaredName"] },
                CancellationToken.None);

            Assert.That(sameName.Value.Select, Is.EqualTo(["declaredName"]));

            this.queryRepository.Setup(x => x.ReadByFilterAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<SearchFilter>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PagedResult<Query> { Records = [other] });

            var takenName = await this.service.UpdateQueryAsync(ProjectId, new Query { Id = QueryId, Project = ProjectId, Name = "ports" }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(takenName.FirstError.Type, Is.EqualTo(ErrorType.Conflict));
                this.queryRepository.Verify(x => x.UpdateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Query>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task VerifyDeleteQueryAsync()
        {
            var foreignQueryId = Guid.NewGuid();

            this.queryRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), QueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Query { Id = QueryId, Project = ProjectId });

            this.queryRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), foreignQueryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Query { Id = foreignQueryId, Project = Guid.NewGuid() });

            this.queryRepository.Setup(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Deleted);

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.DeleteQueryAsync(ProjectId, QueryId, CancellationToken.None)).Value.Id, Is.EqualTo(QueryId));
                Assert.That((await this.service.DeleteQueryAsync(ProjectId, foreignQueryId, CancellationToken.None)).FirstError.Code, Is.EqualTo("query.notFound"));
                this.queryRepository.Verify(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), foreignQueryId, It.IsAny<CancellationToken>()), Times.Never);
            }
        }
    }
}
