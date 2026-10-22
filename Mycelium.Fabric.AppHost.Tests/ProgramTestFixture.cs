// ------------------------------------------------------------------------------------------------
//  <copyright file="ProgramTestFixture.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.AppHost.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Aspire.Hosting;
    using Aspire.Hosting.ApplicationModel;
    using Aspire.Hosting.Testing;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Logging;

    using Npgsql;

    using NUnit.Framework;

    [TestFixture]
    [NonParallelizable]
    public class ProgramTestFixture
    {
        private const string PostgresResourceName = "postgres";
        private const string DatabaseResourceName = "functionaldata";
        private const string DatabaseStartupHealthCheckName = "functionaldata-startup-gate";
        private const string ServerResourceName = "concurrent-server";
        private const string VolumePrefix = "mycelium-fabric-apphost-tests-";
        private const string OwnerLabel = "mycelium.fabric.apphost-test";

        private static readonly TimeSpan EnvironmentTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan ServerStartupObservationTimeout = TimeSpan.FromSeconds(5);

        [SetUp]
        public void RequireEnvironmentTestOptIn()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS"), "1", StringComparison.Ordinal))
            {
                Assert.Ignore("Set MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS=1 to run the Docker environment tests.");
            }
        }

        [Test]
        public async Task VerifyMain()
        {
            var ownerId = Guid.NewGuid().ToString("N");
            var volumeName = VolumePrefix + ownerId;
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var failures = new List<string>();
            var volumeCreationAttempted = false;
            var phase = "creating isolated storage";

            try
            {
                using var timeout = new CancellationTokenSource(EnvironmentTimeout);
                var existingVolumes = await RunDockerAsync(["volume", "ls", "--quiet", "--filter", "name=" + volumeName], timeout.Token);

                Assert.That(existingVolumes, Is.Empty, "The test volume name must be unused before creation.");

                volumeCreationAttempted = true;
                await RunDockerAsync(["volume", "create", "--label", OwnerLabel + "=" + ownerId, volumeName], timeout.Token);

                phase = "startup ordering, connectivity, outage and container restart";
                var retainedState = await VerifyInitialEnvironmentAsync(volumeName, password, failures, timeout.Token);

                phase = "normal shutdown and retained storage";
                var retainedVolume = await RunDockerAsync(["volume", "inspect", "--format", "{{.Name}}", volumeName], timeout.Token);
                var remainingContainers = await RunDockerAsync(["ps", "--all", "--quiet", "--filter", "volume=" + volumeName], timeout.Token);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(retainedVolume, Is.EqualTo(volumeName));
                    Assert.That(remainingContainers, Is.Empty, "Normal AppHost shutdown must remove its session container.");
                }

                phase = "AppHost replacement";
                using var replacementTimeout = new CancellationTokenSource(EnvironmentTimeout);
                await VerifyReplacementEnvironmentAsync(volumeName, password, retainedState.ClusterIdentifier, retainedState.ContainerId, replacementTimeout.Token);
            }
            catch (AssertionException exception)
            {
                failures.Add(phase + ": " + exception.Message);
            }
            catch (Exception exception)
            {
                failures.Add(phase + " failed (" + exception.GetType().Name + "). Check Docker availability and the built AppHost/server outputs.");
            }
            finally
            {
                if (volumeCreationAttempted)
                {
                    try
                    {
                        using var cleanupTimeout = new CancellationTokenSource(DockerCommandTimeout);
                        await RemoveOwnedVolumeAsync(volumeName, ownerId, cleanupTimeout.Token);
                    }
                    catch (Exception exception)
                    {
                        failures.Add("Cleanup failed for test-owned volume " + volumeName + " (" + exception.GetType().Name + ").");
                    }
                }
            }

            Assert.That(failures, Is.Empty);
        }

        private static async Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(string volumeName, string password, CancellationToken cancellationToken)
        {
            var builder = await DistributedApplicationTestingBuilder.CreateAsync(
                typeof(global::Mycelium.Fabric.AppHost.Program),
                ["--environment=Testing", "--FunctionalData:VolumeName=" + volumeName, "--ASPIRE_USER_SECRETS_ID=" + volumeName],
                (options, settings) =>
                {
                    options.DisableDashboard = true;
                    options.EnableResourceLogging = false;
                    settings.EnvironmentName = "Testing";
                    settings.Configuration ??= new ConfigurationManager();
                    settings.Configuration.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["environment"] = "Testing",
                        ["DOTNET_ENVIRONMENT"] = "Testing",
                        ["ASPIRE_USER_SECRETS_ID"] = volumeName,
                        ["Parameters:postgres-password"] = password
                    });
                },
                cancellationToken);

            try
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Parameters:postgres-password"] = password,
                    ["DcpPublisher:ContainerRuntime"] = "docker",
                    ["ASPIRE_CONTAINER_RUNTIME"] = "docker"
                });
                builder.Services.AddLogging(logging => logging.ClearProviders());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(builder.Environment.EnvironmentName, Is.EqualTo("Testing"));
                    Assert.That(builder.Configuration["ASPIRE_USER_SECRETS_ID"], Is.EqualTo(volumeName));
                }

                return builder;
            }
            catch
            {
                await builder.DisposeAsync();
                throw;
            }
        }

        private static async Task<(string ClusterIdentifier, string ContainerId)> VerifyInitialEnvironmentAsync(
            string volumeName, string password, ICollection<string> failures, CancellationToken cancellationToken)
        {
            await using var builder = await CreateBuilderAsync(volumeName, password, cancellationToken);
            var postgres = builder.Resources.OfType<PostgresServerResource>().Single(resource => resource.Name == PostgresResourceName);
            builder.CreateResourceBuilder(postgres).WithExplicitStart();

            var databaseHealthReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            builder.Services.AddHealthChecks().AddCheck(DatabaseStartupHealthCheckName,
                () => databaseHealthReleased.Task.IsCompletedSuccessfully ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Held by the environment test."));

            var database = builder.Resources.OfType<PostgresDatabaseResource>().Single(resource => resource.Name == DatabaseResourceName);
            builder.CreateResourceBuilder(database).WithHealthCheck(DatabaseStartupHealthCheckName);

            await using var application = await builder.BuildAsync(cancellationToken);
            using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var observationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var startupObservation = ObserveStartupOrderAsync(application, observationCancellation.Token);
            var applicationStartup = Task.CompletedTask;

            try
            {
                // Startup includes the server's dependency wait, so release the test gates before awaiting completion.
                applicationStartup = application.StartAsync(startupCancellation.Token);
                await application.ResourceNotifications.WaitForResourceAsync(PostgresResourceName, KnownResourceStates.NotStarted, cancellationToken);
                await application.ResourceNotifications.WaitForResourceAsync(ServerResourceName, KnownResourceStates.Waiting, cancellationToken);

                await ExecuteCommandAsync(application, KnownResourceCommands.StartCommand, cancellationToken);
                await application.ResourceNotifications.WaitForResourceHealthyAsync(PostgresResourceName, cancellationToken);
                await application.ResourceNotifications.WaitForResourceAsync(
                    DatabaseResourceName,
                    resourceEvent => resourceEvent.Snapshot.HealthStatus == HealthStatus.Unhealthy
                        && resourceEvent.Snapshot.HealthReports.Any(report => report.Name == DatabaseStartupHealthCheckName && report.Status == HealthStatus.Unhealthy),
                    cancellationToken);

                // Prove that PostgreSQL accepts connections while the database's test-only health gate remains closed.
                // Resolve the resource expression directly because the testing helper requires completed AppHost startup.
                var readyDatabaseConnection = await database.ConnectionStringExpression.GetValueAsync(cancellationToken);
                await ReadClusterIdentifierAsync(readyDatabaseConnection, cancellationToken);

                using (var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    startupTimeout.CancelAfter(ServerStartupObservationTimeout);

                    Assert.That(
                        async () => await application.ResourceNotifications.WaitForResourceAsync(ServerResourceName, KnownResourceStates.Running, startupTimeout.Token),
                        Throws.InstanceOf<OperationCanceledException>(),
                        "The server must remain waiting while its database is unhealthy, even when PostgreSQL is healthy.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(GetSnapshot(application, PostgresResourceName).State?.Text, Is.EqualTo(KnownResourceStates.Running));
                    Assert.That(GetSnapshot(application, PostgresResourceName).HealthStatus, Is.EqualTo(HealthStatus.Healthy));
                    Assert.That(GetSnapshot(application, DatabaseResourceName).HealthStatus, Is.EqualTo(HealthStatus.Unhealthy));
                    Assert.That(GetSnapshot(application, ServerResourceName).State?.Text, Is.EqualTo(KnownResourceStates.Waiting));
                    Assert.That(GetSnapshot(application, ServerResourceName).StartTimeStamp, Is.Null);
                }

                databaseHealthReleased.SetResult();
                await applicationStartup;
                await startupObservation;
                await application.ResourceNotifications.WaitForResourceHealthyAsync(DatabaseResourceName, cancellationToken);

                var connectionString = await GetDeliveredConnectionStringAsync(application, cancellationToken);
                var clusterIdentifier = await ReadClusterIdentifierAsync(connectionString, cancellationToken);
                var containerId = await GetContainerIdAsync(volumeName, cancellationToken);
                var serverStartTime = GetSnapshot(application, ServerResourceName).StartTimeStamp;

                Assert.That(serverStartTime, Is.Not.Null);
                await VerifyHttpResponseAsync(application, cancellationToken);

                await ExecuteCommandAsync(application, KnownResourceCommands.StopCommand, cancellationToken);
                await application.ResourceNotifications.WaitForResourceAsync(PostgresResourceName, KnownResourceStates.Exited, cancellationToken);

                Assert.That(async () => await ReadClusterIdentifierAsync(connectionString, cancellationToken), Throws.InstanceOf<NpgsqlException>());
                await VerifyHttpResponseAsync(application, cancellationToken);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(GetSnapshot(application, ServerResourceName).State?.Text, Is.EqualTo(KnownResourceStates.Running));
                    Assert.That(GetSnapshot(application, ServerResourceName).StartTimeStamp, Is.EqualTo(serverStartTime));
                }

                await ExecuteCommandAsync(application, KnownResourceCommands.StartCommand, cancellationToken);
                await WaitForFreshDatabaseHealthAsync(application, DateTime.UtcNow, cancellationToken);

                var recoveredClusterIdentifier = await ReadClusterIdentifierAsync(connectionString, cancellationToken);
                Assert.That(recoveredClusterIdentifier, Is.EqualTo(clusterIdentifier));

                // Restart the actual Docker container, separately from replacing an AppHost below.
                containerId = await GetContainerIdAsync(volumeName, cancellationToken);
                await RunDockerAsync(["restart", containerId], cancellationToken);
                await WaitForFreshDatabaseHealthAsync(application, DateTime.UtcNow, cancellationToken);

                var restartedContainerId = await GetContainerIdAsync(volumeName, cancellationToken);
                var restartedClusterIdentifier = await ReadClusterIdentifierAsync(connectionString, cancellationToken);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(restartedContainerId, Is.EqualTo(containerId));
                    Assert.That(restartedClusterIdentifier, Is.EqualTo(clusterIdentifier));
                    Assert.That(GetSnapshot(application, ServerResourceName).StartTimeStamp, Is.EqualTo(serverStartTime));
                }

                await application.StopAsync(cancellationToken);
                return (clusterIdentifier, containerId);
            }
            catch (Exception exception) when (exception is not AssertionException)
            {
                throw new AssertionException("Initial environment failed (" + exception.GetType().Name + "). " + DescribeResourceStates(application));
            }
            finally
            {
                startupCancellation.Cancel();
                observationCancellation.Cancel();

                try
                {
                    await Task.WhenAll(applicationStartup, startupObservation);
                }
                catch (OperationCanceledException) when (startupCancellation.IsCancellationRequested && observationCancellation.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    failures.Add("Startup cleanup failed (" + exception.GetType().Name + "). " + DescribeResourceStates(application));
                }
            }
        }

        private static async Task VerifyReplacementEnvironmentAsync(
            string volumeName, string password, string clusterIdentifier, string previousContainerId, CancellationToken cancellationToken)
        {
            await using var builder = await CreateBuilderAsync(volumeName, password, cancellationToken);
            await using var application = await builder.BuildAsync(cancellationToken);

            try
            {
                await application.StartAsync(cancellationToken);
                await application.ResourceNotifications.WaitForResourceHealthyAsync(DatabaseResourceName, cancellationToken);
                await application.ResourceNotifications.WaitForResourceAsync(ServerResourceName, KnownResourceStates.Running, cancellationToken);

                var connectionString = await GetDeliveredConnectionStringAsync(application, cancellationToken);
                var retainedClusterIdentifier = await ReadClusterIdentifierAsync(connectionString, cancellationToken);
                var replacementContainerId = await GetContainerIdAsync(volumeName, cancellationToken);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(retainedClusterIdentifier, Is.EqualTo(clusterIdentifier));
                    Assert.That(replacementContainerId, Is.Not.EqualTo(previousContainerId));
                }

                await VerifyHttpResponseAsync(application, cancellationToken);
                await application.StopAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not AssertionException)
            {
                throw new AssertionException("Replacement environment failed (" + exception.GetType().Name + "). " + DescribeResourceStates(application));
            }
        }

        private static async Task ObserveStartupOrderAsync(DistributedApplication application, CancellationToken cancellationToken)
        {
            var databaseWasHealthy = false;

            await foreach (var resourceEvent in application.ResourceNotifications.WatchAsync(cancellationToken))
            {
                if (resourceEvent.Resource.Name == DatabaseResourceName && resourceEvent.Snapshot.HealthStatus == HealthStatus.Healthy)
                {
                    databaseWasHealthy = true;
                }

                if (resourceEvent.Resource.Name == ServerResourceName && resourceEvent.Snapshot.State?.Text == KnownResourceStates.Running)
                {
                    Assert.That(databaseWasHealthy, Is.True, "The database must become healthy before the server starts.");
                    return;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Resource observation ended before the server started.");
        }

        private static Task<ResourceEvent> WaitForFreshDatabaseHealthAsync(DistributedApplication application, DateTime after, CancellationToken cancellationToken)
        {
            return application.ResourceNotifications.WaitForResourceAsync(
                DatabaseResourceName,
                resourceEvent => resourceEvent.Snapshot.HealthStatus == HealthStatus.Healthy
                    && resourceEvent.Snapshot.HealthReports.Length > 0
                    && resourceEvent.Snapshot.HealthReports.All(report => report.Status == HealthStatus.Healthy && report.LastRunAt is { } checkedAt && checkedAt >= after),
                cancellationToken);
        }

        private static CustomResourceSnapshot GetSnapshot(DistributedApplication application, string resourceName)
        {
            if (!application.ResourceNotifications.TryGetCurrentState(resourceName, out var resourceEvent))
            {
                throw new InvalidOperationException("Missing resource state: " + resourceName);
            }

            return resourceEvent.Snapshot;
        }

        private static string DescribeResourceStates(DistributedApplication application)
        {
            string[] resourceNames = [PostgresResourceName, DatabaseResourceName, ServerResourceName];

            return string.Join("; ", resourceNames.Select(resourceName =>
            {
                return application.ResourceNotifications.TryGetCurrentState(resourceName, out var resourceEvent)
                    ? resourceName + ": state=" + resourceEvent.Snapshot.State?.Text + ", health=" + resourceEvent.Snapshot.HealthStatus
                    : resourceName + ": no state reported";
            }));
        }

        private static async Task ExecuteCommandAsync(DistributedApplication application, string commandName, CancellationToken cancellationToken)
        {
            var result = await application.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(PostgresResourceName, commandName, cancellationToken);

            Assert.That(result.Success, Is.True, "PostgreSQL resource command failed: " + commandName);
        }

        private static async Task<string> GetDeliveredConnectionStringAsync(DistributedApplication application, CancellationToken cancellationToken)
        {
            var resolved = await application.GetConnectionStringAsync(DatabaseResourceName, cancellationToken);
            var delivered = GetSnapshot(application, ServerResourceName).EnvironmentVariables
                .SingleOrDefault(variable => variable.Name == "ConnectionStrings__functionaldata")?.Value;

            // Compare booleans so assertion diagnostics cannot disclose either connection string.
            Assert.That(!string.IsNullOrWhiteSpace(delivered), Is.True, "The server must receive the Functional Data connection.");
            Assert.That(string.Equals(delivered, resolved, StringComparison.Ordinal), Is.True, "The delivered connection must match the database resource.");

            return delivered;
        }

        private static async Task<string> ReadClusterIdentifierAsync(string connectionString, CancellationToken cancellationToken)
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                Timeout = 5,
                CommandTimeout = 5
            };

            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT current_database(), 1, system_identifier::text FROM pg_control_system()", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            Assert.That(await reader.ReadAsync(cancellationToken), Is.True);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(reader.GetString(0), Is.EqualTo("mycelium_functional_data"));
                Assert.That(reader.GetInt32(1), Is.EqualTo(1));
            }

            return reader.GetString(2);
        }

        private static async Task VerifyHttpResponseAsync(DistributedApplication application, CancellationToken cancellationToken)
        {
            using var client = application.CreateHttpClient(ServerResourceName, "http");
            using var response = await client.GetAsync("projects", cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var problem = document.RootElement;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
                Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo(500));
                Assert.That(problem.GetProperty("title").GetString(), Is.EqualTo("Not yet implemented"));
                Assert.That(problem.GetProperty("detail").GetString(), Is.EqualTo("The 'getProjects' operation is registered but is not yet implemented by this server."));
                Assert.That(problem.GetProperty("instance").GetString(), Is.EqualTo("/projects"));
                Assert.That(problem.GetProperty("operationId").GetString(), Is.EqualTo("getProjects"));
            }
        }

        private static async Task<string> GetContainerIdAsync(string volumeName, CancellationToken cancellationToken)
        {
            var output = await RunDockerAsync(["ps", "--all", "--quiet", "--no-trunc", "--filter", "volume=" + volumeName], cancellationToken);
            var containerIds = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            Assert.That(containerIds, Has.Length.EqualTo(1), "Exactly one test container must use the test-owned volume.");
            return containerIds[0];
        }

        private static async Task RemoveOwnedVolumeAsync(string volumeName, string ownerId, CancellationToken cancellationToken)
        {
            Assert.That(volumeName, Is.EqualTo(VolumePrefix + ownerId));
            Assert.That(Guid.TryParseExact(ownerId, "N", out _), Is.True);

            var existingVolumes = await RunDockerAsync(["volume", "ls", "--quiet", "--filter", "name=" + volumeName], cancellationToken);

            if (string.IsNullOrWhiteSpace(existingVolumes))
            {
                return;
            }

            var label = await RunDockerAsync(["volume", "inspect", "--format", "{{ index .Labels \"" + OwnerLabel + "\" }}", volumeName], cancellationToken);

            Assert.That(label, Is.EqualTo(ownerId), "Refusing to remove a volume without the matching test ownership label.");
            await RunDockerAsync(["volume", "rm", volumeName], cancellationToken);
        }

        private static async Task<string> RunDockerAsync(string[] arguments, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DockerCommandTimeout);

            var startInfo = new ProcessStartInfo("docker")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(output, error);
            }
            catch
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Docker command failed with exit code " + process.ExitCode + ".");
            }

            return (await output).Trim();
        }
    }
}
