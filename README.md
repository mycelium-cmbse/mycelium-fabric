# Mycelium Fabric

Mycelium Fabric is the server tier of the Mycelium platform. The concurrent server and its NUnit test
suite target .NET 10. The server uses the Worker SDK and external SysML2.NET NuGet packages.

## Dependency management

[Directory.Packages.props](Directory.Packages.props) owns NuGet dependency versions through
**Central Package Management (CPM)** for:

- `Mycelium.Fabric.ConcurrentServer`
- `Mycelium.Fabric.ConcurrentServer.Tests`

`Mycelium.Fabric.Mcp` and `Mycelium.Fabric.Mcp.Tests` retain versions in their project files and are
outside this CPM scope. Their migration requires a separate scope decision; the server migration does
not complete repository-wide adoption.

Each direct dependency has one central `PackageVersion`. Its consuming project retains a
`PackageReference` without `Version` or `VersionOverride`. A central version entry does not add a
dependency to a project. The server's own product `Version` is independent of dependency versions.

`CentralPackageTransitivePinningEnabled=false`: packages required indirectly by other packages follow
ordinary NuGet dependency constraints. Do not add direct references or enable transitive pinning to
force lower indirect versions.

### Version policy

The server and tests maintain compatible, supported dependencies. Assess updates deliberately against
the APIs used, target-framework compatibility, indirect dependency requirements, support status and
security advisories. Keep security fixes visible; centralizing versions does not require a mass update.

A **dependency floor** is the minimum package version a consumer must resolve. **There are no
deliberately floored Fabric packages.** The authoritative floor list is the comment in
`Directory.Packages.props`. Library floors chosen for other target frameworks are not automatically
appropriate for this .NET 10 application and its tests.

Routine compatible dependency updates preserve target frameworks, SDK choices, public behavior,
project boundaries and verification requirements. A deliberate floor requires explicit architectural
approval with evidence for its consumer, required APIs, framework compatibility, indirect dependency
constraints and security status. Approved floors are mirrored only in the outdated-check exclusions;
they never suppress deprecation or vulnerability checks.

### Updating dependencies

1. Review the package release notes, compatibility, dependency requirements and security advisories.
2. Change an existing dependency's version in `Directory.Packages.props`. An authorized new dependency
    also needs a `PackageReference` in each actual consuming project and one central version. Preserve
    reference conditions and asset metadata; do not use overrides to conceal version drift.
3. Restore, build and run the existing server tests. Shared configuration changes also require the
    solution build and tests, including the existing MCP tests as regression checks.
4. Review the resulting package versions and dependency reports. Compare fresh restores under the same
    SDK and settings. .NET 10 can omit packages whose code the runtime already supplies, a behavior
    called **package pruning**; older cached restore information can therefore contain extra packages.

### Package assets

The project files own `PrivateAssets`, `IncludeAssets` and any `ExcludeAssets` on `PackageReference`.
`PrivateAssets` controls what flows to consuming projects; `IncludeAssets` and `ExcludeAssets` control
which package assets are used locally. These settings do not belong on `PackageVersion`.

Both coverlet references retain `PrivateAssets=all` and
`IncludeAssets=runtime; build; native; contentfiles; analyzers; buildtransitive`, without explicit
`ExcludeAssets`. The test project remains `IsPackable=false`. No shared `Directory.Build.targets` is
needed for this metadata.

## Build and test

Run from the repository root with the .NET 10 SDK:

```powershell
dotnet restore Mycelium.Fabric.sln
dotnet build Mycelium.Fabric.sln --configuration Debug --no-restore
dotnet test Mycelium.Fabric.sln --configuration Debug --no-restore --no-build
```

The solution commands include all four projects. See [CLAUDE.md](CLAUDE.md) for additional commands
and [TESTING.md](TESTING.md) for test-authoring conventions.

## Dependency checks

After restore, inspect direct and indirect dependencies with:

```powershell
dotnet list Mycelium.Fabric.sln package --outdated --include-transitive --no-restore
dotnet list Mycelium.Fabric.sln package --deprecated --include-transitive --no-restore
dotnet list Mycelium.Fabric.sln package --vulnerable --include-transitive --no-restore
```

[The dependency workflow](.github/workflows/nuget-reference-check.yml) distinguishes full reports from
the summaries used to synchronize a GitHub issue:

- **Outdated** means a newer version is available. Its full log and issue summary include indirect
    dependencies, with the existing `Microsoft.NETCore.Platforms` and `Microsoft.NETCore.Targets`
    exclusions. These are pre-existing exclusions, not deliberate Fabric floors.
- **Deprecated** means the maintainer has deprecated a package. **Vulnerable** means a known security
    issue is reported. Both checks include indirect dependencies and do not use the outdated exclusions.
- Full reports include test projects. All three issue summaries exclude `*.Tests` project sections, so
    test-only findings remain visible in the reports without themselves triggering an issue.
- Issue synchronization runs outside pull-request events. Local report commands do not write issues.

An outdated result alone does not select an upgrade. Review deprecation and security findings even
when an intentional version policy affects the outdated signal.

For NuGet behavior, see [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management).
