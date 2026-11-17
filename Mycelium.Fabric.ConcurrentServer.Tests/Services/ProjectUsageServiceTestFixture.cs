// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectUsageServiceTestFixture.cs" company="Starion Group S.A.">
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

    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    [TestFixture]
    public class ProjectUsageServiceTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid UsedProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid OwnCommitId = Guid.Parse("44444444-4444-4444-4444-111111111111");

        private static readonly Guid UsedCommitId = Guid.Parse("44444444-4444-4444-4444-333333333333");

        private static readonly Guid UsageId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        private ServiceMocks serviceMocks;

        private Mock<ILogger<ProjectUsageService>> logger;

        private Mock<IDataRepository> dataRepository;

        private Mock<IProjectDataVersioningService> versioningService;

        private Mock<IProjectUsageRepository> projectUsageRepository;

        private ProjectUsageService service;

        [SetUp]
        public void SetUp()
        {
            this.serviceMocks = new ServiceMocks(ProjectId);
            this.logger = new Mock<ILogger<ProjectUsageService>>();
            this.logger.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            this.dataRepository = new Mock<IDataRepository>();

            this.dataRepository.Setup(repository => repository.ReadIdentityOwnerAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("dataIdentity.notFound"));
            this.versioningService = new Mock<IProjectDataVersioningService>();
            this.projectUsageRepository = new Mock<IProjectUsageRepository>();

            this.projectUsageRepository.Setup(repository => repository.ReadUsedCommitAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("commit.notFound"));

            this.projectUsageRepository.Setup(repository => repository.ReadUsedCommitAsync(It.IsAny<IDataStoreTransaction>(), UsedCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Commit { Id = UsedCommitId, OwningProject = UsedProjectId });

            this.projectUsageRepository.Setup(repository => repository.ReadUsedCommitAsync(It.IsAny<IDataStoreTransaction>(), OwnCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Commit { Id = OwnCommitId, OwningProject = ProjectId });

            this.versioningService.Setup(service => service.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Commit { Id = Guid.NewGuid(), OwningProject = ProjectId });

            this.service = new ProjectUsageService(this.serviceMocks.DatabaseHandler.Object, this.serviceMocks.ProjectScope.Object, this.serviceMocks.ProjectRepository.Object,
                this.serviceMocks.CommitRepository.Object, this.dataRepository.Object,
                this.versioningService.Object, this.projectUsageRepository.Object, this.logger.Object);
        }

        [Test]
        public async Task VerifyCreateProjectUsageAsync()
        {
            Assert.That(() => this.service.CreateProjectUsageAsync(ProjectId, null, null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => this.service.CreateProjectUsageAsync(UsedProjectId, null, new ProjectUsage { UsedProjectCommit = UsedCommitId }, CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That((await this.service.CreateProjectUsageAsync(ProjectId, null, new ProjectUsage(), CancellationToken.None)).FirstError.Code,
                Is.EqualTo("projectUsage.usedProjectCommit.missing"));

            Assert.That((await this.service.CreateProjectUsageAsync(ProjectId, null, new ProjectUsage { UsedProjectCommit = Guid.NewGuid() },
                CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));

            Assert.That((await this.service.CreateProjectUsageAsync(ProjectId, null, new ProjectUsage { UsedProjectCommit = OwnCommitId }, CancellationToken.None)).FirstError.Code,
                Is.EqualTo("projectUsage.usedProject.invalid"));

            var mismatch = new ProjectUsage { UsedProjectCommit = UsedCommitId, UsedProject = Guid.NewGuid() };

            Assert.That((await this.service.CreateProjectUsageAsync(ProjectId, null, mismatch, CancellationToken.None)).FirstError.Code,
                Is.EqualTo("projectUsage.usedProject.invalid"));

            this.versioningService.Verify(service => service.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()),
                Times.Never);

            this.versioningService.Setup(service => service.GetHeadCommitAsync(ProjectId, null, It.IsAny<CancellationToken>())).ReturnsAsync(new Commit { Id = OwnCommitId });

            var usage = new ProjectUsage { UsedProjectCommit = UsedCommitId };

            var commit = await this.service.CreateProjectUsageAsync(ProjectId, null, usage, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(commit.IsError, Is.False);
                Assert.That(usage.UsedProject, Is.EqualTo(Guid.Empty));

                this.versioningService.Verify(service => service.CreateCommitAsync(
                    It.Is<IReadOnlyList<DataVersion>>(change => change.Count == 1 && change[0].Payload != usage && change[0].Payload.Id != Guid.Empty
                                                                && ((ProjectUsage)change[0].Payload).UsedProject == UsedProjectId
                                                                && ((ProjectUsage)change[0].Payload).UsedProjectCommit == UsedCommitId), null,
                    It.Is<Commit>(template => template.OwningProject == ProjectId), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task CreateProjectUsageAsync_WithIdentifierTakenAtTheHead_IsAConflict()
        {
            this.versioningService.Setup(service => service.GetHeadCommitAsync(ProjectId, null, It.IsAny<CancellationToken>())).ReturnsAsync(new Commit { Id = OwnCommitId });

            this.dataRepository.Setup(repository => repository.ReadByIdAsync<SysML2.NET.Common.IData>(It.IsAny<IDataStoreTransaction>(), OwnCommitId, UsageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProjectUsage { Id = UsageId });

            var existing = await this.service.CreateProjectUsageAsync(ProjectId, null, new ProjectUsage { Id = UsageId, UsedProjectCommit = UsedCommitId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(existing.FirstError.Type, Is.EqualTo(ErrorType.Conflict));
                this.versioningService.Verify(service => service.CreateCommitAsync(It.IsAny<IReadOnlyList<DataVersion>>(), It.IsAny<Guid?>(), It.IsAny<Commit>(),
                    It.IsAny<CancellationToken>()),
                    Times.Never);
            }
        }

        [Test]
        public async Task VerifyDeleteProjectUsageAsync()
        {
            this.versioningService.Setup(service => service.GetHeadCommitAsync(ProjectId, null, It.IsAny<CancellationToken>())).ReturnsAsync(new Commit { Id = OwnCommitId });

            this.dataRepository.Setup(repository => repository.ReadByIdAsync<ProjectUsage>(It.IsAny<IDataStoreTransaction>(), OwnCommitId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("projectUsage.notFound"));

            Assert.That((await this.service.DeleteProjectUsageAsync(ProjectId, null, UsageId, CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));

            this.dataRepository.Setup(repository => repository.ReadByIdAsync<ProjectUsage>(It.IsAny<IDataStoreTransaction>(), OwnCommitId, UsageId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProjectUsage { Id = UsageId });

            var commit = await this.service.DeleteProjectUsageAsync(ProjectId, null, UsageId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(commit.IsError, Is.False);

                this.versioningService.Verify(service => service.CreateCommitAsync(
                    It.Is<IReadOnlyList<DataVersion>>(change => change.Count == 1 && change[0].Identity.Id == UsageId && change[0].Payload == null), null,
                    It.IsAny<Commit>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task CreateProjectUsageAsync_WithNestedCommit_SharesOneTransaction()
        {
            var transaction = new Mock<IDataStoreTransaction>();
            transaction.Setup(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success);
            transaction.Setup(dataStoreTransaction => dataStoreTransaction.RollbackAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success);

            var transactionFactory = new Mock<IDataStoreTransactionFactory>();
            transactionFactory.Setup(factory => factory.BeginAsync(It.IsAny<TransactionMode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => ErrorOrFactory.From(transaction.Object));

            var databaseHandler = new DatabaseHandler(transactionFactory.Object);
            var branchId = Guid.NewGuid();
            var branch = new Branch { Id = branchId, OwningProject = ProjectId, Head = OwnCommitId };

            this.serviceMocks.ProjectRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Project { Id = ProjectId, DefaultBranch = branchId });

            var branchRepository = new Mock<IBranchRepository>();
            branchRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), branchId, It.IsAny<CancellationToken>())).ReturnsAsync(branch);

            var commitRepository = new Mock<ICommitRepository>();

            commitRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), OwnCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Commit { Id = OwnCommitId, OwningProject = ProjectId, Created = DateTime.UtcNow.AddMinutes(-1) });

            commitRepository.Setup(repository => repository.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Commit created, CancellationToken _) => created);

            var commitGraphRepository = new Mock<ICommitGraphRepository>();

            commitGraphRepository.Setup(repository => repository.ReadVersionedDataAsync(It.IsAny<IDataStoreTransaction>(), OwnCommitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ErrorOrFactory.From<IReadOnlyList<DataVersion>>(Array.Empty<DataVersion>()));

            commitGraphRepository.Setup(repository => repository.MoveHeadAsync(It.IsAny<IDataStoreTransaction>(), branchId, OwnCommitId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(branch);

            var dataIdentityRepository = new Mock<IDataIdentityRepository>();

            dataIdentityRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("dataIdentity.notFound"));

            dataIdentityRepository.Setup(repository => repository.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<DataIdentity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, DataIdentity created, CancellationToken _) => created);

            var dataVersionRepository = new Mock<IDataVersionRepository>();

            dataVersionRepository.Setup(repository => repository.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<DataVersion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Unexpected("store.failed"));

            var versioningLogger = new Mock<ILogger<ProjectDataVersioningService>>();
            versioningLogger.Setup(logger => logger.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

            var versioning = new ProjectDataVersioningService(databaseHandler, this.serviceMocks.ProjectScope.Object, this.serviceMocks.ProjectRepository.Object,
                branchRepository.Object, new Mock<ITagRepository>().Object, commitRepository.Object, dataVersionRepository.Object, dataIdentityRepository.Object,
                commitGraphRepository.Object, this.dataRepository.Object, versioningLogger.Object);

            var usageService = new ProjectUsageService(databaseHandler, this.serviceMocks.ProjectScope.Object, this.serviceMocks.ProjectRepository.Object,
                commitRepository.Object, this.dataRepository.Object, versioning, this.projectUsageRepository.Object, this.logger.Object);

            var failed = await usageService.CreateProjectUsageAsync(ProjectId, null, new ProjectUsage { UsedProjectCommit = UsedCommitId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failed.FirstError.Code, Is.EqualTo("store.failed"));
                Assert.That(databaseHandler.HasActiveTransaction, Is.False);
                transactionFactory.Verify(factory => factory.BeginAsync(TransactionMode.ReadWrite, It.IsAny<CancellationToken>()), Times.Once);
                transaction.Verify(dataStoreTransaction => dataStoreTransaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
                transaction.Verify(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
                this.logger.VerifyLogged(LogLevel.Error, "CreateProjectUsageAsync failed with store.failed", Times.Once());
                versioningLogger.VerifyLogged(LogLevel.Error, "failed with", Times.Never());
            }

            dataVersionRepository.Setup(repository => repository.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<DataVersion>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, DataVersion created, CancellationToken _) => created);

            var succeeded = await usageService.CreateProjectUsageAsync(ProjectId, null, new ProjectUsage { UsedProjectCommit = UsedCommitId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(succeeded.IsError, Is.False);
                transactionFactory.Verify(factory => factory.BeginAsync(It.IsAny<TransactionMode>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
                transaction.Verify(dataStoreTransaction => dataStoreTransaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [Test]
        public async Task VerifyGetProjectUsagesAsync()
        {
            var page = new PagedResult<ProjectUsage> { Records = [new ProjectUsage { Id = UsageId, UsedProject = UsedProjectId, UsedProjectCommit = UsedCommitId }] };

            this.serviceMocks.AddCommit(OwnCommitId, ProjectId);
            this.serviceMocks.AddCommit(UsedCommitId, UsedProjectId);

            this.dataRepository.Setup(repository => repository.ReadAsync<ProjectUsage>(It.IsAny<IDataStoreTransaction>(), OwnCommitId, It.IsAny<QueryParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(page);

            using (Assert.EnterMultipleScope())
            {
                Assert.That((await this.service.GetProjectUsagesAsync(ProjectId, OwnCommitId, new QueryParameters(), CancellationToken.None)).Value, Is.SameAs(page));
                Assert.That((await this.service.GetProjectUsagesAsync(ProjectId, UsedCommitId, new QueryParameters(), CancellationToken.None)).FirstError.Code,
                    Is.EqualTo("commit.invalid"));
            }

            this.dataRepository.Verify(repository => repository.ReadAsync<ProjectUsage>(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<QueryParameters>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
