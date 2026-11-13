// ------------------------------------------------------------------------------------------------
//  <copyright file="FunctionalDataInitializer.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Mycelium.SDK.Dal;

    using Npgsql;

    /// <summary>
    /// Applies the <see cref="SqlSchemaScripts"/> packaged in <c>Mycelium.SDK.Dal</c> to the Functional Data database, tracking the applied ones in
    /// <c>public.schema_migration</c>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class FunctionalDataInitializer : IFunctionalDataInitializer
    {
        /// <summary>
        /// The name of the connection string to the Functional Data database.
        /// </summary>
        public const string ConnectionStringName = "functionaldata";

        /// <summary>
        /// Serializes concurrent initializers on a transaction-scoped advisory lock, then ensures the applied-script history exists.
        /// </summary>
        private const string PrepareHistorySql = """
            SELECT pg_advisory_xact_lock(33420001);
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
        /// The data source of the Functional Data database.
        /// </summary>
        private readonly NpgsqlDataSource dataSource;

        /// <summary>
        /// The configuration supplying the initialization deadline.
        /// </summary>
        private readonly IConfiguration configuration;

        /// <summary>
        /// The logger receiving secret-free initialization diagnostics.
        /// </summary>
        private readonly ILogger<FunctionalDataInitializer> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionalDataInitializer"/> class.
        /// </summary>
        /// <param name="dataSource">The data source of the Functional Data database.</param>
        /// <param name="configuration">The service configuration.</param>
        /// <param name="logger">The initialization logger.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public FunctionalDataInitializer(NpgsqlDataSource dataSource, IConfiguration configuration, ILogger<FunctionalDataInitializer> logger)
        {
            ArgumentNullException.ThrowIfNull(dataSource);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);

            this.dataSource = dataSource;
            this.configuration = configuration;
            this.logger = logger;
        }

        /// <summary>
        /// Applies, in name order and within one locked transaction, every packaged script the database has not recorded as applied.
        /// </summary>
        /// <param name="cancellationToken">The token canceling initialization.</param>
        /// <returns>A task that completes after the initialization transaction commits.</returns>
        /// <exception cref="InvalidOperationException">Configuration is invalid, or a script or its history cannot be applied.</exception>
        /// <exception cref="OperationCanceledException">Initialization is canceled or exceeds its configured deadline.</exception>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var timeout = this.ReadInitializationTimeout();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            deadline.CancelAfter(timeout);

            var token = deadline.Token;
            var commandTimeout = (int)timeout.TotalSeconds;

            try
            {
                token.ThrowIfCancellationRequested();

                this.LogStep("opening the Functional Data database");

                await using var connection = await this.dataSource.OpenConnectionAsync(token);

                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);

                this.LogStep("acquiring the initialization lock and preparing the script history");

                await using (var prepare = new NpgsqlCommand(PrepareHistorySql, connection, transaction) { CommandTimeout = commandTimeout })
                {
                    await prepare.ExecuteNonQueryAsync(token);
                }

                this.LogStep("reading the applied scripts");

                var appliedScripts = await ReadAppliedScriptsAsync(connection, transaction, commandTimeout, token);
                var pendingScripts = SqlSchemaScripts.GetAll().Where(script => !appliedScripts.Contains(script.Name)).ToList();

                foreach (var (name, sql) in pendingScripts)
                {
                    if (this.logger.IsEnabled(LogLevel.Information))
                    {
                        this.logger.LogInformation("Applying Functional Data schema script {ScriptName}.", name);
                    }

                    await ApplyScriptAsync(connection, transaction, name, sql, commandTimeout, token);
                }

                this.LogStep("committing the initialization");

                await transaction.CommitAsync(token);

                if (this.logger.IsEnabled(LogLevel.Information))
                {
                    this.logger.LogInformation("Functional Data initialization completed. Applied scripts: {AppliedScriptCount}.", pendingScripts.Count);
                }
            }
            catch (Exception exception) when (token.IsCancellationRequested)
            {
                this.logger.LogWarning(exception, "Functional Data initialization was canceled or exceeded its deadline.");

                throw new OperationCanceledException("Functional Data initialization was canceled or exceeded its deadline.", exception, token);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Functional Data initialization failed.");

                throw new InvalidOperationException("Functional Data initialization failed.", exception);
            }
        }

        /// <summary>
        /// Runs one script and records it as applied, in the initialization transaction.
        /// </summary>
        /// <param name="connection">The open connection.</param>
        /// <param name="transaction">The locked initialization transaction.</param>
        /// <param name="name">The script name.</param>
        /// <param name="sql">The unchanged script text.</param>
        /// <param name="commandTimeout">The command timeout in seconds, bounded by the initialization deadline.</param>
        /// <param name="cancellationToken">The token canceling the script.</param>
        /// <returns>A task that completes once the script is applied and recorded.</returns>
        /// <exception cref="NpgsqlException">The script or its history row fails.</exception>
        /// <exception cref="OperationCanceledException">The script is canceled.</exception>
        private static async Task ApplyScriptAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string name, string sql, int commandTimeout,
            CancellationToken cancellationToken)
        {
            await using (var script = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = commandTimeout })
            {
                await script.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var record = new NpgsqlCommand(RecordScriptSql, connection, transaction) { CommandTimeout = commandTimeout };

            record.Parameters.Add(new NpgsqlParameter { Value = name });

            await record.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Reads the names of the scripts already recorded as applied, after the initialization lock has been acquired.
        /// </summary>
        /// <param name="connection">The open connection.</param>
        /// <param name="transaction">The locked initialization transaction.</param>
        /// <param name="commandTimeout">The command timeout in seconds, bounded by the initialization deadline.</param>
        /// <param name="cancellationToken">The token canceling the query.</param>
        /// <returns>The applied script names.</returns>
        /// <exception cref="NpgsqlException">The history query fails.</exception>
        /// <exception cref="OperationCanceledException">The query is canceled.</exception>
        private static async Task<HashSet<string>> ReadAppliedScriptsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int commandTimeout,
            CancellationToken cancellationToken)
        {
            await using var command = new NpgsqlCommand(ReadHistorySql, connection, transaction) { CommandTimeout = commandTimeout };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var appliedScripts = new HashSet<string>(StringComparer.Ordinal);

            while (await reader.ReadAsync(cancellationToken))
            {
                appliedScripts.Add(reader.GetString(0));
            }

            return appliedScripts;
        }

        /// <summary>
        /// Logs the initialization step that is starting, so a failure can be located from the log.
        /// </summary>
        /// <param name="step">The step description.</param>
        private void LogStep(string step)
        {
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                this.logger.LogDebug("Functional Data initialization: {Step}.", step);
            }
        }

        /// <summary>
        /// Reads the positive initialization deadline, defaulting to sixty seconds.
        /// </summary>
        /// <returns>The configured initialization duration.</returns>
        /// <exception cref="InvalidOperationException">The configured duration is not a supported positive number of seconds.</exception>
        private TimeSpan ReadInitializationTimeout()
        {
            var value = this.configuration["FunctionalData:InitializationTimeoutSeconds"];

            if (value == null)
            {
                return TimeSpan.FromSeconds(60);
            }

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0 || seconds > (uint.MaxValue - 1) / 1000)
            {
                throw new InvalidOperationException("FunctionalData:InitializationTimeoutSeconds must be a supported positive number of seconds.");
            }

            return TimeSpan.FromSeconds(seconds);
        }
    }
}
