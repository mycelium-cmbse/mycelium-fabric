// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectApi.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Modules
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Serialization;
    using Mycelium.Fabric.ConcurrentServer.Services;

    using SysML2.NET.PSM.DTO;

    /// <summary>
    /// Handlers of the Project routes.
    /// </summary>
    /// <remarks>
    /// Binds <c>ProjectService</c> operations (Systems Modeling API and Services 1.0 §7.2.1) to their §8.1.3 endpoints.
    /// </remarks>
    public partial class ProjectApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects</c>: <c>ProjectService::getProjects</c> (Systems Modeling API and Services 1.0 §7.2.1).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectService">The <see cref="IProjectService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetProjects(HttpContext context, [FromServices] IProjectService projectService, ILogger<ProjectApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getProjects invoked");
            }

            if (projectService == null)
            {
                await NotYetImplemented(context, "getProjects");

                return;
            }

            try
            {
                await PagedAsync<Project, SysML2.NET.PIM.DTO.Project>(context, "getProjects",
                    queryParameters => projectService.GetProjectsAsync(queryParameters, context.RequestAborted));
            }
            catch (Exception exception)
            {
                await Failure(context, "getProjects", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>POST /projects</c>: <c>ProjectService::createProject</c> (Systems Modeling API and Services 1.0 §7.2.1).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="body">The <c>ProjectRequest</c> from the request body.</param>
        /// <param name="projectService">The <see cref="IProjectService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// <c>ProjectRequest.defaultBranch</c> is ignored, since a project that does not exist yet has no branch.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task PostProject(HttpContext context, RequestBody<ProjectRequest> body, [FromServices] IProjectService projectService, ILogger<ProjectApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("postProject invoked");
            }

            if (projectService == null)
            {
                await NotYetImplemented(context, "postProject");

                return;
            }

            try
            {
                if (body.Content.IsError)
                {
                    await Failed(context, "postProject", body.Content.Errors);

                    return;
                }

                var request = body.Content.Value;

                var outcome = await projectService.CreateProjectAsync(request.Name, request.Description, context.RequestAborted);

                await WriteAsync<Project, SysML2.NET.PIM.DTO.Project>(context, "postProject", outcome, StatusCodes.Status201Created);
            }
            catch (Exception exception)
            {
                await Failure(context, "postProject", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}</c>: <c>ProjectService::getProjectById</c> (Systems Modeling API and Services 1.0 §7.2.1).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="projectService">The <see cref="IProjectService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task GetProjectById(HttpContext context, Guid projectId, [FromServices] IProjectService projectService, ILogger<ProjectApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getProjectById invoked for project {ProjectId}", projectId);
            }

            if (projectService == null)
            {
                await NotYetImplemented(context, "getProjectById");

                return;
            }

            try
            {
                var outcome = await projectService.GetProjectByIdAsync(projectId, context.RequestAborted);

                await WriteAsync<Project, SysML2.NET.PIM.DTO.Project>(context, "getProjectById", outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, "getProjectById", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>PUT /projects/{projectId}</c>: <c>ProjectService::updateProject</c> (Systems Modeling API and Services 1.0 §7.2.1).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="body">The <c>ProjectRequest</c> from the request body.</param>
        /// <param name="projectService">The <see cref="IProjectService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Also realizes <c>setDefaultBranch</c> through <c>ProjectRequest.defaultBranch</c> (§8.1.3).
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task PutProjectById(HttpContext context, Guid projectId, RequestBody<ProjectRequest> body, [FromServices] IProjectService projectService,
            ILogger<ProjectApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("putProjectById invoked for project {ProjectId}", projectId);
            }

            if (projectService == null)
            {
                await NotYetImplemented(context, "putProjectById");

                return;
            }

            try
            {
                if (body.Content.IsError)
                {
                    await Failed(context, "putProjectById", body.Content.Errors);

                    return;
                }

                var request = body.Content.Value;

                var outcome = await projectService.UpdateProjectAsync(projectId, request.Name, request.Description, request.DefaultBranch, context.RequestAborted);

                await WriteAsync<Project, SysML2.NET.PIM.DTO.Project>(context, "putProjectById", outcome, StatusCodes.Status200OK);
            }
            catch (Exception exception)
            {
                await Failure(context, "putProjectById", exception, logger);
            }
        }

        /// <summary>
        /// Handles <c>DELETE /projects/{projectId}</c>: <c>ProjectService::deleteProject</c> (Systems Modeling API and Services 1.0 §7.2.1).
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <param name="projectService">The <see cref="IProjectService"/>, or <c>null</c> when none is registered.</param>
        /// <param name="logger">The logger of the module.</param>
        /// <returns>A <see cref="Task"/> that completes once the response is written.</returns>
        /// <remarks>
        /// Answers <c>200</c> with the deleted project, or <c>204</c> when the request carries <c>Prefer: return=minimal</c> (RFC 7240 §4.2).
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(Error), StatusCodes.Status500InternalServerError)]
        private static async Task DeleteProjectById(HttpContext context, Guid projectId, [FromServices] IProjectService projectService, ILogger<ProjectApi> logger)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("deleteProjectById invoked for project {ProjectId}", projectId);
            }

            if (projectService == null)
            {
                await NotYetImplemented(context, "deleteProjectById");

                return;
            }

            try
            {
                var outcome = await projectService.DeleteProjectAsync(projectId, context.RequestAborted);

                await DeletedAsync<Project, SysML2.NET.PIM.DTO.Project>(context, "deleteProjectById", outcome);
            }
            catch (Exception exception)
            {
                await Failure(context, "deleteProjectById", exception, logger);
            }
        }
    }
}
