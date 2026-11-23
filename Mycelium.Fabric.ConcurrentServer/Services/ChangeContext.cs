// ------------------------------------------------------------------------------------------------
//  <copyright file="ChangeContext.cs" company="Starion Group S.A.">
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

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using SysML2.NET.Common;
    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// The <see cref="IChangeContext"/> of a change resolved against the state at the previous commits.
    /// </summary>
    public class ChangeContext : IChangeContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeContext"/> class.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> of the commit.</param>
        /// <param name="change">The resolved <see cref="DataVersion"/>s, each naming a distinct data identity.</param>
        /// <param name="previousState">The state at the previous commits, by data identity.</param>
        /// <param name="dataRepository">The <see cref="IDataRepository"/> that reads the data of the used projects.</param>
        /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
        public ChangeContext(IDataStoreTransaction transaction, IReadOnlyList<DataVersion> change, IReadOnlyDictionary<Guid, DataVersion> previousState,
            IDataRepository dataRepository)
        {
            this.Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            this.Change = change ?? throw new ArgumentNullException(nameof(change));
            this.PreviousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
            this.DataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            this.ChangeByIdentity = change.ToDictionary(version => version.Identity.Id);
            this.UsedProjectCommitIds = [.. this.ResultingData().OfType<ProjectUsage>().Select(usage => usage.UsedProjectCommit).Distinct()];
        }

        /// <summary>
        /// Gets the <see cref="DataVersion"/>s the commit writes, a version without payload being a deletion.
        /// </summary>
        public IReadOnlyList<DataVersion> Change { get; }

        /// <summary>
        /// Gets the state at the previous commits, by data identity.
        /// </summary>
        public IReadOnlyDictionary<Guid, DataVersion> PreviousState { get; }

        /// <summary>
        /// Gets the <see cref="DataVersion"/>s of the change, by data identity.
        /// </summary>
        private Dictionary<Guid, DataVersion> ChangeByIdentity { get; }

        /// <summary>
        /// Gets the <see cref="IDataRepository"/> that reads the data of the used projects.
        /// </summary>
        private IDataRepository DataRepository { get; }

        /// <summary>
        /// Gets the <see cref="IDataStoreTransaction"/> of the commit, in which the reads of the used projects take part.
        /// </summary>
        private IDataStoreTransaction Transaction { get; }

        /// <summary>
        /// Gets the identifiers of the commits that the project usages of the committed state pin.
        /// </summary>
        private List<Guid> UsedProjectCommitIds { get; }

        /// <summary>
        /// Gets, in one call, the data with any of the given identities that a change can reference once it is committed.
        /// </summary>
        /// <param name="identityIds">The identifiers of the data identities.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The data found, by identity, none for an identity that cannot be referenced, or the errors that prevented reading it.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="identityIds"/> is <c>null</c>.</exception>
        /// <remarks>
        /// The change prevails over the previous state, as Systems Modeling API and Services 1.0 §7.1.2 computes <c>versionedData</c>. An identity
        /// the project does not hold is looked up at the <c>usedProjectCommit</c> of each of its project usages (§7.1.2), one read per used
        /// project; the usages of a used project are not followed, which the specification leaves open.
        /// </remarks>
        public async Task<ErrorOr<IReadOnlyDictionary<Guid, IData>>> GetDataAsync(IReadOnlyCollection<Guid> identityIds, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identityIds);

            List<Guid> requested = [.. identityIds.Distinct()];

            var found = requested.Select(identityId => (identityId, data: this.LocalData(identityId)))
                .Where(local => local.data != null)
                .ToDictionary(local => local.identityId, local => local.data);

            List<Guid> unresolved = [.. requested.Where(identityId => !this.IsHeldByProject(identityId))];

            foreach (var usedCommitId in this.UsedProjectCommitIds.TakeWhile(_ => unresolved.Count != 0))
            {
                var used = await this.DataRepository.ReadByIdsAsync<IData>(this.Transaction, usedCommitId, unresolved, cancellationToken);

                if (used.IsError)
                {
                    return used.Errors;
                }

                foreach (var data in used.Value)
                {
                    found[data.Id] = data;
                }

                unresolved.RemoveAll(found.ContainsKey);
            }

            return await ErrorOrFactory.FromAsync<IReadOnlyDictionary<Guid, IData>>(found);
        }

        /// <summary>
        /// Gives whether the project holds a data identity, in the change or at the previous commits, whether or not the change deletes it.
        /// </summary>
        /// <param name="identityId">The identifier of the data identity.</param>
        /// <returns>Whether the project holds the identity.</returns>
        private bool IsHeldByProject(Guid identityId) => this.ChangeByIdentity.ContainsKey(identityId) || this.PreviousState.ContainsKey(identityId);

        /// <summary>
        /// Gives the data of an identity of the project once the change is committed.
        /// </summary>
        /// <param name="identityId">The identifier of the data identity.</param>
        /// <returns>The data, or <c>null</c> when the project does not hold it or the change deletes it.</returns>
        private IData LocalData(Guid identityId)
        {
            var version = this.ChangeByIdentity.TryGetValue(identityId, out var changed) ? changed : this.PreviousState.GetValueOrDefault(identityId);

            return version?.Payload;
        }

        /// <summary>
        /// Gives the data of the project once the change is committed.
        /// </summary>
        /// <returns>The data the change creates or updates, and the data at the previous commits that the change does not touch.</returns>
        private IEnumerable<IData> ResultingData()
        {
            return this.Change.Select(version => version.Payload)
                .Concat(this.PreviousState.Where(entry => !this.ChangeByIdentity.ContainsKey(entry.Key)).Select(entry => entry.Value.Payload))
                .Where(data => data != null);
        }
    }
}
