// ------------------------------------------------------------------------------------------------
//  <copyright file="ServiceBase.cs" company="Starion Group S.A.">
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
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.PIM;
    using SysML2.NET.PSM;

    /// <summary>
    /// Runs the operations of a PIM service inside the transaction of the current request and confines them to its project.
    /// </summary>
    public abstract class ServiceBase
    {
        /// <summary>
        /// The name of the property that holds the name of a record.
        /// </summary>
        protected const string NameProperty = "name";

        /// <summary>
        /// The name of the property that holds the owning project of a commit, branch or tag.
        /// </summary>
        protected const string OwningProjectProperty = "owningProject";

        /// <summary>
        /// The kinds of error a caller causes, which are warnings rather than failures of the service.
        /// </summary>
        private static readonly HashSet<ErrorType> CallerErrorTypes = [ErrorType.Validation, ErrorType.NotFound, ErrorType.Conflict, ErrorType.Unauthorized, ErrorType.Forbidden];

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBase"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="logger">The <see cref="ILogger"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        protected ServiceBase(IDatabaseHandler databaseHandler, IProjectScope projectScope, IProjectRepository projectRepository, ILogger logger)
        {
            this.DatabaseHandler = databaseHandler ?? throw new ArgumentNullException(nameof(databaseHandler));
            this.ProjectScope = projectScope ?? throw new ArgumentNullException(nameof(projectScope));
            this.ProjectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the <see cref="IDatabaseHandler"/> that manages the transaction of the request.
        /// </summary>
        protected IDatabaseHandler DatabaseHandler { get; }

        /// <summary>
        /// Gets the <see cref="ILogger"/> that records the warnings and failures of the operations.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="IProjectRepository"/> of the projects.
        /// </summary>
        protected IProjectRepository ProjectRepository { get; }

        /// <summary>
        /// Gets the <see cref="IProjectScope"/> of the request.
        /// </summary>
        protected IProjectScope ProjectScope { get; }

        /// <summary>
        /// Gives the aliases of a record that includes its name.
        /// </summary>
        /// <param name="alias">The aliases supplied for the record, possibly <c>null</c>.</param>
        /// <param name="name">The name of the record, possibly <c>null</c>.</param>
        /// <returns>The aliases, with the name appended when it is not already one of them.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 §7.1.1 requires the name of a record to be one of its aliases.
        /// </remarks>
        protected static List<string> AliasIncludingName(IEnumerable<string> alias, string name)
        {
            List<string> aliases = [.. alias ?? []];

            if (!string.IsNullOrWhiteSpace(name) && !aliases.Contains(name))
            {
                aliases.Add(name);
            }

            return aliases;
        }

        /// <summary>
        /// Creates the error that a project does not exist.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <returns>The <see cref="ErrorType.NotFound"/> error.</returns>
        /// <remarks>
        /// Systems Modeling API and Services 1.0 Annex A (PIM-CC-003) reports a missing project as such, whatever the operation.
        /// </remarks>
        protected static Error ProjectNotFound(Guid projectId) => Error.NotFound("project.notFound", $"Project {projectId} does not exist.");

        /// <summary>
        /// Reads a commit that an operation takes as an input reference, such as the commit its data is read at.
        /// </summary>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> of the commits.</param>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The commit, <see cref="ErrorType.NotFound"/> when it does not exist, or <see cref="ErrorType.Validation"/> when another project owns it.</returns>
        protected static async Task<ErrorOr<SysML2.NET.PIM.DTO.Commit>> ReadReferencedCommitAsync(ICommitRepository commitRepository, IDataStoreTransaction transaction,
            Guid projectId, Guid commitId, CancellationToken cancellationToken)
        {
            var commit = await commitRepository.ReadByIdAsync(transaction, commitId, cancellationToken);

            return ReferencedWithinProject(commit, record => record.OwningProject, commitId, projectId);
        }

        /// <summary>
        /// Verifies that no other live record of a project carries a name.
        /// </summary>
        /// <typeparam name="TRecord">The type of record that is named.</typeparam>
        /// <param name="repository">The <see cref="IRecordRepository{TRecord}"/> of the records.</param>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the read takes part in.</param>
        /// <param name="projectProperty">The name of the property that holds the owning project of a record.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="name">The name that is verified.</param>
        /// <param name="recordId">The identifier of the record that is named, which may already carry the name.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>Success, or <see cref="ErrorType.Conflict"/> when another record carries the name.</returns>
        /// <remarks>
        /// The specification is silent on name uniqueness; the store keeps the names of live branches, tags and queries unique within a project.
        /// This read reports the common case early, and the store's unique index settles two concurrent writes (see <see cref="IRecordRepository{TRecord}"/>).
        /// </remarks>
        protected static async Task<ErrorOr<Success>> VerifyNameIsFreeAsync<TRecord>(IRecordRepository<TRecord> repository, IDataStoreTransaction transaction,
            string projectProperty, Guid projectId, string name, Guid recordId, CancellationToken cancellationToken) where TRecord : Record
        {
            var filter = SearchFilter.And(SearchFilter.EqualTo(projectProperty, projectId), SearchFilter.EqualTo(NameProperty, name));
            var homonyms = await repository.ReadByFilterAsync(transaction, filter, new QueryParameters { PageSize = 2 }, cancellationToken);

            if (homonyms.IsError)
            {
                return homonyms.Errors;
            }

            return homonyms.Value.Records.Any(record => record.Id != recordId)
                ? Error.Conflict($"{typeof(TRecord).Name.ToLowerInvariant()}.name.taken", $"Project {projectId} already has a {typeof(TRecord).Name} named '{name}'.")
                : Result.Success;
        }

        /// <summary>
        /// Narrows a record that an operation reads by its identifier to the given project.
        /// </summary>
        /// <typeparam name="TRecord">The type of record that is read.</typeparam>
        /// <param name="record">The outcome of the read.</param>
        /// <param name="owningProject">Selects the project that owns the record.</param>
        /// <param name="recordId">The identifier of the record that was read.</param>
        /// <param name="projectId">The identifier of the project the record must belong to.</param>
        /// <returns>The record, the errors of the read, or <see cref="ErrorType.NotFound"/> when another project owns it.</returns>
        /// <remarks>
        /// Annex A reports such a record as not existing in the inputted project (PIM-PCB-004, PIM-PCB-006, PIM-PCB-012).
        /// </remarks>
        protected static ErrorOr<TRecord> WithinProject<TRecord>(ErrorOr<TRecord> record, Func<TRecord, Guid> owningProject, Guid recordId, Guid projectId)
        {
            if (record.IsError)
            {
                return record.Errors;
            }

            if (owningProject(record.Value) == projectId)
            {
                return record;
            }

            var recordName = typeof(TRecord).Name;

            return Error.NotFound($"{char.ToLowerInvariant(recordName[0])}{recordName[1..]}.notFound", $"The {recordName} {recordId} does not exist in project {projectId}.");
        }

        /// <summary>
        /// Narrows a record that an operation takes as an input reference to the given project.
        /// </summary>
        /// <typeparam name="TRecord">The type of record that is referenced.</typeparam>
        /// <param name="record">The outcome of the read.</param>
        /// <param name="owningProject">Selects the project that owns the record.</param>
        /// <param name="recordId">The identifier of the record that was read.</param>
        /// <param name="projectId">The identifier of the project the record must belong to.</param>
        /// <returns>The record, the errors of the read, or <see cref="ErrorType.Validation"/> when another project owns it.</returns>
        /// <remarks>
        /// Annex A reports such a reference as an invalid input (PIM-CC-005, PIM-PCB-015) and a missing one as not existing (PIM-CC-004).
        /// </remarks>
        protected static ErrorOr<TRecord> ReferencedWithinProject<TRecord>(ErrorOr<TRecord> record, Func<TRecord, Guid> owningProject, Guid recordId, Guid projectId)
        {
            if (record.IsError || owningProject(record.Value) == projectId)
            {
                return record;
            }

            var recordName = typeof(TRecord).Name;

            return Error.Validation($"{char.ToLowerInvariant(recordName[0])}{recordName[1..]}.invalid",
                $"The {recordName} {recordId} is not a {recordName} of project {projectId}.");
        }

        /// <summary>
        /// Runs a read inside the transaction of the request, opening a read-only one when none is open.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the read.</typeparam>
        /// <param name="operation">The read, given the <see cref="IDataStoreTransaction"/> it takes part in.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <param name="operationName">The name of the operation, as logged.</param>
        /// <returns>The result of the read, or the errors that prevented it.</returns>
        protected Task<ErrorOr<TResult>> ReadAsync<TResult>(Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> operation, CancellationToken cancellationToken,
            [CallerMemberName] string operationName = "")
        {
            return this.ExecuteAsync(TransactionMode.ReadOnly, operation, operationName, cancellationToken);
        }

        /// <summary>
        /// Runs a read of the project of the request, once the project is verified to exist.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the read.</typeparam>
        /// <param name="projectId">The identifier of the project the operation names.</param>
        /// <param name="operation">The read, given the <see cref="IDataStoreTransaction"/> it takes part in.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <param name="operationName">The name of the operation, as logged.</param>
        /// <returns>The result of the read, <see cref="ErrorType.NotFound"/> when the project does not exist, or the errors that prevented it.</returns>
        /// <exception cref="InvalidOperationException">No project is in scope, or <paramref name="projectId"/> is not the project of the request.</exception>
        protected Task<ErrorOr<TResult>> ReadInProjectAsync<TResult>(Guid projectId, Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> operation,
            CancellationToken cancellationToken, [CallerMemberName] string operationName = "")
        {
            this.ScopedProjectId(projectId);

            return this.ExecuteAsync(TransactionMode.ReadOnly, transaction => this.InExistingProjectAsync(transaction, projectId, operation, cancellationToken), operationName,
                cancellationToken);
        }

        /// <summary>
        /// Asserts that the project an operation names is the project of the request.
        /// </summary>
        /// <param name="projectId">The identifier of the project the operation names.</param>
        /// <returns>The identifier of the project.</returns>
        /// <exception cref="InvalidOperationException">No project is in scope, or the operation names another one.</exception>
        protected Guid ScopedProjectId(Guid projectId)
        {
            var scopedProjectId = this.ProjectScope.ProjectId;

            if (projectId == scopedProjectId)
            {
                return projectId;
            }

            if (this.Logger.IsEnabled(LogLevel.Error))
            {
                this.Logger.LogError("An operation named project {ProjectId} while the request is scoped to project {ScopedProjectId}", projectId, scopedProjectId);
            }

            throw new InvalidOperationException($"The operation names project {projectId} while the request is scoped to project {scopedProjectId}.");
        }

        /// <summary>
        /// Runs a write inside the transaction of the request, opening one when none is open.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the write.</typeparam>
        /// <param name="operation">The write, given the <see cref="IDataStoreTransaction"/> it takes part in.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <param name="operationName">The name of the operation, as logged.</param>
        /// <returns>The result of the write, or the errors that prevented it.</returns>
        protected Task<ErrorOr<TResult>> WriteAsync<TResult>(Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> operation, CancellationToken cancellationToken,
            [CallerMemberName] string operationName = "")
        {
            return this.ExecuteAsync(TransactionMode.ReadWrite, operation, operationName, cancellationToken);
        }

        /// <summary>
        /// Runs a write in the project of the request, once the project is verified to exist.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the write.</typeparam>
        /// <param name="projectId">The identifier of the project the operation names.</param>
        /// <param name="operation">The write, given the <see cref="IDataStoreTransaction"/> it takes part in.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <param name="operationName">The name of the operation, as logged.</param>
        /// <returns>The result of the write, <see cref="ErrorType.NotFound"/> when the project does not exist, or the errors that prevented it.</returns>
        /// <exception cref="InvalidOperationException">No project is in scope, or <paramref name="projectId"/> is not the project of the request.</exception>
        protected Task<ErrorOr<TResult>> WriteInProjectAsync<TResult>(Guid projectId, Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> operation,
            CancellationToken cancellationToken, [CallerMemberName] string operationName = "")
        {
            this.ScopedProjectId(projectId);

            return this.ExecuteAsync(TransactionMode.ReadWrite, transaction => this.InExistingProjectAsync(transaction, projectId, operation, cancellationToken), operationName,
                cancellationToken);
        }

        /// <summary>
        /// Runs an operation inside the transaction of the request, and logs its errors and exceptions when it owns the transaction.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
        /// <param name="mode">The <see cref="TransactionMode"/> the operation requires.</param>
        /// <param name="operation">The operation, given the <see cref="IDataStoreTransaction"/> it takes part in.</param>
        /// <param name="operationName">The name of the operation, as logged.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The result of the operation, or the errors that prevented it.</returns>
        /// <remarks>
        /// A nested operation hands its errors to the operation that opened the transaction, which logs them once.
        /// </remarks>
        private async Task<ErrorOr<TResult>> ExecuteAsync<TResult>(TransactionMode mode, Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> operation, string operationName,
            CancellationToken cancellationToken)
        {
            var isOwner = !this.DatabaseHandler.HasActiveTransaction;

            try
            {
                var outcome = await this.ExecuteInTransactionAsync(mode, operation, cancellationToken);

                if (outcome.IsError && isOwner)
                {
                    this.LogErrors(operationName, outcome.Errors);
                }

                return outcome;
            }
            catch (Exception exception) when (isOwner && this.LogException(operationName, exception, cancellationToken))
            {
                throw;
            }
        }

        /// <summary>
        /// Runs an operation inside the transaction of the request, then commits it on success and rolls it back on failure.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
        /// <param name="mode">The <see cref="TransactionMode"/> the operation requires.</param>
        /// <param name="operation">The operation, given the <see cref="IDataStoreTransaction"/> it takes part in.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The result of the operation, or the errors that prevented it.</returns>
        private async Task<ErrorOr<TResult>> ExecuteInTransactionAsync<TResult>(TransactionMode mode, Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> operation,
            CancellationToken cancellationToken)
        {
            var scope = await this.DatabaseHandler.BeginTransactionAsync(mode, cancellationToken);

            if (scope.IsError)
            {
                return scope.Errors;
            }

            await using var transactionScope = scope.Value;

            var outcome = await operation(transactionScope.Transaction);

            if (outcome.IsError)
            {
                var rollback = await transactionScope.RollbackAsync(cancellationToken);

                return rollback.IsError ? outcome.Errors.Concat(rollback.Errors).ToList() : outcome;
            }

            var commit = await transactionScope.CommitAsync(cancellationToken);

            return commit.IsError ? commit.Errors : outcome;
        }

        /// <summary>
        /// Runs an operation once the project it is confined to is verified to exist.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The result of the operation, or <see cref="ErrorType.NotFound"/> when the project does not exist.</returns>
        private async Task<ErrorOr<TResult>> InExistingProjectAsync<TResult>(IDataStoreTransaction transaction, Guid projectId,
            Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> operation, CancellationToken cancellationToken)
        {
            var project = await this.ProjectRepository.ReadByIdAsync(transaction, projectId, cancellationToken);

            if (project.IsError)
            {
                return project.FirstError.Type == ErrorType.NotFound ? ProjectNotFound(projectId) : project.Errors;
            }

            return await operation(transaction);
        }

        /// <summary>
        /// Logs the errors of an operation, as a warning when the caller caused all of them and as an error otherwise.
        /// </summary>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="errors">The errors of the operation.</param>
        private void LogErrors(string operationName, List<Error> errors)
        {
            var level = errors.TrueForAll(error => CallerErrorTypes.Contains(error.Type)) ? LogLevel.Warning : LogLevel.Error;

            if (this.Logger.IsEnabled(level))
            {
                this.Logger.Log(level, "{Operation} failed with {ErrorCodes}: {ErrorDescriptions}", operationName, string.Join(", ", errors.Select(error => error.Code)),
                    string.Join(" ", errors.Select(error => error.Description)));
            }
        }

        /// <summary>
        /// Logs an exception that escapes an operation, unless it is the cancellation the caller requested.
        /// </summary>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="exception">The <see cref="Exception"/> that escapes.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> of the operation.</param>
        /// <returns>Always <c>false</c>, so that the exception filter lets the exception propagate untouched.</returns>
        private bool LogException(string operationName, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (this.Logger.IsEnabled(LogLevel.Error))
            {
                this.Logger.LogError(exception, "{Operation} threw {ExceptionType}", operationName, exception.GetType().Name);
            }

            return false;
        }
    }
}
