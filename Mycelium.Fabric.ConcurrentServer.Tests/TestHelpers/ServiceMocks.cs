// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceMocks.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.TestHelpers
{
    using System;
    using System.Threading;

    using ErrorOr;

    using Moq;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.PIM.DTO;

    public sealed class ServiceMocks
    {
        public ServiceMocks(Guid projectId)
        {
            this.Transaction = new Mock<IDataStoreTransaction>();

            this.Scope = new Mock<ITransactionScope>();
            this.Scope.Setup(scope => scope.Transaction).Returns(this.Transaction.Object);
            this.Scope.Setup(scope => scope.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success);
            this.Scope.Setup(scope => scope.RollbackAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success);

            this.DatabaseHandler = new Mock<IDatabaseHandler>();

            this.DatabaseHandler.Setup(handler => handler.BeginTransactionAsync(It.IsAny<TransactionMode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ErrorOrFactory.From(this.Scope.Object));

            this.ProjectScope = new Mock<IProjectScope>();
            this.ProjectScope.Setup(scope => scope.ProjectId).Returns(projectId);

            this.CommitRepository = new Mock<ICommitRepository>();

            this.CommitRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("commit.notFound"));

            this.ProjectRepository = new Mock<IProjectRepository>();

            this.ProjectRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Error.NotFound("project.notFound"));

            this.ProjectRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Project { Id = projectId, Name = "the project" });
        }

        public Mock<ICommitRepository> CommitRepository { get; }

        public Mock<IDatabaseHandler> DatabaseHandler { get; }

        public Mock<IProjectRepository> ProjectRepository { get; }

        public Mock<IProjectScope> ProjectScope { get; }

        public Mock<ITransactionScope> Scope { get; }

        public Mock<IDataStoreTransaction> Transaction { get; }

        public void VerifyCommitted(TransactionMode mode, int times = 1)
        {
            this.DatabaseHandler.Verify(handler => handler.BeginTransactionAsync(mode, It.IsAny<CancellationToken>()), Times.Exactly(times));
            this.Scope.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(times));
            this.Scope.Verify(scope => scope.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        public void VerifyRolledBack()
        {
            this.Scope.Verify(scope => scope.RollbackAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            this.Scope.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        public void AddCommit(Guid commitId, Guid owningProjectId)
        {
            this.CommitRepository.Setup(repository => repository.ReadByIdAsync(It.IsAny<IDataStoreTransaction>(), commitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Commit { Id = commitId, OwningProject = owningProjectId });
        }

        public void Reset()
        {
            this.DatabaseHandler.Invocations.Clear();
            this.Scope.Invocations.Clear();
        }
    }
}
