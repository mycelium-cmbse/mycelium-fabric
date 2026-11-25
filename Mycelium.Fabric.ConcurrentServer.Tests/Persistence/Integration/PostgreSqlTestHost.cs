// ------------------------------------------------------------------------------------------------
//  <copyright file="PostgreSqlTestHost.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.ConcurrentServer.Tests.Persistence.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging.Testing;

    using Mycelium.Fabric.ConcurrentServer.Persistence;

    using Npgsql;

    using SysML2.NET.Core;

    using Testcontainers.PostgreSql;

    [SetUpFixture]
    public class PostgreSqlTestHost
    {
        private static readonly string[] ContainerEngineEndpoints =
        [
            @"\\.\pipe\dockerDesktopLinuxEngine",
            @"\\.\pipe\docker_engine",
            @"\\.\pipe\podman-machine-default"
        ];

        private PostgreSqlContainer container;

        public static NpgsqlDataSource DataSource { get; private set; }

        [OneTimeSetUp]
        public async Task StartAsync()
        {
            var builder = new PostgreSqlBuilder("postgres:18").WithCommand("-c", "max_locks_per_transaction=4096");
            var endpoint = ResolveContainerEngineEndpoint();

            if (endpoint != null)
            {
                builder = builder.WithDockerEndpoint(endpoint);
            }

            try
            {
                this.container = builder.Build();
                await this.container.StartAsync();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Assert.Ignore($"No container engine is available for the PostgreSQL integration tests: {exception.Message}");
            }

            DataSource = NpgsqlDataSource.Create(this.container.GetConnectionString());

            await new SqlSchemaMigrator(DataSource, new FakeLogger<SqlSchemaMigrator>()).MigrateAsync(CancellationToken.None);
        }

        [OneTimeTearDown]
        public async Task StopAsync()
        {
            if (DataSource != null)
            {
                await DataSource.DisposeAsync();
            }

            if (this.container != null)
            {
                await this.container.DisposeAsync();
            }
        }

        public static async Task<ISqlTransaction> BeginAsync(TransactionMode mode)
        {
            var transaction = await new SqlTransactionFactory(DataSource).BeginAsync(mode, CancellationToken.None);

            return (ISqlTransaction)transaction.Value;
        }

        public static async Task<Guid> SeedProjectAsync()
        {
            var projectId = Guid.NewGuid();

            await ExecuteAsync("INSERT INTO sysml2.project (id, name) VALUES (@id, 'the project')", new NpgsqlParameter("id", projectId));

            return projectId;
        }

        public static async Task<Guid> SeedCommitAsync(Guid projectId)
        {
            var commitId = Guid.NewGuid();

            await ExecuteAsync("INSERT INTO sysml2.commit (id, project_id, model_version_id) VALUES (@id, @project_id, 1)", new NpgsqlParameter("id", commitId),
                new NpgsqlParameter("project_id", projectId));

            return commitId;
        }

        public static async Task SeedIdentitiesAsync(Guid projectId, ClassKind classKind, IReadOnlyCollection<Guid> identityIds)
        {
            await ExecuteAsync("INSERT INTO sysml2.data_identity (id, project_id, class_kind) SELECT id, @project_id, @class_kind FROM unnest(@ids) AS id",
                new NpgsqlParameter("project_id", projectId), new NpgsqlParameter("class_kind", (short)classKind), new NpgsqlParameter("ids", identityIds.ToArray()));
        }

        public static async Task<long> CountAsync(string sql, params NpgsqlParameter[] parameters)
        {
            await using var command = DataSource.CreateCommand(sql);
            command.Parameters.AddRange(parameters);

            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        private static async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
        {
            await using var command = DataSource.CreateCommand(sql);
            command.Parameters.AddRange(parameters);

            await command.ExecuteNonQueryAsync();
        }

        private static string ResolveContainerEngineEndpoint()
        {
            var configuredHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

            if (!string.IsNullOrWhiteSpace(configuredHost))
            {
                return configuredHost;
            }

            var pipe = OperatingSystem.IsWindows() ? Array.Find(ContainerEngineEndpoints, File.Exists) : null;

            return pipe == null ? null : $"npipe://./pipe/{Path.GetFileName(pipe)}";
        }
    }
}
