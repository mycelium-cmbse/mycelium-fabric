# Mycelium Fabric

Mycelium Fabric is the server tier of the Mycelium concurrent modeling platform. The concurrent server exposes HTTP routes and defines service and persistence contracts.

[Issue #43](https://github.com/mycelium-cmbse/mycelium-fabric/issues/43) supplies a local PostgreSQL environment through Aspire. An **AppHost** is the executable that declares resources and their startup relationships. It starts PostgreSQL and the existing server, creates the Functional Data database if absent, and supplies connection configuration.

Environment setup does not implement model persistence. Application database integration and generated data access belong to [#33](https://github.com/mycelium-cmbse/mycelium-fabric/issues/33) and [#42](https://github.com/mycelium-cmbse/mycelium-fabric/issues/42). HTTP handlers retain their placeholder responses. No application database client, schema, migrations, Keycloak or RabbitMQ is included.

## Prerequisites

- A supported .NET 10 SDK; the projects use C# 14.
- Docker CLI on `PATH` and a running Docker engine capable of Linux containers. On Windows, configure Docker Desktop for Linux containers. On Linux, use a suitable Docker installation accessible to your user.
- A trusted local ASP.NET Core development certificate for the HTTPS dashboard.
- First-use access to NuGet.org, Aspire tooling downloads and `docker.io/library/postgres:18.6`.

AppHost and the concurrent server run as local .NET processes. PostgreSQL runs in Docker. The same project declarations work on Windows and Linux; each machine still needs its own prerequisites, credentials and local storage. No server Dockerfile, Compose file, global Aspire CLI or legacy Aspire workload is required for the documented `dotnet run` workflow.

The commands below run from the repository root. PowerShell is used where environment variables need to be managed.

Check prerequisites without starting the application:

```powershell
dotnet --info
docker info --format '{{.OSType}}'
dotnet dev-certs https --check --trust
```

Expect a .NET 10 SDK, Docker operating-system type `linux`, and a valid trusted certificate. If the certificate is missing or untrusted, the following setup command creates/trusts the development certificate and may display an operating-system trust prompt:

```powershell
dotnet dev-certs https --trust
```

See the official [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/), [.NET certificate commands](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs) and [Docker information command](https://docs.docker.com/reference/cli/docker/system/info/).

## Resources and configuration

| Item | Value and role |
| --- | --- |
| AppHost project / launch profile | `Mycelium.Fabric.AppHost` |
| PostgreSQL resource | `postgres`, using `docker.io/library/postgres:18.6` |
| Database resource / connection name | `functionaldata` |
| Physical database | `mycelium_functional_data` |
| Server process resource | `concurrent-server` |
| Server configuration key | `ConnectionStrings:functionaldata` |
| Delivered process environment key | `ConnectionStrings__functionaldata` |
| Development Docker volume | `mycelium-fabric-functionaldata-pg18` |
| AppHost volume override | `FunctionalData:VolumeName`, environment form `FunctionalData__VolumeName` |
| AppHost local secret-store identifier | `mycelium-fabric-apphost` |
| Generated password parameter | `postgres-password`, configuration key `Parameters:postgres-password` |

Host ports are allocated dynamically. Use the dashboard's reported endpoints instead of assuming PostgreSQL port 5432 or a fixed server port on the host.

AppHost's database reference delivers configuration to the actual server process. Its separate readiness relationship waits for the intended database to become healthy before starting that process. A running container alone is insufficient. Before initial startup, unavailable PostgreSQL keeps the server waiting; recovery can release the gate. A later database outage does not automatically stop or restart an already-running server.

The server's existing configuration builder loads the delivered environment variable. This does not register an application database client or open an application connection. Aspire's hosting health checks make their own connections. See the official [AppHost overview](https://aspire.dev/get-started/app-host/) and [PostgreSQL hosting integration](https://aspire.dev/integrations/databases/postgres/postgres-host/).

## Build and ordinary regression checks

These checks compile projects and exercise existing tests without requiring Docker. Leave `MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS` unset: the environment fixture skips before creating AppHost or contacting Docker unless its value is exactly `1`.

Run the existing server checks:

```powershell
dotnet restore Mycelium.Fabric.ConcurrentServer.Tests/Mycelium.Fabric.ConcurrentServer.Tests.csproj
dotnet build Mycelium.Fabric.ConcurrentServer.Tests/Mycelium.Fabric.ConcurrentServer.Tests.csproj --configuration Debug --no-restore
dotnet test Mycelium.Fabric.ConcurrentServer.Tests/Mycelium.Fabric.ConcurrentServer.Tests.csproj --configuration Debug --no-restore --no-build
```

Solution/project wiring or shared dependency changes also require the solution checks. These include the existing MCP projects as regression coverage:

```powershell
dotnet restore Mycelium.Fabric.sln
dotnet build Mycelium.Fabric.sln --configuration Debug --no-restore
dotnet test Mycelium.Fabric.sln --configuration Debug --no-restore --no-build
```

Expect successful restore/build, passing existing regressions, and a skipped environment fixture when opt-in is absent. A successful build does not establish resource startup or database connectivity. Test-authoring conventions are in [TESTING.md](TESTING.md).

After restore, inspect requested and resolved environment-package versions without another restore:

```powershell
dotnet package list --project Mycelium.Fabric.AppHost/Mycelium.Fabric.AppHost.csproj --no-restore
dotnet package list --project Mycelium.Fabric.AppHost.Tests/Mycelium.Fabric.AppHost.Tests.csproj --no-restore
```

## Start and stop the local environment

```powershell
dotnet run --project Mycelium.Fabric.AppHost/Mycelium.Fabric.AppHost.csproj --launch-profile Mycelium.Fabric.AppHost
```

Follow the protected local HTTPS dashboard URL reported at startup. Keep login URLs and access tokens private. The dashboard shows resource states, health, console logs and lifecycle controls. Application traces and metrics require instrumentation beyond this increment.

Expect `postgres` and `functionaldata` to become healthy, followed by `concurrent-server` running. PostgreSQL initialization creates the database if it is absent, without creating Fabric schemas or domain tables. Follow the server's HTTP endpoint from the dashboard. `GET /projects` still returns HTTP 500 with `application/problem+json`, title `Not yet implemented`, and operation identifier `getProjects`; this is the current HTTP contract.

Stop normally with **Ctrl+C** in the AppHost terminal. Its session-scoped container and managed processes stop; the named development volume and matching local credentials remain available for the next launch.

The existing direct-server workflow remains available:

```powershell
dotnet run --project Mycelium.Fabric.ConcurrentServer/Mycelium.Fabric.ConcurrentServer.csproj
```

Direct launch does not orchestrate PostgreSQL or apply the AppHost readiness gate. It retains the existing HTTP behavior.

## Credentials and retained data

During normal Development launch, Aspire generates the PostgreSQL password parameter and stores its generated default in AppHost user secrets. **User secrets** are local developer configuration outside the repository; they are not a production secret manager. Keep the stable `mycelium-fabric-apphost` identifier and matching credential for subsequent launches. Do not commit or print passwords, full connection strings or dashboard tokens.

Docker owns the named development volume. PostgreSQL 18 mounts it at `/var/lib/postgresql`. Database files survive normal AppHost shutdown, a container restart, and container replacement when the same volume is mounted. The container is session-scoped; the volume has an independent lifetime. Each developer owns their local volume and secrets. Neither is stored or synchronized through Git, and a volume is not a backup.

Changing `Parameters:postgres-password` does not rotate the database-role password in an already initialized PostgreSQL cluster. Retained storage must be used with its matching credential. Investigate a mismatch before changing or removing either.

### Deliberate destructive reset

A development reset permanently discards the database cluster in the selected volume. It is separate maintenance, never a normal verification or recovery step:

1. Stop AppHost and ensure no other container uses the development volume.
2. Back up any data that must be retained, and confirm the exact target is `mycelium-fabric-functionaldata-pg18` or your explicitly selected override.
3. Remove only that exact volume through Docker. Do not use wildcard removal or Docker-wide pruning.
4. Start AppHost again. PostgreSQL initializes a fresh cluster and database using the configured credential.

Test volumes have separate ownership and must not be confused with development storage.

## Opt-in automated Docker environment checks

Restore and build the solution first. With the prerequisites available, explicitly enable and run the environment fixture:

```powershell
$previousEnvironmentTestOptIn = $env:MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS
try
{
    $env:MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS = '1'
    dotnet test Mycelium.Fabric.AppHost.Tests/Mycelium.Fabric.AppHost.Tests.csproj --configuration Debug --no-restore --no-build
}
finally
{
    $env:MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS = $previousEnvironmentTestOptIn
}
```

For Bash, scope opt-in to the test command:

```bash
MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS=1 dotnet test Mycelium.Fabric.AppHost.Tests/Mycelium.Fabric.AppHost.Tests.csproj --configuration Debug --no-restore --no-build
```

With opt-in, expect the environment test to execute rather than skip. Missing Docker, resource failures or readiness timeouts produce failures. Initial and replacement environment stages each have a five-minute deadline; individual Docker commands have a one-minute deadline. A first image download can take longer than subsequent runs.

The fixture verifies:

- Initial server waiting while PostgreSQL is held, then database health before server startup.
- Delivery of `ConnectionStrings__functionaldata` to the launched server without displaying its value.
- Authenticated, non-mutating SQL that confirms `mycelium_functional_data`, executes a simple query and reads the PostgreSQL cluster identifier.
- The real server's unchanged `GET /projects` problem response.
- A database outage after startup while the server remains running, followed by recovery.
- An actual container restart and an AppHost/container replacement using the same isolated volume and credential, with the same cluster identifier after reconnecting.
- Normal shutdown and cleanup of the test-owned storage.

Each run creates unpredictable in-memory credentials and a unique volume prefixed `mycelium-fabric-apphost-tests-`, labelled `mycelium.fabric.apphost-test` with its owner identifier. A Testing environment and unique secret-store identifier isolate the run from development user secrets. Npgsql is test-only; probes create no domain tables or records.

The fixture disposes its AppHosts and attempts cleanup even after assertion failure. It removes only the exact volume with the matching ownership label. If cleanup fails, its report identifies the test-owned volume for investigation. An interrupted process may leave resources behind; inspect ownership and usage before any deliberate cleanup. Never remove development storage to make a test pass.

These checks use [Aspire's testing integration](https://aspire.dev/testing/overview/). They do not prove model persistence, transaction semantics or concurrent-update safety. Existing CI does not opt into these Docker tests.

## Interactive dashboard smoke check

Headless environment tests disable the dashboard. Check its browser behavior separately:

1. Start AppHost with the documented launch command and open the reported protected local HTTPS URL. Confirm normal access protection and a trusted certificate.
2. Confirm PostgreSQL/database health and the running server. Open the server's reported HTTP endpoint with `/projects`; expect the placeholder HTTP 500 problem response described above.
3. Stop AppHost with Ctrl+C. Confirm managed processes/container stop. This read-only command should still report the development volume's name:

    ```powershell
    docker volume inspect mycelium-fabric-functionaldata-pg18 --format '{{.Name}}'
    ```

4. Launch AppHost again with the same configuration and secrets; confirm readiness and server startup again. If a volume override was deliberately configured, inspect that exact name instead.

Report build results, existing unit regressions, automated environment startup/connectivity/lifecycle results, and interactive dashboard results separately. A green dashboard or successful connection alone is not evidence of implemented Functional Data persistence.

## Dependency management

The AppHost SDK declaration is `Aspire.AppHost.Sdk/13.6.1`. Explicit environment packages are `Aspire.Hosting.PostgreSQL` and `Aspire.Hosting.Testing` at `13.6.1`, with test-only `Npgsql` at `10.0.3`. Environment tests use `Microsoft.NET.Test.Sdk` `18.8.1`, `NUnit` `4.6.1` and `NUnit3TestAdapter` `6.2.0`. The AppHost SDK supplies its own implicit `Aspire.Hosting.AppHost` reference; do not add a duplicate reference or central version for it. The configured SDK uses the matching CLI bundle through .NET 10's `dnx` support; see the [Aspire SDK documentation](https://aspire.dev/get-started/aspire-sdk/).

**Central Package Management (CPM)** means owning NuGet package versions in root `Directory.Packages.props`. Where scoped CPM configuration is absent, project references declare their versions locally. When present, its activation properties and central-version items must cover the same four projects: ConcurrentServer, ConcurrentServer.Tests, AppHost and AppHost.Tests. Keep MCP excluded, `CentralPackageTransitivePinningEnabled=false`, and participating package references versionless without `VersionOverride`. Transfer version ownership coherently; central entries alone do not activate CPM or supply versions to an excluded project. The AppHost SDK version remains in its SDK declaration.

Preserve existing package asset metadata, both server-test coverlet references and test `IsPackable=false`. Do not combine package-ownership changes with unrelated upgrades. Fabric has no deliberately floored packages: no package is intentionally held at a minimum version as policy.

After restore, review direct and transitive dependency reports:

```powershell
dotnet list Mycelium.Fabric.sln package --outdated --include-transitive --no-restore
dotnet list Mycelium.Fabric.sln package --deprecated --include-transitive --no-restore
dotnet list Mycelium.Fabric.sln package --vulnerable --include-transitive --no-restore
```

Review findings for production and tests. An outdated result alone does not select an upgrade; assess compatibility, deprecation and security separately. Package changes require the applicable restore/build/regression checks above. Fresh package evaluation is required after transferring version ownership.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| SDK or AppHost tooling cannot resolve | Confirm .NET 10 SDK selection and access to NuGet/tool sources. Use the pinned SDK configuration; do not install an obsolete Aspire workload. |
| Docker unavailable or wrong container platform | Check Docker CLI access, engine status and `docker info --format '{{.OSType}}'`; Windows must use Linux containers. |
| PostgreSQL image cannot start or be pulled | Check network/registry access, image availability, Docker diagnostics and resource health. Keep credentials out of reports. |
| HTTPS dashboard fails certificate validation | Check/trust the local development certificate and use the reported URL. Keep normal dashboard access protection enabled. |
| Server remains waiting | Inspect `postgres` and `functionaldata` health and console logs. The database must be ready before initial server startup. |
| Authentication fails with retained storage | Check that the local password parameter still matches that cluster. Changing initialization configuration does not rotate an existing role password. |
| Environment fixture is skipped | Set `MYCELIUM_FABRIC_RUN_ENVIRONMENT_TESTS=1` only for the intended Docker test run. |
| Environment test times out | Check Docker, first-use downloads and reported resource states. Distinguish missing prerequisites from an implementation regression. |
| Test-owned storage remains after a failure | Use the exact reported identifier; inspect its ownership label and container usage before deliberate cleanup. Avoid blanket removal or pruning. |
| `/projects` returns HTTP 500 | `Not yet implemented` problem JSON is the current placeholder contract; database availability does not implement the handler. |
