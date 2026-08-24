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

    /// <summary>
    /// Hand-coded request handlers for the Project routes registered by the generated companion partial.
    /// </summary>
    /// <remarks>
    /// Every operation below belongs to the <c>ProjectService</c> — Systems Modeling API and Services 1.0
    /// §7.2.1 (pp. 39–40) — and is bound to its REST/HTTP endpoint by §8.1.3 (pp. 50–56). A Project is the
    /// container for versioned model data; §7.1.2 (pp. 32–36) defines it together with its
    /// <c>defaultBranch</c> and <c>commits</c>.
    /// </remarks>
    public partial class ProjectApi : ApiBase
    {
        /// <summary>
        /// Handles <c>GET /projects</c>, the REST/HTTP binding of <c>ProjectService::getProjects</c> —
        /// "Get all projects."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.1 (pp. 39–40). §8.1.3 (pp. 50–56) also describes the
        /// pagination strategy that applies to collection endpoints such as this one. Not yet implemented —
        /// answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetProjects(HttpContext context, ILogger<ProjectApi> logger)
        {
            logger.LogInformation("getProjects invoked");

            return NotYetImplemented(context, "getProjects");
        }

        /// <summary>
        /// Handles <c>POST /projects</c>, the REST/HTTP binding of <c>ProjectService::createProject</c> —
        /// "Create a new project with the given name and description (optional)."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.2.1 (pp. 39–40). Not yet implemented — answers
        /// <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task PostProject(HttpContext context, ILogger<ProjectApi> logger)
        {
            logger.LogInformation("postProject invoked");

            return NotYetImplemented(context, "postProject");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}</c>, the REST/HTTP binding of
        /// <c>ProjectService::getProjectById</c> — "Get project with the given id (projectId)."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project that is requested.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// §8.1.3 (pp. 50–56) composes this endpoint into <c>getDefaultBranch</c>: the project returned
        /// here carries <c>Project.defaultBranch</c>, the id of the project's default branch, which
        /// <c>getHeadCommit</c> falls back to when no branch is supplied. Not yet implemented — answers
        /// <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetProjectById(HttpContext context, Guid projectId, ILogger<ProjectApi> logger)
        {
            logger.LogInformation("getProjectById invoked for project {ProjectId}", projectId);

            return NotYetImplemented(context, "getProjectById");
        }

        /// <summary>
        /// Handles <c>PUT /projects/{projectId}</c>, the REST/HTTP binding of
        /// <c>ProjectService::updateProject</c> — "Update the project with the given id (projectId)."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project that is updated.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// The request body is a <c>ProjectRequest</c>. Per §8.1.3 (pp. 50–56) this single endpoint also
        /// realizes <c>ProjectDataVersioningService::setDefaultBranch</c> — "Set the branch with the given
        /// branchId as the default branch of the given project" — by setting the id of the new default
        /// branch as <c>ProjectRequest.defaultBranch</c> in the body. Not yet implemented — answers
        /// <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task PutProjectById(HttpContext context, Guid projectId, ILogger<ProjectApi> logger)
        {
            logger.LogInformation("putProjectById invoked for project {ProjectId}", projectId);

            return NotYetImplemented(context, "putProjectById");
        }

        /// <summary>
        /// Handles <c>DELETE /projects/{projectId}</c>, the REST/HTTP binding of
        /// <c>ProjectService::deleteProject</c> — "Delete the project with the given id (projectId)."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project that is deleted.</param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// §7.2.1 (pp. 39–40) states the pre-condition that the project with the given <c>projectId</c>
        /// exists, and the post-condition that "all operations of all services where projectId (Id of the
        /// deleted project) is an input argument will return null".
        /// <para>
        /// Elements owned by a deleted project may still be referenced from other projects through project
        /// usages. The specification deliberately leaves the detection of, and the response to, that
        /// condition to the API and Service provider, and offers three example behaviours: delete
        /// unconditionally while guaranteeing that operations on the using projects keep working; delete
        /// only when no element in a pre-specified set of commits is used by another project; or delete
        /// only when no commit of the project is referenced by a project usage in any commit of any other
        /// project. Which of these Fabric adopts is an open design decision.
        /// </para>
        /// Not yet implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task DeleteProjectById(HttpContext context, Guid projectId, ILogger<ProjectApi> logger)
        {
            logger.LogInformation("deleteProjectById invoked for project {ProjectId}", projectId);

            return NotYetImplemented(context, "deleteProjectById");
        }
    }
}
