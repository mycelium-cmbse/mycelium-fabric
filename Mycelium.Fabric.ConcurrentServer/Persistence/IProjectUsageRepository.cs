// ------------------------------------------------------------------------------------------------
//  <copyright file="IProjectUsageRepository.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using ErrorOr;

    using SysML2.NET.PIM.DTO;

    /// <summary>
    /// Defines the only read that crosses the project of the request: that of the commit a project usage uses.
    /// </summary>
    /// <remarks>
    /// Systems Modeling API and Services 1.0 §7.2.6 lets a project use a commit of another project. Every other repository stays
    /// confined to the project of the request; navigation does not follow usages, since Annex A (PIM-EN-002 to PIM-EN-005) reads
    /// only the data of the commit itself.
    /// </remarks>
    public interface IProjectUsageRepository : IBaseRepository
    {
        /// <summary>
        /// Reads a commit of any project, as the commit a project usage is about to use.
        /// </summary>
        /// <param name="transaction">The <see cref="IDataStoreTransaction"/> the operation takes part in.</param>
        /// <param name="commitId">The identifier of the commit.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that cancels the operation.</param>
        /// <returns>The outcome of the operation, or the errors that prevented it.</returns>
        Task<ErrorOr<Commit>> ReadUsedCommitAsync(IDataStoreTransaction transaction, Guid commitId, CancellationToken cancellationToken);
    }
}
