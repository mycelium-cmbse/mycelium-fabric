// ------------------------------------------------------------------------------------------------
//  <copyright file="ExternalRelationshipService.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Common;
    using SysML2.NET.Core.DTO.Root.Elements;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    /// <summary>
    /// The ExternalRelationshipService of Systems Modeling API and Services 1.0 §7.2.5, confined to the project of the request.
    /// </summary>
    /// <remarks>
    /// An external relationship is versioned data, so creating or deleting one commits it on the branch, as §8.1.3 maps both operations.
    /// </remarks>
    public class ExternalRelationshipService : VersionedDataServiceBase, IExternalRelationshipService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalRelationshipService"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> that reads the commit the data is read at.</param>
        /// <param name="dataRepository">The <see cref="IDataRepository"/> the external relationships are read from.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/> that resolves commits and commits the changes.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public ExternalRelationshipService(IDatabaseHandler databaseHandler, IProjectScope projectScope, IProjectRepository projectRepository, ICommitRepository commitRepository,
            IDataRepository dataRepository,
            IProjectDataVersioningService versioningService, ILogger<ExternalRelationshipService> logger)
            : base(databaseHandler, projectScope, projectRepository, commitRepository, dataRepository, versioningService, logger)
        {
        }

        /// <summary>
        /// Creates an external relationship on a branch of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch, or <c>null</c> for the default branch.</param>
        /// <param name="externalRelationship">The external relationship to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or the errors that prevented creating the external relationship.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="externalRelationship"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        /// <remarks>
        /// The element end exists at the head of the branch; the external data end is not required to, since §8.1.3 maps the operation to
        /// a commit of the external relationship alone. An identifier already taken at the head is a conflict rather than an update.
        /// </remarks>
        public async Task<ErrorOr<Success>> CreateExternalRelationshipAsync(Guid projectId, Guid? branchId, ExternalRelationship externalRelationship,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(externalRelationship);

            if (externalRelationship.ElementEnd == Guid.Empty || externalRelationship.ExternalDataEnd == Guid.Empty)
            {
                return Error.Validation("externalRelationship.end.missing", "An external relationship relates an element to external data.");
            }

            return await this.WriteInProjectAsync<Success>(projectId, async transaction =>
            {
                var head = await this.VersioningService.GetHeadCommitAsync(projectId, branchId, cancellationToken);

                if (head.IsError)
                {
                    return head.Errors;
                }

                var elementEnd = await this.DataRepository.ReadByIdAsync<IElement>(transaction, head.Value.Id, externalRelationship.ElementEnd, cancellationToken);

                if (elementEnd.IsError)
                {
                    return ReferenceError(elementEnd.Errors, "externalRelationship.elementEnd", externalRelationship.ElementEnd);
                }

                if (externalRelationship.Id != Guid.Empty)
                {
                    var existing = await this.DataRepository.ReadByIdAsync<IData>(transaction, head.Value.Id, externalRelationship.Id, cancellationToken);

                    if (!existing.IsError)
                    {
                        return Error.Conflict("externalRelationship.exists", $"The data {externalRelationship.Id} already exists at the head of the branch.");
                    }

                    if (existing.FirstError.Type != ErrorType.NotFound)
                    {
                        return existing.Errors;
                    }
                }

                var created = new ExternalRelationship
                {
                    Id = externalRelationship.Id == Guid.Empty ? Guid.NewGuid() : externalRelationship.Id,
                    Specification = externalRelationship.Specification,
                    Language = externalRelationship.Language,
                    ElementEnd = externalRelationship.ElementEnd,
                    ExternalDataEnd = externalRelationship.ExternalDataEnd
                };

                var commit = await this.VersioningService.CreateCommitAsync([new DataVersion { Payload = created }], branchId, new Commit { OwningProject = projectId },
                    cancellationToken);

                return commit.IsError ? commit.Errors : Result.Success;
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes an external relationship from a branch of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="branchId">The identifier of the branch, or <c>null</c> for the default branch.</param>
        /// <param name="externalRelationshipId">The identifier of the external relationship.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or the errors that prevented deleting the external relationship.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Success>> DeleteExternalRelationshipAsync(Guid projectId, Guid? branchId, Guid externalRelationshipId, CancellationToken cancellationToken)
        {
            return this.WriteInProjectAsync<Success>(projectId, async transaction =>
            {
                var head = await this.VersioningService.GetHeadCommitAsync(projectId, branchId, cancellationToken);

                if (head.IsError)
                {
                    return head.Errors;
                }

                var existing = await this.DataRepository.ReadByIdAsync<ExternalRelationship>(transaction, head.Value.Id, externalRelationshipId, cancellationToken);

                if (existing.IsError)
                {
                    return existing.Errors;
                }

                var commit = await this.VersioningService.CreateCommitAsync([new DataVersion { Identity = new DataIdentity { Id = externalRelationshipId } }], branchId,
                    new Commit { OwningProject = projectId }, cancellationToken);

                return commit.IsError ? commit.Errors : Result.Success;
            }, cancellationToken);
        }

        /// <summary>
        /// Gets an external relationship as it stands at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="externalRelationshipId">The identifier of the external relationship.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The external relationship, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<ExternalRelationship>> GetExternalRelationshipByIdAsync(Guid projectId, Guid commitId, Guid externalRelationshipId, CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId,
                transaction => this.DataRepository.ReadByIdAsync<ExternalRelationship>(transaction, commitId, externalRelationshipId, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets one page of the external relationships at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of external relationships, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<ExternalRelationship>>> GetExternalRelationshipsAsync(Guid projectId, Guid commitId, QueryParameters queryParameters,
            CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId,
                transaction => this.DataRepository.ReadAsync<ExternalRelationship>(transaction, commitId, queryParameters, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Gets one page of the external relationships an element is an end of at a commit of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="elementId">The identifier of the element.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of external relationships, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<ExternalRelationship>>> GetExternalRelationshipsByElementAsync(Guid projectId, Guid commitId, Guid elementId,
            QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadAtCommitAsync(projectId, commitId,
                transaction => this.DataRepository.ReadExternalRelationshipsByElementAsync(transaction, commitId, elementId, queryParameters, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Turns the failed read of an end into an invalid input when the end does not exist.
        /// </summary>
        /// <param name="errors">The errors of the read.</param>
        /// <param name="code">The code of the end.</param>
        /// <param name="endId">The identifier of the end.</param>
        /// <returns>The errors to report.</returns>
        private static ErrorOr<Success> ReferenceError(List<Error> errors, string code, Guid endId)
        {
            return errors[0].Type == ErrorType.NotFound
                ? Error.Validation($"{code}.invalid", $"The end {endId} does not exist at the head of the branch.")
                : errors;
        }
    }
}
