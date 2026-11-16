// ------------------------------------------------------------------------------------------------
//  <copyright file="QueryService.cs" company="Starion Group S.A.">
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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Common;
    using SysML2.NET.PIM.DTO;
    using SysML2.NET.PSM;

    using ConstraintValue = SysML2.NET.PSM.DTO.ConstraintValue;

    /// <summary>
    /// The QueryService of Systems Modeling API and Services 1.0 §7.2.4, confined to the project of the request.
    /// </summary>
    public class QueryService : ServiceBase, IQueryService
    {
        /// <summary>
        /// The name of the property that holds the owning project of a query.
        /// </summary>
        private const string ProjectProperty = "project";

        /// <summary>
        /// The name of the property that holds the identifier of a data.
        /// </summary>
        private const string IdProperty = "id";

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryService"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="queryRepository">The <see cref="IQueryRepository"/> of the queries.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> that reads the commit a query is executed at.</param>
        /// <param name="dataRepository">The <see cref="IDataRepository"/> the queries are executed against.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/> that resolves the commit a query is executed at.</param>
        /// <param name="logger">The <see cref="ILogger{TCategoryName}"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public QueryService(IDatabaseHandler databaseHandler, IProjectScope projectScope, IQueryRepository queryRepository, IProjectRepository projectRepository,
            ICommitRepository commitRepository,
            IDataRepository dataRepository, IProjectDataVersioningService versioningService, ILogger<QueryService> logger)
            : base(databaseHandler, projectScope, projectRepository, logger)
        {
            this.QueryRepository = queryRepository ?? throw new ArgumentNullException(nameof(queryRepository));
            this.CommitRepository = commitRepository ?? throw new ArgumentNullException(nameof(commitRepository));
            this.DataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            this.VersioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        }

        /// <summary>
        /// Gets the <see cref="ICommitRepository"/> that reads the commit a query is executed at.
        /// </summary>
        private ICommitRepository CommitRepository { get; }

        /// <summary>
        /// Gets the <see cref="IDataRepository"/> the queries are executed against.
        /// </summary>
        private IDataRepository DataRepository { get; }

        /// <summary>
        /// Gets the <see cref="IQueryRepository"/> of the queries.
        /// </summary>
        private IQueryRepository QueryRepository { get; }

        /// <summary>
        /// Gets the <see cref="IProjectDataVersioningService"/> that resolves the commit a query is executed at.
        /// </summary>
        private IProjectDataVersioningService VersioningService { get; }

        /// <summary>
        /// Creates a query of the project of the request.
        /// </summary>
        /// <param name="query">The query to create, of which only the name, aliases, description, project, select, scope, where and orderBy are read.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The created query, or the errors that prevented creating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="query"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The query names a project other than the one of the request.</exception>
        public async Task<ErrorOr<Query>> CreateQueryAsync(Query query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var projectId = query.Project;
            var condition = ToSearchFilter(query.Where);

            if (condition.IsError)
            {
                return condition.Errors;
            }

            return await this.WriteInProjectAsync(projectId, async transaction =>
            {
                var created = new Query { Id = Guid.NewGuid(), Project = projectId };
                var nameIsFree = await this.ApplyDefinitionAsync(transaction, created, query, cancellationToken);

                return nameIsFree.IsError ? nameIsFree.Errors : await this.QueryRepository.CreateAsync(transaction, created, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes a query of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryId">The identifier of the query.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The deleted query, or the errors that prevented deleting it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Query>> DeleteQueryAsync(Guid projectId, Guid queryId, CancellationToken cancellationToken)
        {
            return this.WriteInProjectAsync(projectId, async transaction =>
            {
                var query = await this.ReadQueryAsync(transaction, projectId, queryId, cancellationToken);

                if (query.IsError)
                {
                    return query.Errors;
                }

                var deleted = await this.QueryRepository.DeleteAsync(transaction, queryId, cancellationToken);

                return deleted.IsError ? deleted.Errors : query;
            }, cancellationToken);
        }

        /// <summary>
        /// Executes a query in the project of the request at a commit.
        /// </summary>
        /// <param name="query">The query to execute.</param>
        /// <param name="commitId">The identifier of the commit, or <c>null</c> for the head of the default branch.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of data that satisfy the query, or the errors that prevented executing it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="query"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The query names a project other than the one of the request.</exception>
        /// <exception cref="NotSupportedException">The query selects or orders by properties, which the data store contract cannot apply.</exception>
        public Task<ErrorOr<PagedResult<IData>>> ExecuteQueryAsync(Query query, Guid? commitId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var projectId = query.Project;

            return this.ReadInProjectAsync(projectId, transaction => this.ExecuteAsync(transaction, projectId, query, commitId, queryParameters, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Executes a stored query of the project of the request at a commit.
        /// </summary>
        /// <param name="queryId">The identifier of the query.</param>
        /// <param name="commitId">The identifier of the commit, or <c>null</c> for the head of the default branch.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of data that satisfy the query, or the errors that prevented executing it.</returns>
        /// <exception cref="InvalidOperationException">No project is in scope.</exception>
        /// <exception cref="NotSupportedException">The query selects or orders by properties, which the data store contract cannot apply.</exception>
        public Task<ErrorOr<PagedResult<IData>>> ExecuteQueryByIdAsync(Guid queryId, Guid? commitId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            var projectId = this.ProjectScope.ProjectId;

            return this.ReadInProjectAsync(projectId, async transaction =>
            {
                var query = await this.ReadQueryAsync(transaction, projectId, queryId, cancellationToken);

                return query.IsError ? query.Errors : await this.ExecuteAsync(transaction, projectId, query.Value, commitId, queryParameters, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Gets one page of the queries of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of queries, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<PagedResult<Query>>> GetQueriesAsync(Guid projectId, QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId,
                transaction => this.QueryRepository.ReadByFilterAsync(transaction, SearchFilter.EqualTo(ProjectProperty, projectId), queryParameters, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Gets a query of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryId">The identifier of the query.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The query, or the errors that prevented reading it.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public Task<ErrorOr<Query>> GetQueryByIdAsync(Guid projectId, Guid queryId, CancellationToken cancellationToken)
        {
            return this.ReadInProjectAsync(projectId, transaction => this.ReadQueryAsync(transaction, projectId, queryId, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Replaces the definition of a query of the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="updateQuery">The query in its updated state, identified by its identifier.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The updated query, or the errors that prevented updating it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="updateQuery"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        public async Task<ErrorOr<Query>> UpdateQueryAsync(Guid projectId, Query updateQuery, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(updateQuery);

            var condition = ToSearchFilter(updateQuery.Where);

            if (condition.IsError)
            {
                return condition.Errors;
            }

            return await this.WriteInProjectAsync(projectId, async transaction =>
            {
                var existing = await this.ReadQueryAsync(transaction, projectId, updateQuery.Id, cancellationToken);

                if (existing.IsError)
                {
                    return existing.Errors;
                }

                var nameIsFree = await this.ApplyDefinitionAsync(transaction, existing.Value, updateQuery, cancellationToken);

                return nameIsFree.IsError ? nameIsFree.Errors : await this.QueryRepository.UpdateAsync(transaction, existing.Value, cancellationToken);
            }, cancellationToken);
        }

        /// <summary>
        /// Translates the <c>where</c> of a query into the <see cref="SearchFilter"/> the data store evaluates.
        /// </summary>
        /// <param name="constraint">The <see cref="Constraint"/> to translate, possibly <c>null</c>.</param>
        /// <returns>The <see cref="SearchFilter"/>, an empty list when there is no constraint, or the validation error of the constraint.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.1.4: a primitive constraint names a property, and a composite one joins at least two constraints.
        /// </remarks>
        private static ErrorOr<List<SearchFilter>> ToSearchFilter(Constraint constraint)
        {
            switch (constraint)
            {
                case null:
                    return new List<SearchFilter>();

                case PrimitiveConstraint primitive when string.IsNullOrWhiteSpace(primitive.Property):
                    return Error.Validation("query.where.property.missing", "A primitive constraint names the property it constrains.");

                case PrimitiveConstraint primitive:
                    return new List<SearchFilter>
                    {
                        new PrimitiveSearchFilter
                        {
                            Property = primitive.Property,
                            Operator = primitive.Operator,
                            Value = [.. (primitive.Value ?? []).Select(ConstraintValue.From)],
                            Inverse = primitive.Inverse
                        }
                    };

                case CompositeConstraint composite when (composite.Constraint?.Count ?? 0) < 2:
                    return Error.Validation("query.where.composite.invalid", "A composite constraint joins at least two constraints.");

                case CompositeConstraint composite:
                    List<SearchFilter> filters = [];

                    foreach (var part in composite.Constraint)
                    {
                        var filter = ToSearchFilter(part);

                        if (filter.IsError)
                        {
                            return filter.Errors;
                        }

                        if (filter.Value.Count == 0)
                        {
                            return Error.Validation("query.where.composite.invalid", "A composite constraint does not join an empty constraint.");
                        }

                        filters.AddRange(filter.Value);
                    }

                    return new List<SearchFilter> { new CompositeSearchFilter { Filters = filters, Operator = composite.Operator } };

                default:
                    return Error.Validation("query.where.invalid", $"A {constraint.GetType().Name} is not a constraint a query can hold.");
            }
        }

        /// <summary>
        /// Copies the definition of a requested query onto the stored one, after verifying its name is free in the project.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="target">The stored <see cref="Query"/>.</param>
        /// <param name="source">The requested <see cref="Query"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or <see cref="ErrorType.Conflict"/> when another query of the project carries the name.</returns>
        private async Task<ErrorOr<Success>> ApplyDefinitionAsync(IDataStoreTransaction transaction, Query target, Query source, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(source.Name))
            {
                var nameIsFree = await VerifyNameIsFreeAsync(this.QueryRepository, transaction, ProjectProperty, target.Project, source.Name, target.Id, cancellationToken);

                if (nameIsFree.IsError)
                {
                    return nameIsFree.Errors;
                }
            }

            target.Name = source.Name;
            target.Alias = AliasIncludingName(source.Alias, source.Name);
            target.Description = source.Description;
            target.Select = [.. source.Select ?? []];
            target.Scope = [.. source.Scope ?? []];
            target.Where = source.Where;
            target.OrderBy = [.. source.OrderBy ?? []];

            return Result.Success;
        }

        /// <summary>
        /// Executes a query of a project at a commit.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the reads take part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="query">The query to execute.</param>
        /// <param name="commitId">The identifier of the commit, or <c>null</c> for the head of the default branch.</param>
        /// <param name="queryParameters">The paging parameters applied to the result.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The page of data that satisfy the query, or the errors that prevented executing it.</returns>
        /// <exception cref="NotSupportedException">The query selects or orders by properties.</exception>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.1.4: an empty scope is the whole project, otherwise the data are among the scope.
        /// </remarks>
        private async Task<ErrorOr<PagedResult<IData>>> ExecuteAsync(IDataStoreTransaction transaction, Guid projectId, Query query, Guid? commitId,
            QueryParameters queryParameters, CancellationToken cancellationToken)
        {
            if ((query.Select?.Count ?? 0) > 0 || (query.OrderBy?.Count ?? 0) > 0)
            {
                throw new NotSupportedException("A query that selects or orders by properties cannot be executed by the data store contract yet.");
            }

            var condition = ToSearchFilter(query.Where);

            if (condition.IsError)
            {
                return condition.Errors;
            }

            var commit = commitId.HasValue
                ? await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, commitId.Value, cancellationToken)
                : await this.VersioningService.GetHeadCommitAsync(projectId, null, cancellationToken);

            if (commit.IsError)
            {
                return commit.Errors;
            }

            List<SearchFilter> filters = [.. condition.Value];

            if ((query.Scope?.Count ?? 0) > 0)
            {
                filters.Add(SearchFilter.In(IdProperty, [.. query.Scope.Select(ConstraintValue.From)]));
            }

            return filters.Count switch
            {
                0 => await this.DataRepository.ReadAsync<IData>(transaction, commit.Value.Id, queryParameters, cancellationToken),
                1 => await this.DataRepository.ReadMatchingAsync(transaction, commit.Value.Id, filters[0], queryParameters, cancellationToken),
                _ => await this.DataRepository.ReadMatchingAsync(transaction, commit.Value.Id, SearchFilter.And([.. filters]), queryParameters, cancellationToken)
            };
        }

        /// <summary>
        /// Reads a query of a project.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="queryId">The identifier of the query.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The query, or the errors that prevented reading it.</returns>
        private async Task<ErrorOr<Query>> ReadQueryAsync(IDataStoreTransaction transaction, Guid projectId, Guid queryId, CancellationToken cancellationToken)
        {
            var query = await this.QueryRepository.ReadByIdAsync(transaction, queryId, cancellationToken);

            return WithinProject(query, record => record.Project, queryId, projectId);
        }
    }
}
