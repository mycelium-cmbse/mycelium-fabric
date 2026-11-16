// ------------------------------------------------------------------------------------------------
//  <copyright file="VersionedDataServiceBase.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Services
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    /// <summary>
    /// Runs the operations of a PIM service over the data versioned at the commits of the project of the request.
    /// </summary>
    public abstract class VersionedDataServiceBase : ServiceBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VersionedDataServiceBase"/> class.
        /// </summary>
        /// <param name="databaseHandler">The <see cref="IDatabaseHandler"/> that manages the transaction of the request.</param>
        /// <param name="projectScope">The <see cref="IProjectScope"/> of the request.</param>
        /// <param name="projectRepository">The <see cref="IProjectRepository"/> of the projects.</param>
        /// <param name="commitRepository">The <see cref="ICommitRepository"/> that reads the commit the data is read at.</param>
        /// <param name="dataRepository">The <see cref="IDataRepository"/> of the versioned data.</param>
        /// <param name="versioningService">The <see cref="IProjectDataVersioningService"/> that resolves commits and commits changes.</param>
        /// <param name="logger">The <see cref="ILogger"/> that records the warnings and failures of the operations.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        protected VersionedDataServiceBase(IDatabaseHandler databaseHandler, IProjectScope projectScope, IProjectRepository projectRepository, ICommitRepository commitRepository,
            IDataRepository dataRepository,
            IProjectDataVersioningService versioningService, ILogger logger)
            : base(databaseHandler, projectScope, projectRepository, logger)
        {
            this.CommitRepository = commitRepository ?? throw new ArgumentNullException(nameof(commitRepository));
            this.DataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            this.VersioningService = versioningService ?? throw new ArgumentNullException(nameof(versioningService));
        }

        /// <summary>
        /// Gets the <see cref="ICommitRepository"/> that reads the commit the data is read at.
        /// </summary>
        protected ICommitRepository CommitRepository { get; }

        /// <summary>
        /// Gets the <see cref="IDataRepository"/> of the versioned data.
        /// </summary>
        protected IDataRepository DataRepository { get; }

        /// <summary>
        /// Gets the <see cref="IProjectDataVersioningService"/> that resolves commits and commits changes, inside the transaction of the request.
        /// </summary>
        protected IProjectDataVersioningService VersioningService { get; }

        /// <summary>
        /// Runs a read at a commit, once the commit is verified to belong to the project of the request.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the read.</typeparam>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="read">The read, given the <see cref="IDataStoreTransaction"/> it takes part in.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <param name="operationName">The name of the operation, as logged.</param>
        /// <returns>The result of the read, or the errors of the project or commit when either cannot be read.</returns>
        /// <exception cref="InvalidOperationException"><paramref name="projectId"/> is not the project of the request.</exception>
        protected Task<ErrorOr<TResult>> ReadAtCommitAsync<TResult>(Guid projectId, Guid commitId, Func<IDataStoreTransaction, Task<ErrorOr<TResult>>> read,
            CancellationToken cancellationToken, [CallerMemberName] string operationName = "")
        {
            return this.ReadInProjectAsync(projectId, async transaction =>
            {
                var commit = await ReadReferencedCommitAsync(this.CommitRepository, transaction, projectId, commitId, cancellationToken);

                return commit.IsError ? commit.Errors : await read(transaction);
            }, cancellationToken, operationName);
        }
    }
}
