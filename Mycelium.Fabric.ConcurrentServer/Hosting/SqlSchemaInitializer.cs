// ------------------------------------------------------------------------------------------------
//  <copyright file="SqlSchemaInitializer.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Hosting
{
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    /// <summary>
    /// Migrates the SysML v2 schema at host start-up, before the server accepts requests.
    /// </summary>
    public class SqlSchemaInitializer : IHostedService
    {
        /// <summary>
        /// The migrator that applies the pending schema scripts.
        /// </summary>
        private readonly ISqlSchemaMigrator migrator;

        /// <summary>
        /// The logger reporting the outcome of the migration.
        /// </summary>
        private readonly ILogger<SqlSchemaInitializer> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlSchemaInitializer"/> class.
        /// </summary>
        /// <param name="migrator">The migrator that applies the pending schema scripts.</param>
        /// <param name="logger">The logger reporting the outcome of the migration.</param>
        public SqlSchemaInitializer(ISqlSchemaMigrator migrator, ILogger<SqlSchemaInitializer> logger)
        {
            this.migrator = migrator;
            this.logger = logger;
        }

        /// <summary>
        /// Applies the pending schema scripts; a failing migration stops the host.
        /// </summary>
        /// <param name="cancellationToken">Signals that the start-up has been aborted.</param>
        /// <returns>A <see cref="Task"/> that completes once the schema is up to date.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var appliedScripts = await this.migrator.MigrateAsync(cancellationToken);

            if (this.logger.IsEnabled(LogLevel.Information))
            {
                this.logger.LogInformation("SQL schema is up to date, {AppliedCount} script(s) applied", appliedScripts.Count);
            }
        }

        /// <summary>
        /// Does nothing, since the schema is migrated once at start-up.
        /// </summary>
        /// <param name="cancellationToken">Signals that the shutdown is no longer graceful.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
