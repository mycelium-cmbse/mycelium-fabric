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
    using System.IO;
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
    public class FunctionalDataInitializerIntegrationTestFixture
    {
        private PostgreSqlContainer database;

        private Mock<ILogger<FunctionalDataInitializer>> logger;

        [SetUp]
        public async Task SetUp()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("MYCELIUM_FABRIC_RUN_DATABASE_TESTS"), "1", StringComparison.Ordinal))
            {
                Assert.Ignore("Set MYCELIUM_FABRIC_RUN_DATABASE_TESTS=1 to run isolated PostgreSQL checks.");
            }

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

            var identity = Guid.NewGuid();

            await this.ExecuteAsync("INSERT INTO \"Fabric\".\"Thing\" (id, \"classKind\", data) VALUES (@id, 'test', '{\"sentinel\":\"retained\"}'::jsonb)", identity);
            await new FunctionalDataInitializationService(this.CreateInitializer()).StartAsync(CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
                Assert.That(await this.ScalarAsync<string>("SELECT data->>'sentinel' FROM \"Fabric\".\"Thing\" WHERE id = @id", identity), Is.EqualTo("retained"));
                Assert.That(await this.ScalarAsync<string>("SELECT fabric_version FROM \"Fabric\".\"__InitializationMetadata\""), Is.EqualTo("0.3.0"));
                Assert.That(await this.ScalarAsync<string>("SELECT common_primitives_version FROM \"Fabric\".\"__InitializationMetadata\""), Is.EqualTo("0.4.0"));
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
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"__InitializationMetadata\""), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task InitializeAsync_UnrecordedSchema_RejectsWithoutChangingData()
        {
            await this.ExecuteAsync("CREATE SCHEMA \"Fabric\"; CREATE TABLE \"Fabric\".sentinel (value integer); INSERT INTO \"Fabric\".sentinel VALUES (7)");

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<int>("SELECT value FROM \"Fabric\".sentinel"), Is.EqualTo(7));
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regclass('public.schemaversions') IS NULL"), Is.True);
            }
        }

        [Test]
        public async Task InitializeAsync_IncompatibleVersions_RejectsWithoutOverwritingMetadata()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync("UPDATE \"Fabric\".\"__InitializationMetadata\" SET fabric_version = '99.0.0'");

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());
            Assert.That(await this.ScalarAsync<string>("SELECT fabric_version FROM \"Fabric\".\"__InitializationMetadata\""), Is.EqualTo("99.0.0"));
        }

        [Test]
        public async Task InitializeAsync_MissingMetadata_RejectsExistingSchema()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync("DELETE FROM \"Fabric\".\"__InitializationMetadata\"");

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());
            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"__InitializationMetadata\""), Is.Zero);
        }

        [Test]
        public async Task InitializeAsync_JournalWithoutSchema_RejectsContradictoryState()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync("DROP SCHEMA \"Fabric\" CASCADE");

            Assert.That(() => this.CreateInitializer().InitializeAsync(CancellationToken.None), Throws.TypeOf<InvalidOperationException>());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regnamespace('\"Fabric\"') IS NULL"), Is.True);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task InitializeAsync_LateSqlFailure_RollsBackSchemaJournalAndMetadata()
        {
            const string failureInjection = """
                CREATE FUNCTION public.reject_initialization_metadata() RETURNS event_trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF to_regclass('"Fabric"."__InitializationMetadata"') IS NOT NULL THEN
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
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regnamespace('\"Fabric\"') IS NULL"), Is.True);
                Assert.That(await this.ScalarAsync<bool>("SELECT to_regclass('public.schemaversions') IS NULL"), Is.True);
            }

            await this.ExecuteAsync("DROP EVENT TRIGGER reject_initialization_metadata");
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

            Assert.That(async () => await initialization, Throws.TypeOf<OperationCanceledException>());
            Assert.That(await this.ScalarAsync<bool>("SELECT to_regnamespace('\"Fabric\"') IS NULL"), Is.True);

            await transaction.CommitAsync();
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM public.schemaversions"), Is.EqualTo(1));
        }

        [Test]
        public async Task InitializeAsync_InstalledSchema_PreservesExplicitNamesAndAutovacuumSettings()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            using var stream = typeof(FunctionalDataInitializer).Assembly.GetManifestResourceStream("Mycelium.Fabric.FunctionalDataServer.Persistence.Sql.schema.sql");
            using var reader = new StreamReader(stream);

            var sql = await reader.ReadToEndAsync();
            var expectedConstraints = Regex.Matches(sql, "CONSTRAINT \"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
            var expectedIndexes = Regex.Matches(sql, "CREATE INDEX \"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
            var constraints = await this.ReadNamesAsync("SELECT conname FROM pg_constraint WHERE connamespace = '\"Fabric\"'::regnamespace");
            var indexes = await this.ReadNamesAsync("SELECT indexname FROM pg_indexes WHERE schemaname = 'Fabric'");

            const string maintenanceQuery = """
                SELECT NOT EXISTS (
                    SELECT c.oid FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    LEFT JOIN LATERAL pg_options_to_table(c.reloptions) o ON TRUE
                    WHERE n.nspname = 'Fabric' AND c.relkind = 'r' AND c.relname <> '__InitializationMetadata'
                    GROUP BY c.oid
                    HAVING count(*) FILTER (
                        WHERE (o.option_name IN ('autovacuum_vacuum_scale_factor', 'autovacuum_analyze_scale_factor') AND o.option_value::numeric = 0)
                           OR (o.option_name IN ('autovacuum_vacuum_threshold', 'autovacuum_analyze_threshold') AND o.option_value::numeric = 2500)
                    ) <> 4
                )
                """;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(expectedConstraints, Is.Not.Empty);
                Assert.That(expectedIndexes, Is.Not.Empty);
                Assert.That(constraints, Is.SupersetOf(expectedConstraints));
                Assert.That(indexes, Is.SupersetOf(expectedIndexes));
                Assert.That(await this.ScalarAsync<bool>(maintenanceQuery), Is.True);
            }
        }

        [Test]
        public async Task InitializeAsync_CompositionDeletion_RemovesOwnedObjectsWithoutDeletingOwnerFromChild()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            var policy = Guid.NewGuid();
            var organization = Guid.NewGuid();

            await this.InsertThingAsync(policy, "OrganizationPolicy");
            await this.InsertThingAsync(organization, "Organization");
            await this.ExecuteAsync("INSERT INTO \"Fabric\".\"OrganizationPolicy\" (id) VALUES (@id)", policy);
            await this.ExecuteAsync("INSERT INTO \"Fabric\".\"Organization\" (id, policy) VALUES (@id, @target)", organization, policy);

            Assert.That(() => this.ExecuteAsync("DELETE FROM \"Fabric\".\"OrganizationPolicy\" WHERE id = @id", policy),
                Throws.TypeOf<PostgresException>().With.Property(nameof(PostgresException.SqlState)).EqualTo("23503"));
            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"Organization\" WHERE id = @id", organization), Is.EqualTo(1));

            await this.ExecuteAsync("DELETE FROM \"Fabric\".\"Organization\" WHERE id = @id", organization);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"Thing\" WHERE id IN (@id, @target)", organization, policy), Is.Zero);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"OrganizationPolicy\" WHERE id = @id", policy), Is.Zero);
            }
        }

        [Test]
        public async Task InitializeAsync_OrdinaryReference_RestrictsTargetDeletion()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            var target = Guid.NewGuid();
            var source = Guid.NewGuid();

            await this.InsertThingAsync(target, "Message");
            await this.InsertThingAsync(source, "Message");
            await this.ExecuteAsync("INSERT INTO \"Fabric\".\"Message\" (id) VALUES (@id)", target);
            await this.ExecuteAsync("INSERT INTO \"Fabric\".\"Message\" (id, quotes) VALUES (@id, @target)", source, target);

            Assert.That(() => this.ExecuteAsync("DELETE FROM \"Fabric\".\"Message\" WHERE id = @id", target),
                Throws.TypeOf<PostgresException>().With.Property(nameof(PostgresException.SqlState)).EqualTo("23503"));
            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"Message\""), Is.EqualTo(2));

            await this.ExecuteAsync("DELETE FROM \"Fabric\".\"Message\" WHERE id = @id", source);
            await this.ExecuteAsync("DELETE FROM \"Fabric\".\"Message\" WHERE id = @id", target);

            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"Thing\""), Is.Zero);
        }

        [Test]
        public async Task InitializeAsync_LinkDeletion_CleansRowsFromEitherEndpoint()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            var user = Guid.NewGuid();
            var firstRule = Guid.NewGuid();
            var secondRule = Guid.NewGuid();

            await this.InsertThingAsync(user, "User");
            await this.InsertThingAsync(firstRule, "BranchProtectionRule");
            await this.InsertThingAsync(secondRule, "BranchProtectionRule");
            await this.ExecuteAsync("INSERT INTO \"Fabric\".\"User\" (id) VALUES (@id)", user);
            await this.ExecuteAsync("INSERT INTO \"Fabric\".\"BranchProtectionRule\" (id) VALUES (@id), (@target)", firstRule, secondRule);

            const string insertLink = "INSERT INTO \"Fabric\".\"BranchProtectionRule_defaultReviewers__User\" VALUES (@id, @target)";

            await this.ExecuteAsync(insertLink, firstRule, user);
            await this.ExecuteAsync(insertLink, secondRule, user);
            await this.ExecuteAsync("DELETE FROM \"Fabric\".\"BranchProtectionRule\" WHERE id = @id", firstRule);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"BranchProtectionRule_defaultReviewers__User\""), Is.EqualTo(1));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"User\" WHERE id = @id", user), Is.EqualTo(1));
            }

            await this.ExecuteAsync("DELETE FROM \"Fabric\".\"User\" WHERE id = @id", user);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"BranchProtectionRule_defaultReviewers__User\""), Is.Zero);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM \"Fabric\".\"BranchProtectionRule\" WHERE id = @id", secondRule), Is.EqualTo(1));
            }
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

        private async Task ExecuteAsync(string sql, Guid? identity = null, Guid? target = null)
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var command = CreateCommand(connection, sql, identity, target);

            await command.ExecuteNonQueryAsync();
        }

        private async Task<T> ScalarAsync<T>(string sql, Guid? identity = null, Guid? target = null)
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var command = CreateCommand(connection, sql, identity, target);

            return (T)await command.ExecuteScalarAsync();
        }

        private async Task<List<string>> ReadNamesAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var command = CreateCommand(connection, sql);
            await using var reader = await command.ExecuteReaderAsync();

            var names = new List<string>();

            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            return names;
        }

        private async Task InsertThingAsync(Guid identity, string classKind)
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var command = CreateCommand(connection, "INSERT INTO \"Fabric\".\"Thing\" (id, \"classKind\", data) VALUES (@id, @kind, '{}'::jsonb)", identity);

            command.Parameters.AddWithValue("kind", classKind);

            await command.ExecuteNonQueryAsync();
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

        private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, string sql, Guid? identity = null, Guid? target = null)
        {
            var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 15 };

            if (identity.HasValue)
            {
                command.Parameters.AddWithValue("id", identity.Value);
            }

            if (target.HasValue)
            {
                command.Parameters.AddWithValue("target", target.Value);
            }

            return command;
        }
    }
}
