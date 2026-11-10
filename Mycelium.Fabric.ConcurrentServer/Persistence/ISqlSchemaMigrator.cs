// ------------------------------------------------------------------------------------------------
//  <copyright file="ISqlSchemaMigrator.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using SysML2.NET.Dal;

    /// <summary>
    /// Brings the database up to date with the <see cref="SqlSchemaScripts"/> embedded in SysML2.NET.Dal.
    /// </summary>
    public interface ISqlSchemaMigrator
    {
        /// <summary>
        /// Applies, in name order and within one transaction, every script the database has not recorded as applied.
        /// </summary>
        /// <param name="cancellationToken">Signals that the migration has been aborted.</param>
        /// <returns>The names of the scripts applied, empty when the schema was already up to date.</returns>
        Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken);
    }
}
