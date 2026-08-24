// ------------------------------------------------------------------------------------------------
//  <copyright file="DiffMergeApi.cs" company="Starion Group S.A.">
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
    /// Hand-coded request handlers for the DiffMerge routes registered by the generated companion partial.
    /// </summary>
    /// <remarks>
    /// Both operations below belong to the <c>ProjectDataVersioningService</c> — Systems Modeling API and
    /// Services 1.0 §7.2.3 (pp. 42–45) — and are bound to their REST/HTTP endpoints by §8.1.3 (pp. 50–56).
    /// They are the operations that make concurrent modelling tractable: <c>diffCommits</c> reports what
    /// changed between two snapshots, and <c>mergeIntoBranch</c> reconciles divergent work.
    /// </remarks>
    public partial class DiffMergeApi : ApiBase
    {
        /// <summary>
        /// Handles <c>POST /projects/{projectId}/branches/{targetBranchId}/merge</c>, the REST/HTTP binding
        /// of <c>ProjectDataVersioningService::mergeIntoBranch</c> — "Merge the given commits
        /// (commitsToMerge) in the given branch (baseBranch)."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the merge takes place in.</param>
        /// <param name="targetBranchId">
        /// The identifier of the branch the commits are merged into — the <c>baseBranch</c> of the
        /// operation.
        /// </param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// Per §7.2.3 (pp. 42–45) the commits in <c>commitsToMerge</c> "may be commits referenced by a
        /// CommitReference, such as Branch.head or Tag.taggedCommit, or any other commit in the owning
        /// project (Project.commits)". The operation returns a <c>MergeResult</c> carrying either the
        /// commit produced by a successful merge (<c>MergeResult.mergeCommit</c>) or, when the merge fails,
        /// the set of <c>DataIdentity</c> records representing the conflicts (<c>MergeResult.conflict</c>).
        /// Two optional inputs may be supplied: a <c>resolution</c> set of Data that resolves the
        /// conflicts, and a <c>description</c> for the merged commit.
        /// <para>
        /// Not yet implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </para>
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task Merge(HttpContext context, Guid projectId, Guid targetBranchId, ILogger<DiffMergeApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("merge invoked for project {ProjectId} and target branch {TargetBranchId}", projectId, targetBranchId);
            }

            return NotYetImplemented(context, "merge");
        }

        /// <summary>
        /// Handles <c>GET /projects/{projectId}/commits/{compareCommitId}/diff</c>, the REST/HTTP binding
        /// of <c>ProjectDataVersioningService::diffCommits</c> — "Get the difference between two commits -
        /// compareCommit and baseCommit."
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> of the request being handled.</param>
        /// <param name="projectId">The identifier of the project the commits belong to.</param>
        /// <param name="compareCommitId">
        /// The identifier of the commit being compared — the <c>compareCommit</c> of the operation. The
        /// <c>baseCommit</c> is supplied separately by the request.
        /// </param>
        /// <param name="logger">The <see cref="ILogger"/> resolved from the request services.</param>
        /// <returns>A <see cref="Task"/> that completes once the response has been written.</returns>
        /// <remarks>
        /// §7.2.3 (pp. 42–45) defines the result set-theoretically: the operation "gets
        /// compareCommit.versionedData - baseCommit.versionedData and returns a DataDifference object with
        /// baseData and compareData for each difference". Data present in the compare commit but absent
        /// from the base yields a <c>DataDifference</c> with <c>compareData</c> populated and
        /// <c>baseData</c> empty; data absent from the compare commit but present in the base yields the
        /// mirror image; data present in both but differing yields both fields populated.
        /// <para>
        /// The optional <c>changeTypes</c> argument is a collection of the <c>ChangeType</c> literals
        /// <c>CREATED</c>, <c>UPDATED</c> and <c>DELETED</c>. When it is omitted, differences for all data
        /// created, updated or deleted in the compare commit versus the base commit are returned; when it
        /// is supplied, only differences of the given types are returned.
        /// </para>
        /// Not yet implemented — answers <c>500</c> with an RFC 7807 payload.
        /// </remarks>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public static Task Diff(HttpContext context, Guid projectId, Guid compareCommitId, ILogger<DiffMergeApi> logger)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("diff invoked for project {ProjectId} and compare commit {CompareCommitId}", projectId, compareCommitId);
            }

            return NotYetImplemented(context, "diff");
        }
    }
}
