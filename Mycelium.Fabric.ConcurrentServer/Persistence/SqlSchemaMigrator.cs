// ------------------------------------------------------------------------------------------------
//  <copyright file="SqlSchemaMigrator.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Persistence
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Npgsql;

    using SysML2.NET.Dal;

    /// <summary>
    /// Applies the <see cref="SqlSchemaScripts"/> to PostgreSQL, tracking the applied ones in <c>public.schema_migration</c>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SqlSchemaMigrator : ISqlSchemaMigrator
    {
        /// <summary>
        /// The name of the connection string to the database that holds the SysML v2 schema.
        /// </summary>
        public const string ConnectionStringName = "concurrentserverdata";

        /// <summary>
        /// The command timeout of one script, sized for the whole-schema DDL of the initial script.
        /// </summary>
        private const int ScriptTimeoutInSeconds = 600;

        /// <summary>
        /// Serialises concurrent migrations on a transaction-scoped advisory lock, then ensures the applied-script history exists.
        /// </summary>
        private const string PrepareHistorySql = """
            SELECT pg_advisory_xact_lock(hashtext('mycelium_fabric.schema_migration'));
            CREATE TABLE IF NOT EXISTS public.schema_migration
            (
                script_name text PRIMARY KEY,
                applied_at timestamptz NOT NULL DEFAULT now()
            );
            """;

        /// <summary>
        /// Reads the names of the scripts already applied.
        /// </summary>
        private const string ReadHistorySql = "SELECT script_name FROM public.schema_migration";

        /// <summary>
        /// Records one script as applied.
        /// </summary>
        private const string RecordScriptSql = "INSERT INTO public.schema_migration (script_name) VALUES ($1)";

        /// <summary>
        /// The data source of the database that holds the SysML v2 schema.
        /// </summary>
        private readonly NpgsqlDataSource dataSource;

        /// <summary>
        /// The logger reporting the applied scripts.
        /// </summary>
        private readonly ILogger<SqlSchemaMigrator> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlSchemaMigrator"/> class.
        /// </summary>
        /// <param name="dataSource">The data source of the database that holds the SysML v2 schema.</param>
        /// <param name="logger">The logger reporting the applied scripts.</param>
        public SqlSchemaMigrator(NpgsqlDataSource dataSource, ILogger<SqlSchemaMigrator> logger)
        {
            this.dataSource = dataSource;
            this.logger = logger;
        }

        /// <summary>
        /// Applies, in name order and within one transaction, every script the database has not recorded as applied, so a failing script leaves the database untouched.
        /// </summary>
        /// <param name="cancellationToken">Signals that the migration has been aborted.</param>
        /// <returns>The names of the scripts applied, empty when the schema was already up to date.</returns>
        public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken)
        {
            await using var connection = await this.dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var prepare = new NpgsqlCommand(PrepareHistorySql, connection, transaction))
            {
                await prepare.ExecuteNonQueryAsync(cancellationToken);
            }

            var appliedScripts = await ReadAppliedScriptsAsync(connection, transaction, cancellationToken);
            var pendingScripts = SqlSchemaScripts.GetAll().Where(script => !appliedScripts.Contains(script.Name)).ToList();

            foreach (var (name, sql) in pendingScripts)
            {
                if (this.logger.IsEnabled(LogLevel.Information))
                {
                    this.logger.LogInformation("Applying SQL schema script {ScriptName}", name);
                }

                await ApplyScriptAsync(connection, transaction, name, sql, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return pendingScripts.Select(script => script.Name).ToList();
        }

        /// <summary>
        /// Runs one script and records it as applied.
        /// </summary>
        /// <param name="connection">The open connection.</param>
        /// <param name="transaction">The transaction the whole migration runs in.</param>
        /// <param name="name">The script name.</param>
        /// <param name="sql">The script text.</param>
        /// <param name="cancellationToken">Signals that the migration has been aborted.</param>
        /// <returns>A <see cref="Task"/> that completes once the script is applied and recorded.</returns>
        private static async Task ApplyScriptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string name, string sql, CancellationToken cancellationToken)
        {
            await using (var script = new NpgsqlCommand(sql, connection, transaction))
            {
                script.CommandTimeout = ScriptTimeoutInSeconds;
                await script.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var record = new NpgsqlCommand(RecordScriptSql, connection, transaction);
            record.Parameters.AddWithValue(name);
            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Reads the names of the scripts already applied.
        /// </summary>
        /// <param name="connection">The open connection.</param>
        /// <param name="transaction">The transaction the whole migration runs in.</param>
        /// <param name="cancellationToken">Signals that the migration has been aborted.</param>
        /// <returns>The applied script names.</returns>
        private static async Task<HashSet<string>> ReadAppliedScriptsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
        {
            await using var command = new NpgsqlCommand(ReadHistorySql, connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var appliedScripts = new HashSet<string>();

            while (await reader.ReadAsync(cancellationToken))
            {
                appliedScripts.Add(reader.GetString(0));
            }

            return appliedScripts;
        }
    }
}
