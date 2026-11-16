// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectUsageService.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Common;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    /// <summary>
    /// The ProjectUsageService of Systems Modeling API and Services 1.0 §7.2.6, confined to the project of the request.
    /// </summary>
    public class ProjectUsageService : VersionedDataServiceBase, IProjectUsageService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectUsageService"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> that reads the commit the data is read at.</param>
        /// <param name="dataRepository">The <see cref="IDataRepository"/> the project usages are read from.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/> that resolves commits and commits the changes.</param>
        /// <param name="projectUsageRepository">The <see cref="IProjectUsageRepository"/> that reads the used commit of another project.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public ProjectUsageService(IDatabaseHandler databaseHandler, IProjectScope projectScope, IProjectRepository projectRepository, ICommitRepository commitRepository,
            IDataRepository dataRepository,
            IProjectDataVersioningService versioningService,
            IProjectUsageRepository projectUsageRepository, ILogger<ProjectUsageService> logger)
            : base(databaseHandler, projectScope, projectRepository, commitRepository, dataRepository, versioningService, logger)
        {
            this.ProjectUsageRepository = projectUsageRepository ?? throw new ArgumentNullException(nameof(projectUsageRepository));
        }

        /// <summary>
        /// Gets the <see cref="IProjectUsageRepository"/> that reads the used commit of another project.
        /// </summary>
        private IProjectUsageRepository ProjectUsageRepository { get; }

        /// <summary>
        /// Creates a usage of a commit of another project on a branch of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch, or <c>null</c> for the default branch.</param>
        /// <param name="projectUsage">The project usage to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The commit that holds the project usage, or the errors that prevented creating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="projectUsage"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.1.2 makes the used project the owning project of the used commit, and describes a
        /// usage as the use of another project; the used commit is the one read this service makes outside the project of the request.
        /// </remarks>
        public async Task<ErrorOr<Commit>> CreateProjectUsageAsync(Guid projectId, Guid? branchId, ProjectUsage projectUsage, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(projectUsage);

            if (projectUsage.UsedProjectCommit == Guid.Empty)
            {
                return Error.Validation("projectUsage.usedProjectCommit.missing", "A project usage names the commit it uses.");
            }

            return await this.WriteInProjectAsync(projectId, async transaction =>
            {
                var usedCommit = await this.ProjectUsageRepository.ReadUsedCommitAsync(transaction, projectUsage.UsedProjectCommit, cancellationToken);

                if (usedCommit.IsError)
                {
                    return usedCommit.Errors;
                }

                if (usedCommit.Value.OwningProject == projectId)
                {
                    return Error.Validation("projectUsage.usedProject.invalid", "A project does not use one of its own commits.");
                }

                if (projectUsage.UsedProject != Guid.Empty && projectUsage.UsedProject != usedCommit.Value.OwningProject)
                {
                    return Error.Validation("projectUsage.usedProject.invalid", $"The used project is the owning project {usedCommit.Value.OwningProject} of the used commit.");
                }

                var head = await this.VersioningService.GetHeadCommitAsync(projectId, branchId, cancellationToken);

                if (head.IsError)
                {
                    return head.Errors;
                }

                if (projectUsage.Id != Guid.Empty)
                {
                    var existing = await this.DataRepository.ReadByIdAsync<IData>(transaction, head.Value.Id, projectUsage.Id, cancellationToken);

                    if (!existing.IsError)
                    {
                        return Error.Conflict("projectUsage.exists", $"The data {projectUsage.Id} already exists at the head of the branch.");
                    }

                    if (existing.FirstError.Type != ErrorType.NotFound)
                    {
                        return existing.Errors;
                    }
                }

                var usage = new ProjectUsage
                {
                    Id = projectUsage.Id == Guid.Empty ? Guid.NewGuid() : projectUsage.Id,
                    UsedProjectCommit = usedCommit.Value.Id,
                    UsedProject = usedCommit.Value.OwningProject
                };

                return await this.VersioningService.CreateCommitAsync([new DataVersion { Payload = usage }], branchId, new Commit { OwningProject = projectId },
                    cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes a project usage from a branch of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch, or <c>null</c> for the default branch.</param>
        /// <param name="projectUsageId">The identifier of the project usage.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The commit from which the project usage is absent, or the errors that prevented deleting it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Commit>> DeleteProjectUsageAsync(Guid projectId, Guid? branchId, Guid projectUsageId, CancellationToken cancellationToken)
        {
            return this.WriteInProjectAsync(projectId, async transaction =>
            {
                var head = await this.VersioningService.GetHeadCommitAsync(projectId, branchId, cancellationToken);

                if (head.IsError)
                {
                    return head.Errors;
                }

                var existing = await this.DataRepository.ReadByIdAsync<ProjectUsage>(transaction, head.Value.Id, projectUsageId, cancellationToken);

                if (existing.IsError)
                {
                    return existing.Errors;
                }

                return await this.VersioningService.CreateCommitAsync([new DataVersion { Identity = new DataIdentity { Id = projectUsageId } }], branchId,
                    new Commit { OwningProject = projectId }, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets one page of the project usages at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of project usages, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<ProjectUsage>>> GetProjectUsagesAsync(Guid projectId, Guid commitId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId,
                transaction => this.DataRepository.ReadAsync<ProjectUsage>(transaction, commitId, queryParameters, cancellationToken), cancellationToken);
        }
    }
}
