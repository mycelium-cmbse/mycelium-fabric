// ------------------------------------------------------------------------------------------------
//  <copyright file="ExternalRelationshipServiceTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Services
{
    using System;
    using System.Collections.Generic;
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
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class ExternalRelationshipServiceTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid HeadCommitId = Guid.Parse("44444444-4444-4444-4444-111111111111");

        private static readonly Guid ForeignCommitId = Guid.Parse("44444444-4444-4444-4444-333333333333");

        private static readonly Guid ElementId = Guid.Parse("55555555-5555-5555-5555-000000000001");

        private static readonly Guid ExternalDataId = Guid.Parse("88888888-8888-8888-8888-888888888888");

        private ServiceMocks serviceMocks;

        private Mock<ILogger<ExternalRelationshipService>> logger;

        private Mock<IDataRepository> dataRepository;

        private Mock<IProjectDataVersioningService> versioningService;

        private ExternalRelationshipService service;

        [SetUp]
        public void SetUp()
        {
            this.serviceMocks = new ServiceMocks(ProjectId);
            this.logger = new Mock<ILogger<ExternalRelationshipService>>();
            this.logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            this.dataRepository = new Mock<IDataRepository>();
            this.versioningService = new Mock<IProjectDataVersioningService>();

            this.versioningService.Setup(x => x.GetHeadCommitAsync(ProjectId, BranchId, It.IsAny<CancellationToken>())).ReturnsAsync(new Commit { Id = HeadCommitId });

            this.versioningService.Setup(x => x.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Commit { Id = Guid.NewGuid() });

            this.dataRepository.Setup(x => x.ReadByIdAsync<IElement>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("element.notFound"));

            this.dataRepository.Setup(x => x.ReadByIdAsync<IElement>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, ElementId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PartUsage { Id = ElementId });

            this.dataRepository.Setup(x => x.ReadByIdAsync<ExternalData>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("externalData.notFound"));

            this.dataRepository.Setup(x => x.ReadByIdAsync<ExternalData>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, ExternalDataId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalData { Id = ExternalDataId });

            this.serviceMocks.AddCommit(HeadCommitId, ProjectId);
            this.serviceMocks.AddCommit(ForeignCommitId, Guid.NewGuid());

            this.service = new ExternalRelationshipService(this.serviceMocks.DatabaseHandler.Object, this.serviceMocks.ProjectScope.Object,
                this.serviceMocks.ProjectRepository.Object, this.serviceMocks.CommitRepository.Object, this.dataRepository.Object, this.versioningService.Object,
                this.logger.Object);
        }

        [Test]
        public async Task VerifyCreateExternalRelationshipAsync()
        {
            Assert.That(() => this.service.CreateExternalRelationshipAsync(ProjectId, BranchId, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            Assert.That((await this.service.CreateExternalRelationshipAsync(ProjectId, BranchId, new ExternalRelationship { ElementEnd = ElementId }, CancellationToken.None))
                .FirstError.Code, Is.EqualTo("externalRelationship.end.missing"));

            var unknownElement = new ExternalRelationship { ElementEnd = Guid.NewGuid(), ExternalDataEnd = ExternalDataId };

            Assert.That((await this.service.CreateExternalRelationshipAsync(ProjectId, BranchId, unknownElement, CancellationToken.None)).FirstError.Code,
                Is.EqualTo("externalRelationship.elementEnd.invalid"));


            this.versioningService.Verify(x => x.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()),
                Times.Never);

            this.serviceMocks.Reset();

            var relationship = new ExternalRelationship { ElementEnd = ElementId, ExternalDataEnd = ExternalDataId };

            var created = await this.service.CreateExternalRelationshipAsync(ProjectId, BranchId, relationship, CancellationToken.None);

            this.dataRepository.Verify(x => x.ReadByIdAsync<ExternalData>(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(created.IsError, Is.False);
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadWrite);

                this.versioningService.Verify(x => x.CreateCommitAsync(
                    It.Is<IReadOnlyList<DataVersion>>(change => change.Count == 1 && change[0].Payload != relationship && change[0].Payload.Id != Guid.Empty
                                                                && ((ExternalRelationship)change[0].Payload).ElementEnd == ElementId),
                    BranchId, It.Is<Commit>(template => template.OwningProject == ProjectId), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task CreateExternalRelationshipAsync_WithIdentifierTakenAtTheHead_IsAConflict()
        {
            var takenId = Guid.NewGuid();

            this.dataRepository.Setup(x => x.ReadByIdAsync<SysML2.NET.Common.IData>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, takenId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalRelationship { Id = takenId });

            var existing = await this.service.CreateExternalRelationshipAsync(ProjectId, BranchId,
                new ExternalRelationship { Id = takenId, ElementEnd = ElementId, ExternalDataEnd = ExternalDataId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(existing.FirstError.Type, Is.EqualTo(ErrorType.Conflict));
                this.versioningService.Verify(x => x.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<Commit>(),
                    It.IsAny<CancellationToken>()),
                    Times.Never);
            }
        }

        [Test]
        public async Task VerifyGetExternalRelationshipsAsync()
        {
            var page = new PagedResult<ExternalRelationship> { Records = [new ExternalRelationship { Id = Guid.NewGuid(), ElementEnd = ElementId }] };

            this.dataRepository.Setup(x => x.ReadAsync<ExternalRelationship>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(page);

            Assert.That((await this.service.GetExternalRelationshipsAsync(ProjectId, HeadCommitId, new QueryParameters(), CancellationToken.None)).Value, Is.SameAs(page));

            Assert.That((await this.service.GetExternalRelationshipsAsync(ProjectId, ForeignCommitId, new QueryParameters(), CancellationToken.None)).FirstError.Code,
                Is.EqualTo("commit.invalid"));

            this.dataRepository.Verify(x => x.ReadAsync<ExternalRelationship>(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task VerifyGetExternalRelationshipByIdAsync()
        {
            var relationshipId = Guid.NewGuid();

            this.dataRepository.Setup(x => x.ReadByIdAsync<ExternalRelationship>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("externalRelationship.notFound"));

            this.dataRepository.Setup(x => x.ReadByIdAsync<ExternalRelationship>(It.IsAny<IDataStoreTransaction>(), HeadCommitId, relationshipId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExternalRelationship { Id = relationshipId });

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.GetExternalRelationshipByIdAsync(ProjectId, HeadCommitId, relationshipId, CancellationToken.None)).Value.Id,
                    Is.EqualTo(relationshipId));
                Assert.That((await this.service.GetExternalRelationshipByIdAsync(ProjectId, HeadCommitId, Guid.NewGuid(), CancellationToken.None)).FirstError.Type,
                    Is.EqualTo(ErrorType.NotFound));
                Assert.That((await this.service.GetExternalRelationshipByIdAsync(ProjectId, ForeignCommitId, relationshipId, CancellationToken.None)).FirstError.Type,
                    Is.EqualTo(ErrorType.Validation));
            }
        }

        [Test]
        public async Task VerifyGetExternalRelationshipsByElementAsync()
        {
            var page = new PagedResult<ExternalRelationship> { Records = [new ExternalRelationship { Id = Guid.NewGuid(), ElementEnd = ElementId }] };

            this.dataRepository.Setup(x => x.ReadExternalRelationshipsByElementAsync(It.IsAny<IDataStoreTransaction>(), HeadCommitId, ElementId, It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(page);

            Assert.That((await this.service.GetExternalRelationshipsByElementAsync(ProjectId, HeadCommitId, ElementId, new QueryParameters(), CancellationToken.None)).Value,
                Is.SameAs(page));

            Assert.That((await this.service.GetExternalRelationshipsByElementAsync(ProjectId, ForeignCommitId, ElementId, new QueryParameters(), CancellationToken.None))
                .FirstError.Code, Is.EqualTo("commit.invalid"));
        }
    }
}
