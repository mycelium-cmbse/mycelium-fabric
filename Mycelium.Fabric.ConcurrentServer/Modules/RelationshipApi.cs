// ------------------------------------------------------------------------------------------------
//  <copyright file="RelationshipApi.cs" company="Starion Group S.A.">
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
    /// Hand-coded request handlers for the Relationship routes registered by the generated companion
    /// partial.
    /// </summary>
    /// <remarks>
    /// The operation below belongs to the <c>ElementNavigationService</c> — Systems Modeling API and
    /// Services 1.0 §7.2.2 (p. 41) — and is bound to its REST/HTTP endpoint by §8.1.3 (pp. 50–56).
    /// </remarks>
    public partial class RelationshipApi : ApiBase
    {
        /// <summary>
        /// Handles
        /// <c>GET /projects/{projectId}/commits/{commitId}/elements/{relatedElementId}/relationships</c>,
        /// the REST/HTTP binding of <c>ElementNavigationService::getRelationshipsByRelatedElement</c> —
        /// "Get relationships that are incoming, outgoing, or both relative to the given related element."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the relationships belong to.</param>
        /// <param name="commitId">The identifier of the commit the relationships are read at.</param>
        /// <param name="relatedElementId">
        /// The identifier of the element the relationships are traversed relative to.
        /// </param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// The traversal direction is carried by the <c>direction</c> query parameter, which §8.1.3
        /// (pp. 50–56) restricts to the allowable values <c>in</c>, <c>out</c> and <c>both</c> — the
        /// literals of the <c>Direction</c> enumeration declared alongside the operation in §7.2.2 (p. 41).
        /// Relationship is a subtype of Element in the KerML abstract syntax, so results are Data records
        /// scoped to the given project and commit. Not yet implemented — answers <c>500</c> with an
        /// RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task GetRelationshipsByProjectCommitRelatedElement(HttpContext context, Guid projectId, Guid commitId, Guid relatedElementId, ILogger<RelationshipApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("getRelationshipsByProjectCommitRelatedElement invoked for project {ProjectId}, commit {CommitId} and related element {RelatedElementId}", projectId, commitId, relatedElementId);
            }

            return NotYetImplemented(context, "getRelationshipsByProjectCommitRelatedElement");
        }
    }
}
