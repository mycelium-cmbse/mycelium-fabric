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
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using DbUp;
    using DbUp.Engine;
    using DbUp.Engine.Output;
    using DbUp.Engine.Transactions;
    using DbUp.Postgresql;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Npgsql;

    /// <summary>
    /// Initializes an uninitialized Functional Data schema and preserves compatible installed databases.
    /// </summary>
    public class FunctionalDataInitializer
    {
        /// <summary>
        /// The embedded script name used as the stable DbUp journal identity.
        /// </summary>
        private const string SchemaResourceName = "Mycelium.Fabric.FunctionalDataServer.Persistence.Sql.schema.sql";

        /// <summary>
        /// The PostgreSQL advisory-lock key shared by Functional Data initializers.
        /// </summary>
        private const long InitializationLockKey = 33420001;

        /// <summary>
        /// The configuration supplying the connection and initialization deadline.
        /// </summary>
        private readonly IConfiguration configuration;

        /// <summary>
        /// The logger receiving secret-free initialization diagnostics.
        /// </summary>
        private readonly ILogger<FunctionalDataInitializer> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionalDataInitializer"/> class.
        /// </summary>
        /// <param name="configuration">The service configuration.</param>
        /// <param name="logger">The initialization logger.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public FunctionalDataInitializer(IConfiguration configuration, ILogger<FunctionalDataInitializer> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);

            this.configuration = configuration;
            this.logger = logger;
        }

        /// <summary>
        /// Initializes the database atomically, or verifies that the installed model versions match the embedded artifact.
        /// </summary>
        /// <param name="cancellationToken">The token canceling initialization.</param>
        /// <returns>A task that completes after the initialization transaction commits.</returns>
        /// <exception cref="InvalidOperationException">Configuration, artifact loading, database state or initialization is invalid.</exception>
        /// <exception cref="OperationCanceledException">Initialization is canceled or exceeds its configured deadline.</exception>
        public virtual async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var timeout = this.ReadInitializationTimeout();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            deadline.CancelAfter(timeout);

            var elapsed = Stopwatch.StartNew();
            var token = deadline.Token;
            var stage = "reading connection configuration";

            try
            {
                token.ThrowIfCancellationRequested();

                var connectionString = this.configuration.GetConnectionString("functionaldata");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException();
                }

                stage = "loading the embedded schema and model versions";

                var script = await ReadSchemaAsync(token).ConfigureAwait(false);
                var fabricVersion = ReadModelVersion(script, "Fabric");
                var primitivesVersion = ReadModelVersion(script, "CommonPrimitives");

                stage = "opening the Functional Data database";

                await using var connection = new NpgsqlConnection(connectionString);

                await connection.OpenAsync(token).ConfigureAwait(false);

                await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);

                var manager = new InitializationConnectionManager(connection, transaction, timeout, elapsed, token);

                stage = "acquiring the initialization lock";

                await using (var command = manager.CreateCommand("SELECT pg_advisory_xact_lock(@key)"))
                {
                    command.Parameters.AddWithValue("key", InitializationLockKey);

                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                stage = "checking initialization state";

                var schemaExists = await SchemaExistsAsync(manager, token).ConfigureAwait(false);
                var upgrader = PostgresqlExtensions.PostgresqlDatabase(manager, "public")
                    .WithScript(SchemaResourceName, script)
                    .WithVariablesDisabled()
                    .LogToNowhere()
                    .Build();

                var executedScripts = await Task.Run(upgrader.GetExecutedScripts, token).ConfigureAwait(false);

                if (schemaExists)
                {
                    if (executedScripts.Count != 1 || !string.Equals(executedScripts[0], SchemaResourceName, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException();
                    }

                    stage = "checking installed model versions";

                    await VerifyVersionsAsync(manager, fabricVersion, primitivesVersion, token).ConfigureAwait(false);
                }
                else
                {
                    if (executedScripts.Count != 0)
                    {
                        throw new InvalidOperationException();
                    }

                    stage = "executing the initial schema";

                    var result = await Task.Run(upgrader.PerformUpgrade, token).ConfigureAwait(false);

                    token.ThrowIfCancellationRequested();

                    if (!result.Successful)
                    {
                        throw result.Error ?? new InvalidOperationException();
                    }

                    stage = "recording installed model versions";

                    await StoreVersionsAsync(manager, fabricVersion, primitivesVersion, token).ConfigureAwait(false);
                }

                stage = "committing initialization";

                token.ThrowIfCancellationRequested();

                await transaction.CommitAsync(token).ConfigureAwait(false);

                this.logger.LogInformation("Functional Data initialization completed. Created schema: {CreatedSchema}.", !schemaExists);
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                this.logger.LogWarning("Functional Data initialization was canceled or exceeded its deadline during {Stage}.", stage);

                throw new OperationCanceledException("Functional Data initialization was canceled or exceeded its deadline.", token);
            }
            catch (Exception exception)
            {
                var errorCode = exception is PostgresException postgresException ? postgresException.SqlState : exception.GetType().Name;

                this.logger.LogError("Functional Data initialization failed during {Stage}. Error code: {ErrorCode}.", stage, errorCode);

                throw new InvalidOperationException($"Functional Data initialization failed during {stage}. Error code: {errorCode}.");
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

        /// <summary>
        /// Reads the generator-owned schema from this service's assembly.
        /// </summary>
        /// <param name="cancellationToken">The token canceling resource loading.</param>
        /// <returns>The complete, unmodified embedded SQL text.</returns>
        /// <exception cref="InvalidOperationException">The embedded schema is missing.</exception>
        /// <exception cref="IOException">The resource cannot be read.</exception>
        /// <exception cref="OperationCanceledException">Loading is canceled.</exception>
        private static async Task<string> ReadSchemaAsync(CancellationToken cancellationToken)
        {
            await using var stream = typeof(FunctionalDataInitializer).Assembly.GetManifestResourceStream(SchemaResourceName)
                ?? throw new InvalidOperationException("The embedded Functional Data schema is missing.");

            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads one model-package version from the schema's deterministic header comments.
        /// </summary>
        /// <param name="script">The complete embedded schema.</param>
        /// <param name="modelName">The model-package suffix.</param>
        /// <returns>The version recorded by the producer.</returns>
        /// <exception cref="InvalidOperationException">A version comment is missing, duplicated or malformed.</exception>
        private static string ReadModelVersion(string script, string modelName)
        {
            var prefix = $"-- Mycelium.Model.{modelName} ";
            var versions = script.Split('\n').Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).Select(line => line[prefix.Length..]).ToArray();

            if (versions.Length != 1 || !Version.TryParse(versions[0], out _))
            {
                throw new InvalidOperationException("The embedded schema's model-version comments are invalid.");
            }

            return versions[0];
        }

        /// <summary>
        /// Checks for the application schema after the initialization lock has been acquired.
        /// </summary>
        /// <param name="manager">The locked transaction's command factory.</param>
        /// <param name="cancellationToken">The token canceling the query.</param>
        /// <returns>Whether the case-sensitive Fabric schema exists.</returns>
        /// <exception cref="NpgsqlException">The state query fails.</exception>
        /// <exception cref="OperationCanceledException">The query is canceled.</exception>
        private static async Task<bool> SchemaExistsAsync(InitializationConnectionManager manager, CancellationToken cancellationToken)
        {
            await using var command = manager.CreateCommand("SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname = 'Fabric')");

            return (bool)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Requires exactly one installed metadata record with matching model-package versions.
        /// </summary>
        /// <param name="manager">The locked transaction's command factory.</param>
        /// <param name="fabricVersion">The embedded Fabric model version.</param>
        /// <param name="primitivesVersion">The embedded CommonPrimitives model version.</param>
        /// <param name="cancellationToken">The token canceling the query.</param>
        /// <returns>A task that completes when installed versions match.</returns>
        /// <exception cref="InvalidOperationException">The installed versions are missing, duplicated or incompatible.</exception>
        /// <exception cref="NpgsqlException">The metadata query fails.</exception>
        /// <exception cref="OperationCanceledException">The query is canceled.</exception>
        private static async Task VerifyVersionsAsync(InitializationConnectionManager manager, string fabricVersion, string primitivesVersion, CancellationToken cancellationToken)
        {
            await using var command = manager.CreateCommand("SELECT fabric_version, common_primitives_version FROM \"Fabric\".\"__InitializationMetadata\"");

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                || !string.Equals(reader.GetString(0), fabricVersion, StringComparison.Ordinal)
                || !string.Equals(reader.GetString(1), primitivesVersion, StringComparison.Ordinal)
                || await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The installed Functional Data model versions are incompatible.");
            }
        }

        /// <summary>
        /// Records model-package versions in consumer-owned metadata in the schema-creation transaction.
        /// </summary>
        /// <param name="manager">The locked transaction's command factory.</param>
        /// <param name="fabricVersion">The embedded Fabric model version.</param>
        /// <param name="primitivesVersion">The embedded CommonPrimitives model version.</param>
        /// <param name="cancellationToken">The token canceling metadata creation.</param>
        /// <returns>A task that completes after inserting the single metadata row.</returns>
        /// <exception cref="NpgsqlException">Metadata creation fails.</exception>
        /// <exception cref="OperationCanceledException">Metadata creation is canceled.</exception>
        private static async Task StoreVersionsAsync(InitializationConnectionManager manager, string fabricVersion, string primitivesVersion, CancellationToken cancellationToken)
        {
            const string sql = """
                CREATE TABLE "Fabric"."__InitializationMetadata" (
                    singleton boolean NOT NULL DEFAULT TRUE PRIMARY KEY CHECK (singleton),
                    fabric_version text NOT NULL,
                    common_primitives_version text NOT NULL
                );
                INSERT INTO "Fabric"."__InitializationMetadata" (fabric_version, common_primitives_version)
                VALUES (@fabricVersion, @primitivesVersion);
                """;

            await using var command = manager.CreateCommand(sql);

            command.Parameters.AddWithValue("fabricVersion", fabricVersion);
            command.Parameters.AddWithValue("primitivesVersion", primitivesVersion);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Binds DbUp commands to the caller-owned connection, transaction and initialization deadline.
        /// </summary>
        private sealed class InitializationConnectionManager : IConnectionManager, IDisposable
        {
            /// <summary>
            /// The existing application connection.
            /// </summary>
            private readonly NpgsqlConnection connection;

            /// <summary>
            /// The caller-owned transaction holding the advisory lock.
            /// </summary>
            private readonly NpgsqlTransaction transaction;

            /// <summary>
            /// The maximum initialization duration.
            /// </summary>
            private readonly TimeSpan timeout;

            /// <summary>
            /// The clock measuring elapsed initialization time.
            /// </summary>
            private readonly Stopwatch elapsed;

            /// <summary>
            /// The combined shutdown and deadline cancellation token.
            /// </summary>
            private readonly CancellationToken cancellationToken;

            /// <summary>
            /// Initializes a new instance of the <see cref="InitializationConnectionManager"/> class.
            /// </summary>
            /// <param name="connection">The open application connection.</param>
            /// <param name="transaction">The active initialization transaction.</param>
            /// <param name="timeout">The total initialization duration.</param>
            /// <param name="elapsed">The elapsed-time clock.</param>
            /// <param name="cancellationToken">The combined cancellation token.</param>
            public InitializationConnectionManager(NpgsqlConnection connection, NpgsqlTransaction transaction, TimeSpan timeout, Stopwatch elapsed,
                CancellationToken cancellationToken)
            {
                this.connection = connection;
                this.transaction = transaction;
                this.timeout = timeout;
                this.elapsed = elapsed;
                this.cancellationToken = cancellationToken;
            }

            /// <summary>
            /// Gets or sets DbUp's transaction preference; the actual transaction is owned by the initializer.
            /// </summary>
            public TransactionMode TransactionMode { get; set; }

            /// <summary>
            /// Gets or sets whether DbUp requests script-result logging.
            /// </summary>
            public bool IsScriptOutputLogged { get; set; }

            /// <summary>
            /// Gets or sets an additional per-command timeout, bounded by the initialization deadline.
            /// </summary>
            public int? ExecutionTimeoutSeconds { get; set; }

            /// <summary>
            /// Starts a DbUp operation without opening a connection or creating another transaction.
            /// </summary>
            /// <param name="upgradeLog">The DbUp operation logger.</param>
            /// <param name="executedScripts">The scripts executed by this operation.</param>
            /// <returns>This non-owning operation scope.</returns>
            /// <exception cref="OperationCanceledException">Initialization has been canceled.</exception>
            public IDisposable OperationStarting(IUpgradeLog upgradeLog, List<SqlScript> executedScripts)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                return this;
            }

            /// <summary>
            /// Leaves connection and transaction lifetime management to the initializer.
            /// </summary>
            public void Dispose()
            {
            }

            /// <summary>
            /// Creates a command in the locked transaction with a bounded execution timeout.
            /// </summary>
            /// <param name="sql">The SQL command text.</param>
            /// <returns>A command that the caller must dispose.</returns>
            /// <exception cref="OperationCanceledException">The initialization deadline has expired.</exception>
            public NpgsqlCommand CreateCommand(string sql)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                var remaining = this.timeout - this.elapsed.Elapsed;

                if (remaining <= TimeSpan.Zero)
                {
                    throw new OperationCanceledException("The Functional Data initialization deadline expired.", this.cancellationToken);
                }

                var commandTimeout = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));

                if (this.ExecutionTimeoutSeconds is > 0)
                {
                    commandTimeout = Math.Min(commandTimeout, this.ExecutionTimeoutSeconds.Value);
                }

                return new NpgsqlCommand(sql, this.connection, this.transaction) { CommandTimeout = commandTimeout };
            }

            /// <summary>
            /// Executes DbUp commands on the existing transaction with cancellation enabled.
            /// </summary>
            /// <param name="action">The operation receiving a command factory.</param>
            /// <exception cref="OperationCanceledException">Initialization is canceled.</exception>
            public void ExecuteCommandsWithManagedConnection(Action<Func<IDbCommand>> action)
            {
                this.ExecuteCommandsWithManagedConnection(commandFactory =>
                {
                    action(commandFactory);

                    return true;
                });
            }

            /// <summary>
            /// Executes a DbUp query on the existing transaction with cancellation enabled.
            /// </summary>
            /// <typeparam name="T">The operation's result type.</typeparam>
            /// <param name="actionWithResult">The operation receiving a command factory.</param>
            /// <returns>The operation's result.</returns>
            /// <exception cref="OperationCanceledException">Initialization is canceled.</exception>
            public T ExecuteCommandsWithManagedConnection<T>(Func<Func<IDbCommand>, T> actionWithResult)
            {
                var registrations = new List<CancellationTokenRegistration>();

                try
                {
                    var result = actionWithResult(() =>
                    {
                        var command = this.CreateCommand(string.Empty);

                        registrations.Add(this.cancellationToken.Register(command.Cancel));

                        return command;
                    });

                    this.cancellationToken.ThrowIfCancellationRequested();

                    return result;
                }
                finally
                {
                    foreach (var registration in registrations)
                    {
                        registration.Dispose();
                    }
                }
            }

            /// <summary>
            /// Uses DbUp's PostgreSQL parser to preserve dollar-quoted function bodies when splitting statements.
            /// </summary>
            /// <param name="scriptContents">The complete generated SQL.</param>
            /// <returns>The provider-parsed SQL statements.</returns>
            public IEnumerable<string> SplitScriptIntoCommands(string scriptContents)
            {
                return new PostgresqlConnectionManager(string.Empty).SplitScriptIntoCommands(scriptContents);
            }

            /// <summary>
            /// Reports whether the caller-owned connection remains open without creating another connection.
            /// </summary>
            /// <param name="upgradeLog">The DbUp operation logger.</param>
            /// <param name="errorMessage">A secret-free diagnostic when the connection is closed.</param>
            /// <returns>Whether the existing connection is open.</returns>
            public bool TryConnect(IUpgradeLog upgradeLog, out string errorMessage)
            {
                var connected = this.connection.State == ConnectionState.Open;

                errorMessage = connected ? string.Empty : "The Functional Data connection is not open.";

                return connected;
            }
        }
    }
}
