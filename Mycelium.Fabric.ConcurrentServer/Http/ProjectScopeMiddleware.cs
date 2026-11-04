// ------------------------------------------------------------------------------------------------
//  <copyright file="ProjectScopeMiddleware.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Http
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Patterns;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    /// <summary>
    /// Confines every request on a route nested under a project to the <c>projectId</c> of that route, for as long as the request runs.
    /// </summary>
    /// <remarks>
    /// The <c>ProjectService</c> routes <c>/projects</c> and <c>/projects/{projectId}</c> address projects themselves and stay unscoped
    /// (Systems Modeling API and Services 1.0 §8.1.3, pp. 50–56).
    /// </remarks>
    public sealed class ProjectScopeMiddleware
    {
        /// <summary>
        /// The name of the route parameter that carries the project of a nested route.
        /// </summary>
        public const string ProjectIdParameter = "projectId";

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectScopeMiddleware"/> class.
        /// </summary>
        /// <param name="next">The <see cref="RequestDelegate"/> of the rest of the pipeline.</param>
        public ProjectScopeMiddleware(RequestDelegate next)
        {
            this.Next = next;
        }

        /// <summary>
        /// Gets the <see cref="RequestDelegate"/> of the rest of the pipeline.
        /// </summary>
        private RequestDelegate Next { get; }

        /// <summary>
        /// Runs the rest of the pipeline, inside the scope of the route's project when the route is nested under one.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <returns>A <see cref="Task"/> that completes once the rest of the pipeline has run.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="projectScope"/> is <c>null</c>.</exception>
        public async Task InvokeAsync(HttpContext context, IProjectScope projectScope)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(projectScope);

            if (!TryQueryScopedProjectId(context, out var projectId))
            {
                await this.Next(context);

                return;
            }

            using (projectScope.Enter(projectId))
            {
                await this.Next(context);
            }
        }

        /// <summary>
        /// Asserts whether the given route pattern addresses a record nested under a project, rather than the project itself.
        /// </summary>
        /// <param name="pattern">The <see cref="T:Microsoft.AspNetCore.Routing.Patterns.RoutePattern"/> of the matched endpoint.</param>
        /// <returns><c>true</c> when the pattern carries <see cref="ProjectIdParameter"/> followed by at least one further segment.</returns>
        private static bool IsNestedUnderProject(RoutePattern pattern) => pattern.GetParameter(ProjectIdParameter) != null && pattern.PathSegments.Count > 2;

        /// <summary>
        /// Reads the project of the matched route when that route is nested under a project.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request.</param>
        /// <param name="projectId">The identifier of the project, or <see cref="Guid.Empty"/> when the route is not scoped.</param>
        /// <returns><c>true</c> when the request is to be confined to <paramref name="projectId"/>.</returns>
        private static bool TryQueryScopedProjectId(HttpContext context, out Guid projectId)
        {
            projectId = Guid.Empty;

            return context.GetEndpoint() is RouteEndpoint { RoutePattern: var pattern } && IsNestedUnderProject(pattern)
                   && Guid.TryParse(context.Request.RouteValues[ProjectIdParameter]?.ToString(), out projectId);
        }
    }
}
