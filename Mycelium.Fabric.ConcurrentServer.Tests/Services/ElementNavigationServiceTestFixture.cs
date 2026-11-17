// ------------------------------------------------------------------------------------------------
//  <copyright file="ElementNavigationServiceTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.Core.DTO.Systems.Parts;
    using SysML2.NET.PIM;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class ElementNavigationServiceTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid UnknownProjectId = Guid.Parse("11111111-1111-1111-1111-999999999999");

        private static readonly Guid CommitId = Guid.Parse("44444444-4444-4444-4444-111111111111");

        private static readonly Guid ForeignCommitId = Guid.Parse("44444444-4444-4444-4444-333333333333");

        private static readonly Guid ElementId = Guid.Parse("55555555-5555-5555-5555-000000000001");

        private ServiceMocks serviceMocks;

        private Mock<ILogger<ElementNavigationService>> logger;

        private Mock<IDataRepository> dataRepository;

        private Mock<IProjectDataVersioningService> versioningService;

        private ElementNavigationService service;

        [SetUp]
        public void SetUp()
        {
            this.serviceMocks = new ServiceMocks(ProjectId);
            this.logger = new Mock<ILogger<ElementNavigationService>>();
            this.logger.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            this.dataRepository = new Mock<IDataRepository>();
            this.versioningService = new Mock<IProjectDataVersioningService>();

            this.serviceMocks.AddCommit(CommitId, ProjectId);
            this.serviceMocks.AddCommit(ForeignCommitId, Guid.NewGuid());

            this.dataRepository.Setup(repository => repository.ReadByIdAsync<IElement>(It.IsAny<IDataStoreTransaction>(), CommitId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("element.notFound"));

            this.dataRepository.Setup(repository => repository.ReadByIdAsync<IElement>(It.IsAny<IDataStoreTransaction>(), CommitId, ElementId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartUsage { Id = ElementId });

            this.dataRepository.Setup(repository => repository.ReadRelationshipsAsync(It.IsAny<IDataStoreTransaction>(), CommitId, ElementId, Direction.@out, It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new PagedResult<IRelationship>());

            this.service = new ElementNavigationService(this.serviceMocks.DatabaseHandler.Object, this.serviceMocks.ProjectScope.Object,
                this.serviceMocks.ProjectRepository.Object, this.serviceMocks.CommitRepository.Object, this.dataRepository.Object, this.versioningService.Object,
                this.logger.Object);
        }

        [Test]
        public async Task VerifyGetElementsAsync()
        {
            var page = new PagedResult<IElement> { Records = [new PartUsage { Id = ElementId }] };

            this.dataRepository.Setup(repository => repository.ReadAsync<IElement>(It.IsAny<IDataStoreTransaction>(), CommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(page);

            var elements = await this.service.GetElementsAsync(ProjectId, CommitId, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(elements.Value, Is.SameAs(page));
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadOnly);
            }

            var foreignCommit = await this.service.GetElementsAsync(ProjectId, ForeignCommitId, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(foreignCommit.FirstError.Code, Is.EqualTo("commit.invalid"));
                this.dataRepository.Verify(repository => repository.ReadAsync<IElement>(It.IsAny<IDataStoreTransaction>(), ForeignCommitId, It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()),
                    Times.Never);
            }

            this.serviceMocks.ProjectScope.Setup(scope => scope.ProjectId).Returns(UnknownProjectId);

            var unknownProject = await this.service.GetElementsAsync(UnknownProjectId, CommitId, new QueryParameters(), CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unknownProject.FirstError.Code, Is.EqualTo("project.notFound"));
                this.dataRepository.Verify(repository => repository.ReadAsync<IElement>(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<QueryParameters>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task VerifyGetRelationshipsByRelatedElementAsync()
        {
            Assert.That(() => this.service.GetRelationshipsByRelatedElementAsync(Guid.NewGuid(), CommitId, ElementId, Direction.@out, new QueryParameters(),
                CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            var relationships = await this.service.GetRelationshipsByRelatedElementAsync(ProjectId, CommitId, ElementId, Direction.@out, new QueryParameters(),
                CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(relationships.IsError, Is.False);
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadOnly);
            }

            var unknownElement = await this.service.GetRelationshipsByRelatedElementAsync(ProjectId, CommitId, Guid.NewGuid(), Direction.@out, new QueryParameters(),
                CancellationToken.None);

            Assert.That(unknownElement.FirstError.Code, Is.EqualTo("element.notFound"));

            var foreignCommit = await this.service.GetRelationshipsByRelatedElementAsync(ProjectId, ForeignCommitId, ElementId, Direction.@out, new QueryParameters(),
                CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(foreignCommit.FirstError.Type, Is.EqualTo(ErrorType.Validation));

                this.dataRepository.Verify(repository => repository.ReadRelationshipsAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Direction>(),
                    It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }
    }
}
