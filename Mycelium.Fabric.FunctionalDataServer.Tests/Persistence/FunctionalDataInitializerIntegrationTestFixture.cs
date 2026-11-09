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
    using Mycelium.SDK.DTO;

    using Npgsql;

    using NUnit.Framework;
    using NUnit.Framework.Constraints;

    using Testcontainers.PostgreSql;

    [TestFixture]
    [Category("Database")]
    [NonParallelizable]
    public partial class FunctionalDataInitializerIntegrationTestFixture
    {
        private const string SchemaResourceName = "Mycelium.SDK.Sql.schema.sql";

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
                Assert.That(await this.ScalarAsync<string>("SELECT scriptname FROM public.schemaversions"), Is.EqualTo(SchemaResourceName));
                Assert.That(await this.ScalarAsync<string>("SELECT data->>'@type' FROM fabric.thing WHERE id = @admin"), Is.EqualTo("User"));
                Assert.That(await this.ScalarAsync<string>("SELECT fabric_version FROM fabric.initialization_metadata"), Is.EqualTo("0.3.0"));
                Assert.That(await this.ScalarAsync<string>("SELECT common_primitives_version FROM fabric.initialization_metadata"), Is.EqualTo("0.4.0"));
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

        [Test]
        public async Task InitializeAsync_InstalledSchema_PreservesGeneratedNamesAndAutovacuumSettings()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            await using var stream = typeof(User).Assembly.GetManifestResourceStream(SchemaResourceName);

            using var reader = new StreamReader(stream);

            var sql = await reader.ReadToEndAsync();
            var expectedConstraints = ConstraintNameRegex().Matches(sql).Select(match => match.Groups[1].Value).ToList();
            var expectedIndexes = IndexNameRegex().Matches(sql).Concat(KeyConstraintNameRegex().Matches(sql)).Select(match => match.Groups[1].Value).ToList();
            var expectedTriggers = TriggerNameRegex().Matches(sql).Select(match => $"{match.Groups[2].Value}.{match.Groups[1].Value}").ToList();

            const string constraintQuery = """
                SELECT con.conname FROM pg_constraint con
                JOIN pg_class rel ON rel.oid = con.conrelid
                WHERE con.connamespace = 'fabric'::regnamespace AND con.contype IN ('p', 'f', 'u', 'c') AND rel.relname <> 'initialization_metadata'
                """;

            const string triggerQuery = """
                SELECT rel.relname || '.' || trg.tgname FROM pg_trigger trg
                JOIN pg_class rel ON rel.oid = trg.tgrelid
                WHERE rel.relnamespace = 'fabric'::regnamespace AND NOT trg.tgisinternal
                """;

            const string maintenanceQuery = """
                SELECT NOT EXISTS (
                    SELECT c.oid FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    LEFT JOIN LATERAL pg_options_to_table(c.reloptions) o ON TRUE
                    WHERE n.nspname = 'fabric' AND c.relkind = 'r' AND c.relname <> 'initialization_metadata'
                    GROUP BY c.oid
                    HAVING count(*) FILTER (
                        WHERE (o.option_name IN ('autovacuum_vacuum_scale_factor', 'autovacuum_analyze_scale_factor') AND o.option_value::numeric = 0)
                           OR (o.option_name IN ('autovacuum_vacuum_threshold', 'autovacuum_analyze_threshold') AND o.option_value::numeric = 2500)
                    ) <> 4
                )
                """;

            var constraints = await this.ReadNamesAsync(constraintQuery);
            var indexes = await this.ReadNamesAsync("SELECT indexname FROM pg_indexes WHERE schemaname = 'fabric' AND tablename <> 'initialization_metadata'");
            var triggers = await this.ReadNamesAsync(triggerQuery);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(expectedConstraints, Does.Contain("functional_project_branch_rules_target_id_fkey").And.Contain("thing_class_kind_check"));
                Assert.That(expectedTriggers, Does.Contain("auditable_thing.t20_delete_root").And.Contain("organization.t10_track_ownership_policy"));
                Assert.That(constraints, Is.EquivalentTo(expectedConstraints));
                Assert.That(indexes, Is.EquivalentTo(expectedIndexes));
                Assert.That(triggers, Is.EquivalentTo(expectedTriggers));
                Assert.That(triggers.Select(trigger => trigger[(trigger.IndexOf('.') + 1)..]), Has.All.Match("^t(10|20|30|80|90)_[a-z_]+$"));
                Assert.That(await this.ScalarAsync<bool>(maintenanceQuery), Is.True);
            }
        }

        [Test]
        public async Task InitializeAsync_RootRow_ConstrainsClassKindAndPayload()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync(InsertObject("admin", "User", "model_user"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<string>("SELECT class_kind FROM fabric.thing WHERE id = @admin"), Is.EqualTo("User"));

                Assert.That(() => this.ExecuteAsync(InsertRoot("unknown", "Unknown", "jsonb_build_object('@id', @unknown, '@type', 'Unknown')")),
                    ViolatesConstraint("thing_class_kind_check"));

                Assert.That(() => this.ExecuteAsync(InsertRoot("mismatch", "User", "jsonb_build_object('@id', @other, '@type', 'User')")), ViolatesConstraint("thing_data_check"));

                Assert.That(() => this.ExecuteAsync(InsertRoot("mistyped", "User", "jsonb_build_object('@id', @mistyped, '@type', 'Review')")),
                    ViolatesConstraint("thing_data_check"));

                Assert.That(() => this.ExecuteAsync(InsertRoot("untyped", "User", "jsonb_build_object('@id', @untyped)")), ViolatesConstraint("thing_data_check"));
                Assert.That(() => this.ExecuteAsync(InsertRoot("empty", "User", "NULL")), FailsWithState("23502"));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing"), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task InitializeAsync_InheritanceRows_MatchConcreteClassKind()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync(InsertObject("admin", "User", "model_user"));
            await this.ExecuteAsync(InsertObject("complete", "User", "model_user"));

            const string missingClassRow = """
                INSERT INTO fabric.thing (id, class_kind, data) VALUES (@incomplete, 'User', jsonb_build_object('@id', @incomplete, '@type', 'User'));
                INSERT INTO fabric.auditable_thing (id, created_by, updated_by) VALUES (@incomplete, @admin, @admin);
                """;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.model_user WHERE id = @complete"), Is.EqualTo(1));
                Assert.That(() => this.ExecuteAsync(missingClassRow), RaisesMessage("Inheritance rows do not match type User"));

                Assert.That(() => this.ExecuteAsync(InsertObject("overextended", "User", "model_user") + "INSERT INTO fabric.organization_policy (id) VALUES (@overextended);"),
                    RaisesMessage("Inheritance rows do not match type User"));

                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id IN (@incomplete, @overextended)"), Is.Zero);
            }
        }

        [Test]
        public async Task InitializeAsync_RootOrClassRowDeletion_RemovesWholeObject()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync(InsertObject("admin", "User", "model_user"));
            await this.ExecuteAsync(InsertObject("root", "User", "model_user") + InsertObject("intermediate", "User", "model_user") + InsertObject("leaf", "User", "model_user"));

            await this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @root;");
            await this.ExecuteAsync("DELETE FROM fabric.auditable_thing WHERE id = @intermediate;");
            await this.ExecuteAsync("DELETE FROM fabric.model_user WHERE id = @leaf;");

            const string remainingRows = """
                SELECT (SELECT count(*) FROM fabric.thing WHERE id IN (@root, @intermediate, @leaf))
                     + (SELECT count(*) FROM fabric.auditable_thing WHERE id IN (@root, @intermediate, @leaf))
                     + (SELECT count(*) FROM fabric.model_user WHERE id IN (@root, @intermediate, @leaf))
                """;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>(remainingRows), Is.Zero);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.model_user WHERE id = @admin"), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task InitializeAsync_CompositionDeletion_FollowsOwnershipDirection()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync(InsertProjectGraph());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @policy;"), ViolatesConstraint("organization_policy_fkey"));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.organization_policy WHERE id = @policy"), Is.EqualTo(1));
            }

            await this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @project;");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id IN (@project, @projectPolicy)"), Is.Zero);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id IN (@organization, @policy)"), Is.EqualTo(2));
            }

            await this.ExecuteAsync(InsertProject("successor", "organization", "successorPolicy"));
            await this.ExecuteAsync("DELETE FROM fabric.organization WHERE id = @organization;");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id IN (@organization, @policy, @successor, @successorPolicy)"), Is.Zero);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing"), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task InitializeAsync_CompositeOwnership_RejectsSecondOwnerAndRemovesOrphans()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);
            await this.ExecuteAsync(InsertObject("admin", "User", "model_user") + InsertOrganization("organization", "policy"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.ExecuteAsync(InsertObject("intruder", "Organization", "organization", ("policy", "policy"))), FailsWithState("23505"));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id = @intruder"), Is.Zero);
            }

            const string detaching = "UPDATE fabric.organization SET policy = @replacement WHERE id = @organization;";

            await this.ExecuteAsync(InsertObject("replacement", "OrganizationPolicy", "organization_policy") + detaching);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id = @policy"), Is.Zero);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.organization WHERE id = @organization AND policy = @replacement"), Is.EqualTo(1));
            }

            const string reparenting = "UPDATE fabric.organization SET policy = @successor WHERE id = @organization;";

            var adopter = InsertObject("adopter", "Organization", "organization", ("policy", "replacement"));

            await this.ExecuteAsync(InsertObject("successor", "OrganizationPolicy", "organization_policy") + reparenting + adopter);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.organization WHERE id = @adopter AND policy = @replacement"), Is.EqualTo(1));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id IN (@replacement, @successor)"), Is.EqualTo(2));
            }
        }

        [Test]
        public async Task InitializeAsync_OrdinaryReference_RestrictsTargetDeletion()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            var member = InsertObject("member", "OrganizationMember", "organization_member", ("organization", "organization"), ("model_user", "admin"));

            await this.ExecuteAsync(InsertObject("admin", "User", "model_user") + InsertOrganization("organization", "policy") + member);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(() => this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @organization;"), ViolatesConstraint("organization_member_organization_fkey"));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.organization WHERE id = @organization"), Is.EqualTo(1));
            }

            await this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @member;");
            await this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @organization;");

            Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.thing WHERE id IN (@organization, @policy, @member)"), Is.Zero);
        }

        [Test]
        public async Task InitializeAsync_LinkDeletion_CleansRowsFromEitherEndpoint()
        {
            await this.CreateInitializer().InitializeAsync(CancellationToken.None);

            const string reviewers = "INSERT INTO fabric.review_reviewers (source_id, target_id) VALUES (@review, @firstReviewer), (@review, @secondReviewer);";

            var users = InsertObject("firstReviewer", "User", "model_user") + InsertObject("secondReviewer", "User", "model_user");
            var review = InsertObject("review", "Review", "review") + "INSERT INTO fabric.functional_project_reviews (source_id, target_id) VALUES (@project, @review);";

            await this.ExecuteAsync(InsertProjectGraph());
            await this.ExecuteAsync(users + review + reviewers);
            await this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @firstReviewer;");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.review_reviewers"), Is.EqualTo(1));
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.review WHERE id = @review"), Is.EqualTo(1));
            }

            await this.ExecuteAsync("DELETE FROM fabric.thing WHERE id = @review;");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.review_reviewers"), Is.Zero);
                Assert.That(await this.ScalarAsync<long>("SELECT count(*) FROM fabric.model_user WHERE id = @secondReviewer"), Is.EqualTo(1));
            }
        }

        [GeneratedRegex(@"(?<!CREATE )CONSTRAINT (\w+)")]
        private static partial Regex ConstraintNameRegex();

        [GeneratedRegex(@"CONSTRAINT (\w+) (?:PRIMARY KEY|UNIQUE)")]
        private static partial Regex KeyConstraintNameRegex();

        [GeneratedRegex(@"CREATE INDEX (\w+)")]
        private static partial Regex IndexNameRegex();

        [GeneratedRegex(@"CREATE (?:CONSTRAINT )?TRIGGER (\w+)\s+AFTER [A-Z ]+? ON fabric\.(\w+)")]
        private static partial Regex TriggerNameRegex();

        [GeneratedRegex(@"(?<!')@(\w+)")]
        private static partial Regex ParameterNameRegex();

        private static string InsertRoot(string identity, string classKind, string data)
        {
            return $"INSERT INTO fabric.thing (id, class_kind, data) VALUES (@{identity}, '{classKind}', {data});\n";
        }

        private static string InsertObject(string identity, string classKind, string classTable, params (string Column, string Identity)[] references)
        {
            var columns = string.Concat(references.Select(reference => $", {reference.Column}"));
            var values = string.Concat(references.Select(reference => $", @{reference.Identity}"));
            var root = InsertRoot(identity, classKind, $"jsonb_build_object('@id', @{identity}, '@type', '{classKind}')");

            return $"""
                {root}INSERT INTO fabric.auditable_thing (id, created_by, updated_by) VALUES (@{identity}, @admin, @admin);
                INSERT INTO fabric.{classTable} (id{columns}) VALUES (@{identity}{values});

                """;
        }

        private static string InsertProjectGraph()
        {
            return InsertObject("admin", "User", "model_user") + InsertOrganization("organization", "policy") + InsertProject("project", "organization", "projectPolicy");
        }

        private static string InsertOrganization(string organization, string policy)
        {
            return InsertObject(policy, "OrganizationPolicy", "organization_policy") + InsertObject(organization, "Organization", "organization", ("policy", policy));
        }

        private static string InsertProject(string project, string organization, string policy)
        {
            return InsertObject(policy, "FunctionalProjectPolicy", "functional_project_policy")
                + InsertObject(project, "FunctionalProject", "functional_project", ("belongs_to", organization), ("policy", policy));
        }

        private static IResolveConstraint ViolatesConstraint(string constraintName)
        {
            return Throws.TypeOf<PostgresException>().With.Property(nameof(PostgresException.ConstraintName)).EqualTo(constraintName);
        }

        private static IResolveConstraint FailsWithState(string sqlState)
        {
            return Throws.TypeOf<PostgresException>().With.Property(nameof(PostgresException.SqlState)).EqualTo(sqlState);
        }

        private static IResolveConstraint RaisesMessage(string messagePrefix)
        {
            return Throws.TypeOf<PostgresException>().With.Property(nameof(PostgresException.MessageText)).StartsWith(messagePrefix);
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

        private async Task<List<string>> ReadNamesAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(this.database.GetConnectionString());

            await connection.OpenAsync();

            await using var command = this.CreateCommand(connection, sql);
            await using var reader = await command.ExecuteReaderAsync();

            var names = new List<string>();

            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }

            return names;
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
