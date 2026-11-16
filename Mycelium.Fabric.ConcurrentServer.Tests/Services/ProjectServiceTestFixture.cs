// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectServiceTestFixture.cs" company="Starion Group S.A.">
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

    [TestFixture]
    public class ProjectServiceTestFixture
    {
        private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid ForeignBranchId = Guid.Parse("33333333-3333-3333-3333-444444444444");

        private ServiceMocks serviceMocks;

        private Mock<ILogger<ProjectService>> logger;

        private Mock<IProjectRepository> projectRepository;

        private Mock<IBranchRepository> branchRepository;

        private Mock<ICommitRepository> commitRepository;

        private ProjectService service;

        [SetUp]
        public void SetUp()
        {
            this.serviceMocks = new ServiceMocks(ProjectId);
            this.logger = new Mock<ILogger<ProjectService>>();
            this.logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            this.projectRepository = new Mock<IProjectRepository>();
            this.branchRepository = new Mock<IBranchRepository>();
            this.commitRepository = new Mock<ICommitRepository>();

            this.projectRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Project created, CancellationToken _) => created);

            this.projectRepository.Setup(x => x.UpdateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Project>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Project updated, CancellationToken _) => updated);

            this.commitRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Commit created, CancellationToken _) => created);

            this.branchRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Branch>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDataStoreTransaction _, Branch created, CancellationToken _) => created);

            this.service = new ProjectService(this.serviceMocks.DatabaseHandler.Object, this.serviceMocks.ProjectScope.Object, this.projectRepository.Object,
                this.branchRepository.Object, this.commitRepository.Object, this.logger.Object);
        }

        [Test]
        public async Task VerifyCreateProjectAsync()
        {
            Assert.That(() => this.service.CreateProjectAsync(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            var unnamed = await this.service.CreateProjectAsync(new Project { Description = "no name" }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unnamed.FirstError.Code, Is.EqualTo("project.name.missing"));
                this.serviceMocks.DatabaseHandler.Verify(x => x.BeginTransactionAsync(It.IsAny<TransactionMode>(), It.IsAny<CancellationToken>()), Times.Never);
            }

            var created = await this.service.CreateProjectAsync(new Project { Name = "the project", Description = "the description" }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(created.Value.Name, Is.EqualTo("the project"));
                Assert.That(created.Value.Alias, Does.Contain("the project"));
                Assert.That(created.Value.Description, Is.EqualTo("the description"));
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadWrite);
            }

            this.branchRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                It.Is<Branch>(branch => branch.Id == created.Value.DefaultBranch && branch.Name == ProjectService.DefaultBranchName && branch.OwningProject == created.Value.Id
                                        && branch.Head != Guid.Empty), It.IsAny<CancellationToken>()), Times.Once);

            this.commitRepository.Verify(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(),
                It.Is<Commit>(commit => commit.OwningProject == created.Value.Id && commit.PreviousCommits.Count == 0 && commit.Change.Count == 0),
                    It.IsAny<CancellationToken>()), Times.Once);

            this.serviceMocks.Reset();

            this.commitRepository.Setup(x => x.CreateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Commit>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Unexpected("store.failed"));

            var failed = await this.service.CreateProjectAsync(new Project { Name = "the project" }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failed.FirstError.Code, Is.EqualTo("store.failed"));
                this.serviceMocks.VerifyRolledBack();
            }
        }

        [Test]
        public async Task VerifyGetProjectByIdAsync()
        {
            Assert.That(() => new ProjectService(this.serviceMocks.DatabaseHandler.Object, this.serviceMocks.ProjectScope.Object, this.projectRepository.Object,
                this.branchRepository.Object, this.commitRepository.Object, null), Throws.TypeOf<ArgumentNullException>());

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Project { Id = ProjectId });

            var project = await this.service.GetProjectByIdAsync(ProjectId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(project.Value.Id, Is.EqualTo(ProjectId));
                this.logger.VerifyLogged(LogLevel.Warning, "GetProjectByIdAsync", Times.Never());
                this.logger.VerifyLogged(LogLevel.Error, "GetProjectByIdAsync", Times.Never());
            }

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("project.notFound"));

            await this.service.GetProjectByIdAsync(ProjectId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                this.logger.VerifyLogged(LogLevel.Warning, "GetProjectByIdAsync failed with project.notFound", Times.Once());
                this.logger.VerifyLogged(LogLevel.Error, "GetProjectByIdAsync", Times.Never());
            }

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Error> { Error.NotFound("project.notFound"), Error.Unexpected("store.failed") });

            await this.service.GetProjectByIdAsync(ProjectId, CancellationToken.None);

            this.logger.VerifyLogged(LogLevel.Error, "GetProjectByIdAsync failed with project.notFound, store.failed", Times.Once());

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException());

            Assert.That(() => this.service.GetProjectByIdAsync(ProjectId, CancellationToken.None), Throws.TypeOf<TimeoutException>());

            this.logger.VerifyLogged(LogLevel.Error, "GetProjectByIdAsync threw TimeoutException", Times.Once());

            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            Assert.That(() => this.service.GetProjectByIdAsync(ProjectId, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());

            this.logger.VerifyLogged(LogLevel.Error, "threw OperationCanceledException", Times.Never());
        }

        [Test]
        public async Task VerifyUpdateProjectAsync()
        {
            Assert.That(() => this.service.UpdateProjectAsync(null, CancellationToken.None), Throws.TypeOf<ArgumentNullException>());

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("project.notFound"));

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Project { Id = ProjectId, Name = "old name", Alias = ["old name"], Description = "old description", DefaultBranch = BranchId });

            this.branchRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), BranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Branch { Id = BranchId, OwningProject = ProjectId });

            this.branchRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ForeignBranchId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Branch { Id = ForeignBranchId, OwningProject = Guid.NewGuid() });

            var unchanged = await this.service.UpdateProjectAsync(new Project { Id = ProjectId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unchanged.Value.Name, Is.EqualTo("old name"));
                Assert.That(unchanged.Value.Description, Is.EqualTo("old description"));
                Assert.That(unchanged.Value.DefaultBranch, Is.EqualTo(BranchId));
            }

            var renamed = await this.service.UpdateProjectAsync(new Project { Id = ProjectId, Name = "new name", DefaultBranch = BranchId }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(renamed.Value.Name, Is.EqualTo("new name"));
                Assert.That(renamed.Value.Alias, Is.EquivalentTo(["old name", "new name"]));
            }

            var foreignBranch = await this.service.UpdateProjectAsync(new Project { Id = ProjectId, DefaultBranch = ForeignBranchId }, CancellationToken.None);

            Assert.That(foreignBranch.FirstError.Type, Is.EqualTo(ErrorType.NotFound));

            var unknownProject = await this.service.UpdateProjectAsync(new Project { Id = Guid.NewGuid(), Name = "any" }, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(unknownProject.FirstError.Type, Is.EqualTo(ErrorType.NotFound));
                this.projectRepository.Verify(x => x.UpdateAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            }
        }

        [Test]
        public async Task VerifyDeleteProjectAsync()
        {
            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("project.notFound"));

            this.projectRepository.Setup(x => x.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Project { Id = ProjectId });

            this.projectRepository.Setup(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Deleted);

            var deleted = await this.service.DeleteProjectAsync(ProjectId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(deleted.Value.Id, Is.EqualTo(ProjectId));
                this.serviceMocks.VerifyCommitted(TransactionMode.ReadWrite);
            }

            Assert.That((await this.service.DeleteProjectAsync(Guid.NewGuid(), CancellationToken.None)).FirstError.Type, Is.EqualTo(ErrorType.NotFound));

            this.serviceMocks.Reset();

            this.projectRepository.Setup(x => x.DeleteAsync(It.IsAny<IDataStoreTransaction>(), ProjectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.Conflict("project.used"));

            var used = await this.service.DeleteProjectAsync(ProjectId, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(used.FirstError.Type, Is.EqualTo(ErrorType.Conflict));
                this.serviceMocks.VerifyRolledBack();
            }
        }
    }
}
