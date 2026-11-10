// ------------------------------------------------------------------------------------------------
//  <copyright file="FunctionalDataInitializerIntegrationTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.FunctionalDataServer.Tests.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Mycelium.Fabric.FunctionalDataServer.Persistence;

    using Npgsql;

    using NUnit.Framework;

    using Testcontainers.PostgreSql;

    [TestFixture]
    [Category("Database")]
    [NonParallelizable]
    public partial class FunctionalDataInitializerIntegrationTestFixture
    {
        private readonly Dictionary<string, Guid> identities = new();

        private PostgreSqlContainer database;

        private Mock<ILogger<FunctionalDataInitializer>> logger;

        [SetUp]
        public async Task SetUp()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("MYCELIUM_FABRIC_RUN_DATABASE_TESTS"), "1", StringComparison.Ordinal))
            {
                Assert.Ignore("Set MYCELIUM_FABRIC_RUN_DATABASE_TESTS=1 to run isolated PostgreSQL checks.");
            }

            this.identities.Clear();
            this.logger = new Mock<ILogger<FunctionalDataInitializer>>();
            this.database = new PostgreSqlBuilder("postgres:18.6").Build();

            await this.database.StartAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (this.database != null)
            {
                await this.database.DisposeAsync();

                this.database = null;
            }
        }

        [Test]
        public async Task InitializeAsync_EmptyDatabase_PreservesDataOnRepeatStartup()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync(InsertObject("admin", "User", "model_user"));
            await new FunctionalDataInitializationService(this.CreateInitializer()).StartAsync(CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
                Assert.That(await this.ScalarAsync<string>("SELECT scriptname FROM public.schemaversions"), Is.EqualTo(FunctionalDataInitializer.InitialScriptName));
                Assert.That(await this.ScalarAsync<string>("SELECT data->>'@type' FROM fabric.thing WHERE id = @admin"), Is.EqualTo("User"));
                Assert.That(await this.ScalarAsync<string>("SELECT fabric_version FROM fabric.initialization_metadata"), Is.EqualTo("0.3.0"));
                Assert.That(await this.ScalarAsync<string>("SELECT common_primitives_version FROM fabric.initialization_metadata"), Is.EqualTo("0.4.0"));
            }
        }

        [Test]
        public async Task InitializeAsync_FormerScriptIdentity_RejectsWithoutChangingData()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync(InsertObject("admin", "User", "model_user"));
            await this.ExecuteAsync("UPDATE public.schemaversions SET scriptname = 'Mycelium.SDK.Sql.schema.sql';");

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None),
                Throws.TypeOf<InvalidOperationException>().With.InnerException.Message.Contains("without a matching initialization journal entry"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<string>("SELECT scriptname FROM public.schemaversions"), Is.EqualTo("Mycelium.SDK.Sql.schema.sql"));
                Assert.That(await this.ScalarAsync<string>("SELECT data->>'@type' FROM fabric.thing WHERE id = @admin"), Is.EqualTo("User"));
            }
        }

        [Test]
        public async Task InitializeAsync_SimultaneousStarts_CommitOneInstallation()
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(33420001::bigint)", connection, transaction);

            await command.ExecuteNonQueryAsync();

            var first = this.CreateInitializer().InitializeAsync(CancellationToken.None);
            var second = this.CreateInitializer().InitializeAsync(CancellationToken.None);

            await this.WaitForLockWaitersAsync(2);
            await transaction.CommitAsync();
            await Task.WhenAll(first, second);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.initialization_metadata"), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task InitializeAsync_UnrecordedSchema_RejectsWithoutChangingData()
        {
            await this.ExecuteAsync("CREATE SCHEMA fabric; CREATE TABLE fabric.sentinel (value integer); INSERT INTO fabric.sentinel VALUES (7);");

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<int>("SELECT value FROM fabric.sentinel"), Is.EqualTo(7));
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regclass('public.schemaversions') IS NULL"), Is.True);
            }
        }

        [Test]
        public async Task InitializeAsync_IncompatibleVersions_RejectsWithoutOverwritingMetadata()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync("UPDATE fabric.initialization_metadata SET fabric_version = '99.0.0';");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());
                Assert.That(await this.ScalarAsync<string>("SELECT fabric_version FROM fabric.initialization_metadata"), Is.EqualTo("99.0.0"));
            }
        }

        [Test]
        public async Task InitializeAsync_MissingMetadata_RejectsExistingSchema()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync("DELETE FROM fabric.initialization_metadata;");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.initialization_metadata"), Is.Zero);
            }
        }

        [Test]
        public async Task InitializeAsync_JournalWithoutSchema_RejectsContradictoryState()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync("DROP SCHEMA fabric CASCADE;");

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regnamespace('fabric') IS NULL"), Is.True);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task InitializeAsync_LateSqlFailure_RollsBackSchemaJournalAndMetadata()
        {
            const string failureInjection = """
                CREATE FUNCTION public.reject_initialization_metadata() RETURNS event_trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF to_regclass('fabric.initialization_metadata') IS NOT NULL THEN
                        RAISE EXCEPTION 'Injected initialization failure';
                    END IF;
                END;
                $$;
                CREATE EVENT TRIGGER reject_initialization_metadata ON ddl_command_end
                WHEN TAG IN ('CREATE TABLE') EXECUTE FUNCTION public.reject_initialization_metadata();
                """;

            await this.ExecuteAsync(failureInjection);

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regnamespace('fabric') IS NULL"), Is.True);
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regclass('public.schemaversions') IS NULL"), Is.True);
            }

            await this.ExecuteAsync("DROP EVENT TRIGGER reject_initialization_metadata;");
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
        }

        [Test]
        public async Task InitializeAsync_LockDeadlineAndCancellation_AllowSubsequentInitialization()
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(33420001::bigint)", connection, transaction);

            await command.ExecuteNonQueryAsync();

            Assert.That(() => this.CreateInitializer("1").InitializeAsync(CancellationToken.None), Throws.TypeOf<OperationCanceledException>());

            using var cancellation = new CancellationTokenSource();

            var initialization = this.CreateInitializer().InitializeAsync(cancellation.Token);

            await this.WaitForLockWaitersAsync(1);
            await cancellation.CancelAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(async () => await initialization, Throws.TypeOf<OperationCanceledException>());
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regnamespace('fabric') IS NULL"), Is.True);
            }

            await transaction.CommitAsync();
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
        }

        [GeneratedRegex(@"(?<!')@(\w+)")]
        private static partial Regex ParameterNameRegex();

        private static string InsertObject(string identity, string classKind, string classTable)
        {
            return $"""
                INSERT INTO fabric.thing (id, class_kind, data) VALUES (@{identity}, '{classKind}', jsonb_build_object('@id', @{identity}, '@type', '{classKind}'));
                INSERT INTO fabric.auditable_thing (id, created_by, updated_by) VALUES (@{identity}, @admin, @admin);
                INSERT INTO fabric.{classTable} (id) VALUES (@{identity});

                """;
        }

        private FunctionalDataInitializer CreateInitializer(string timeout = "60")
        {
            var section = new Mock<IConfigurationSection>();

            section.Setup(connection => connection["functionaldata"]).Returns(this.database.GetConnectionString());

            var configuration = new Mock<IConfiguration>();

            configuration.Setup(config => config.GetSection("ConnectionStrings")).Returns(section.Object);
            configuration.Setup(config => config["FunctionalData:InitializationTimeoutSeconds"]).Returns(timeout);

            return new FunctionalDataInitializer(configuration.Object, this.logger.Object);
        }

        private async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            await using (var command = this.CreateCommand(connection, $"SET CONSTRAINTS ALL DEFERRED;\n{sql}"))
            {
                command.Transaction = transaction;

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        private async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var command = this.CreateCommand(connection, sql);

            return (T)await command.ExecuteScalarAsync();
        }

        private async Task WaitForLockWaitersAsync(int expected)
        {
            var elapsed = Stopwatch.StartNew();

            while (elapsed.Elapsed < TimeSpan.FromSeconds(10))
            {
                const string query = "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND objid = 33420001 AND NOT granted";

                if (await this.ScalarAsync<long>(query) >= expected)
                {
                    return;
                }

                await Task.Delay(25);
            }

            Assert.Fail("Initializers did not reach the expected advisory-lock wait.");
        }

        private NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql)
        {
            var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 15 };

            foreach (var name in ParameterNameRegex().Matches(sql).Select(match => match.Groups[1].Value).Distinct())
            {
                command.Parameters.AddWithValue(name, this.Identity(name));
            }

            return command;
        }

        private Guid Identity(string name)
        {
            if (!this.identities.TryGetValue(name, out var identity))
            {
                identity = Guid.NewGuid();
                this.identities.Add(name, identity);
            }

            return identity;
        }
    }
}
