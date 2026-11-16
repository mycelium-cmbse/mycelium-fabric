// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectService.cs" company="Starion Group S.A.">
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

    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    /// <summary>
    /// The ProjectService of Systems Modeling API and Services 1.0 §7.2.1.
    /// </summary>
    /// <remarks>
    /// The routes of this service address projects themselves, so its operations run outside any project scope.
    /// </remarks>
    public class ProjectService : ServiceBase, IProjectService
    {
        /// <summary>
        /// The name of the default branch of a created project.
        /// </summary>
        public const string DefaultBranchName = "main";

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectService"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="branchRepository">The <see cref="IBranchRepository"/> of the branches.</param>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> of the commits.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public ProjectService(IDatabaseHandler databaseHandler, IProjectScope projectScope, IProjectRepository projectRepository, IBranchRepository branchRepository,
            ICommitRepository commitRepository, ILogger<ProjectService> logger)
            : base(databaseHandler, projectScope, projectRepository, logger)
        {
            this.BranchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
            this.CommitRepository = commitRepository ?? throw new ArgumentNullException(nameof(commitRepository));
        }

        /// <summary>
        /// Gets the <see cref="IBranchRepository"/> of the branches.
        /// </summary>
        private IBranchRepository BranchRepository { get; }

        /// <summary>
        /// Gets the <see cref="ICommitRepository"/> of the commits.
        /// </summary>
        private ICommitRepository CommitRepository { get; }

        /// <summary>
        /// Creates a project together with its default branch, whose head is an empty initial commit.
        /// </summary>
        /// <param name="project">The project to create, of which only the name, aliases and description are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The created project, or the errors that prevented creating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="project"/> is <c>null</c>.</exception>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.1.2 creates a default branch with every project, which Annex A (PIM-PS-001) names
        /// <c>main</c>. A branch always has a head, so the initial commit is created with an empty change, a deliberate exception to the
        /// non-empty change §7.2.3 requires of <c>createCommit</c>.
        /// </remarks>
        public async Task<ErrorOr<Project>> CreateProjectAsync(Project project, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(project);

            if (string.IsNullOrWhiteSpace(project.Name))
            {
                return Error.Validation("project.name.missing", "A project is created with a name.");
            }

            return await this.WriteAsync(async transaction =>
            {
                var created = DateTime.UtcNow;

                var newProject = new Project
                {
                    Id = Guid.NewGuid(),
                    Name = project.Name,
                    Alias = AliasIncludingName(project.Alias, project.Name),
                    Description = project.Description,
                    Created = created,
                    DefaultBranch = Guid.NewGuid()
                };

                var initialCommit = new Commit { Id = Guid.NewGuid(), OwningProject = newProject.Id, Created = created, Alias = [] };

                var defaultBranch = new Branch
                {
                    Id = newProject.DefaultBranch,
                    Name = DefaultBranchName,
                    Alias = [DefaultBranchName],
                    OwningProject = newProject.Id,
                    Head = initialCommit.Id,
                    Created = created
                };

                var createdProject = await this.ProjectRepository.CreateAsync(transaction, newProject, cancellationToken);

                if (createdProject.IsError)
                {
                    return createdProject.Errors;
                }

                var createdCommit = await this.CommitRepository.CreateAsync(transaction, initialCommit, cancellationToken);

                if (createdCommit.IsError)
                {
                    return createdCommit.Errors;
                }

                var createdBranch = await this.BranchRepository.CreateAsync(transaction, defaultBranch, cancellationToken);

                return createdBranch.IsError ? createdBranch.Errors : createdProject;
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes a project.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The deleted project, or the errors that prevented deleting it.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.1 leaves the treatment of projects used by other projects to the provider; this one
        /// takes its third option, deleting a project only when no commit of another project uses one of its commits. The data store
        /// enforces it, and answers a used project with <see cref="ErrorType.Conflict"/>.
        /// </remarks>
        public Task<ErrorOr<Project>> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return this.WriteAsync(async transaction =>
            {
                var project = await this.ProjectRepository.ReadByIdAsync(transaction, projectId, cancellationToken);

                if (project.IsError)
                {
                    return project.Errors;
                }

                var deleted = await this.ProjectRepository.DeleteAsync(transaction, projectId, cancellationToken);

                return deleted.IsError ? deleted.Errors : project;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets a project.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The project, or <see cref="ErrorType.NotFound"/> when it does not exist.</returns>
        public Task<ErrorOr<Project>> GetProjectByIdAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return this.ReadAsync(transaction => this.ProjectRepository.ReadByIdAsync(transaction, projectId, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets one page of the projects.
        /// </summary>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of projects, or the errors that prevented reading it.</returns>
        public Task<ErrorOr<PagedResult<Project>>> GetProjectsAsync(QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadAsync(transaction => this.ProjectRepository.ReadAsync(transaction, queryParameters, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Updates the name, description and default branch of a project, keeping the current value of each one left unset.
        /// </summary>
        /// <param name="project">The changes to the project, identified by its identifier.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The updated project, or the errors that prevented updating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="project"/> is <c>null</c>.</exception>
        /// <remarks>
        /// A default branch that is not a branch of the project is answered as Annex A (PIM-PCB-009) answers <c>setDefaultBranch</c>.
        /// </remarks>
        public Task<ErrorOr<Project>> UpdateProjectAsync(Project project, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(project);

            return this.WriteAsync(async transaction =>
            {
                var existing = await this.ProjectRepository.ReadByIdAsync(transaction, project.Id, cancellationToken);

                if (existing.IsError)
                {
                    return existing.Errors;
                }

                if (project.DefaultBranch != Guid.Empty)
                {
                    var branch = await this.BranchRepository.ReadByIdAsync(transaction, project.DefaultBranch, cancellationToken);

                    if (branch.IsError && branch.FirstError.Type != ErrorType.NotFound)
                    {
                        return branch.Errors;
                    }

                    if (branch.IsError || branch.Value.OwningProject != project.Id)
                    {
                        return Error.NotFound("branch.notFound", $"Project {project.Id} has no Branch {project.DefaultBranch}.");
                    }

                    existing.Value.DefaultBranch = project.DefaultBranch;
                }

                if (!string.IsNullOrWhiteSpace(project.Name))
                {
                    existing.Value.Name = project.Name;
                }

                existing.Value.Alias = AliasIncludingName(existing.Value.Alias, existing.Value.Name);
                existing.Value.Description = project.Description ?? existing.Value.Description;

                return await this.ProjectRepository.UpdateAsync(transaction, existing.Value, cancellationToken);
            }, cancellationToken);
        }
    }
}
