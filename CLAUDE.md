# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Mycelium Fabric** is the Fabric tier of the Mycelium platform — a *concurrent* modelling platform that lets
engineers model a complex system together, applying the **Concurrent Design** methodology on top of the
**SysML v2** language. Fabric is the server side: it holds the model, arbitrates concurrent change, and
exposes it over HTTP.

`Mycelium.Fabric.ConcurrentServer` implements the **REST/HTTP PSM of the OMG Systems Modeling API and
Services 1.0** specification. It consumes the **SysML2.NET SDK** (`C:\Code\SysML2.NET` locally; a sibling
solution) for the SysML v2 metaclass DTOs/POCOs, serializers, and PIM types.

Sibling repositories in the `mycelium-cmbse` org share conventions and CI: `mycelium-sdk`,
`mycelium-forge`, `mycelium-bloom`.

## Build & Test Commands

```bash
# Restore and build the solution
dotnet restore Mycelium.Fabric.sln
dotnet build Mycelium.Fabric.sln

# Run all tests
dotnet test Mycelium.Fabric.sln

# Run a single test by name
dotnet test Mycelium.Fabric.ConcurrentServer.Tests/Mycelium.Fabric.ConcurrentServer.Tests.csproj --filter "FullyQualifiedName~BranchApiTestFixture"

# Run with coverage (as CI does)
dotnet-coverage collect "dotnet test Mycelium.Fabric.sln --no-build" -f xml -o coverage.xml
```

Test framework: **NUnit**. Test classes use `[TestFixture]` and `[Test]`.

**When writing or modifying unit tests** in `Mycelium.Fabric.ConcurrentServer.Tests/`: read `TESTING.md`
at the repo root for the binding NUnit conventions (one `[Test]` per method-under-test, `Assert.That`
everywhere, `Assert.EnterMultipleScope` only for consecutive asserts, mandatory positive + negative
coverage, `Verify{MethodUnderTest}` naming).

> Note on the Windows/`bash` shell: `/p:ContinuousIntegrationBuild=true` is mangled by MSYS path
> conversion locally — use `-p:` when reproducing a CI build by hand. The workflows keep `/p:` because
> they run on `ubuntu-latest`.

## Architecture

### API modules: generated routes + hand-coded handlers

HTTP endpoints are **Carter** modules (`ICarterModule`), split across two partial-class halves of the
same type:

| Half | Location | Authored by |
| --- | --- | --- |
| Route registration — `AddRoutes(IEndpointRouteBuilder)` | `Mycelium.Fabric.ConcurrentServer/Modules/AutoGenModules/<Name>Api.cs` | **Generated** — never edit |
| Request handlers | `Mycelium.Fabric.ConcurrentServer/Modules/<Name>Api.cs` | Hand-coded |

Nine modules — `Branch`, `Commit`, `DiffMerge`, `Element`, `Meta`, `Project`, `Query`, `Relationship`,
`Tag` — registering 35 routes in total.

Generated files carry `[GeneratedCode("Mycelium.SDK", "latest")]` and the banner
`THIS IS AN AUTOMATICALLY GENERATED FILE. ANY MANUAL CHANGES WILL BE OVERWRITTEN!` — **do not edit them**.
To change a route, change the generator in the Mycelium SDK and regenerate.

**Handler contract.** Each route maps onto a hand-written method of the same name on the companion
partial. Handlers take the `HttpContext` and return a non-generic `Task` — they write the response
themselves rather than returning a value for the framework to serialize:

```csharp
public partial class BranchApi
{
    public Task GetBranchesByProject(HttpContext context) { … }
}
```

Handlers are free to resolve services from `context.RequestServices` and read route/query parameters
from `context.Request`.

**Documenting a handler is a specification task, not a naming exercise.** The generated
`.WithName("getBranchesByProject")` operation id and the route template both come from the OMG
Systems Modeling API spec. Ground every handler's XML doc-comment in the spec (see below) and cite the
clause — never infer the semantics from the method name.

### Grounding SysML v2 / KerML / API work with the Hypha plugin

The **Hypha** plugin is the **preferred grounding source for every SysML v2, KerML, and Systems Modeling
API semantic question**. Use it **before** implementing or documenting anything that depends on the
metamodel or on the API specification — do not rely on a sibling analogue, a route name, or prior
knowledge as the source of truth. A plausible prior is exactly what produces confident-but-wrong output.

- **`hypha:spec-citation` — normative specification text (required for API route/handler work).**
  Its knowledge base now covers three documents: KerML 1.0, SysML v2.0, and **Systems Modeling API and
  Services 1.0**. For this repository the API tree is the one that matters:
  - clauses **7.2.1–7.2.6** describe the PIM services — `ProjectService`, `ElementNavigationService`,
    `ProjectDataVersioningService`, `QueryService`, `ExternalRelationshipService`, `ProjectUsageService`;
  - clause **8.1.3** carries the PIM-operation → REST/HTTP endpoint mapping table plus the pagination
    strategy;
  - clauses **7.1.1–7.1.4** define the API model — `Record`, project data versioning (`Project`,
    `Branch`, `Commit`, `Tag`, `DataVersion`, `DataIdentity`), `ExternalData`, and `Query`.
- **`hypha:metamodel-lookup`** — a SysML v2 / KerML metaclass's features, multiplicities, ordering,
  redefinitions, supertypes/subtypes and constraint OCL. Use the `hypha:metamodel-navigator` agent for
  cross-cutting questions spanning many metaclasses.
- **`hypha:sysml-validation`** — validate `.sysml` / `.kerml` textual notation.

Cite spec content by document name and clause — e.g. *"Systems Modeling API and Services 1.0 §8.1.3
(pp. 50–56)"* — never by a file path. This repository does not carry the OMG specification texts.

> **Provenance of the API knowledge tree.** Hypha ships without any `knowledge/spec/` content (OMG
> licensing forbids redistributing the specification text); each tree is generated locally by
> `tools/spec-extract` from the maintainer's own PDF. The `api` tree was generated from
> `3-Systems_Modeling_API_and_Services.pdf` (33 clauses). It lives in the **plugin cache**, so a Hypha
> reinstall or upgrade wipes it — regenerate it if `hypha:spec-citation` reports the API tree missing.
> The durable fix is upstream in `mycelium-hypha`: wire the API document into
> `tools/spec-extract/tests/test_generate.py` and list `knowledge/spec/api/` in
> `skills/spec-citation/SKILL.md`.

**If the Hypha plugin is not installed:** the fallback for API questions is the OMG specification itself,
<https://www.omg.org/spec/SystemsModelingAPI/1.0/PDF>, cited by clause. Tell the user once, in a line or
two, that installing Hypha is recommended for accurate SysML v2 / API work; do not repeat it every task.

### Project layout

```
Mycelium.Fabric.ConcurrentServer            (net10.0, Microsoft.NET.Sdk.Worker)
  ├── Program.cs                            - host entry point
  ├── Modules/                              - hand-coded Carter handler partials
  │   └── AutoGenModules/                   - GENERATED route registration; do not edit
  └── appsettings.json

Mycelium.Fabric.ConcurrentServer.Tests      (net10.0, NUnit + Moq + coverlet)
```

The project uses the **Worker** SDK, not the Web SDK; the `Carter` package pulls the ASP.NET Core
framework reference in transitively, so `ICarterModule`, `IEndpointRouteBuilder` and `StatusCodes`
resolve without switching SDKs.

### Target framework

`net10.0`, `LangVersion` 14.0, across both projects.

## Key Conventions

- **Paths are ALWAYS repo-relative — NEVER absolute.** This applies to every path written into a
  durable artifact: code comments, XML doc `<see cref="…"/>` and prose, error and log messages, commit
  messages, PR bodies, GitHub issue bodies, plan files, skill prompts and agent briefs. Say
  `Mycelium.Fabric.ConcurrentServer/Modules/BranchApi.cs`, NOT
  `C:\CODE\Mycelium\mycelium-fabric\Mycelium.Fabric.ConcurrentServer\Modules\BranchApi.cs` and NOT
  `/c/CODE/Mycelium/...`. Use forward slashes. Reason: absolute paths are user- and machine-specific;
  they leak the local filesystem into the repo, break for every other contributor, and go stale on
  rename. The ONLY exception is the `Read` / `Edit` / `Write` tool `file_path` parameter, which the tool
  requires to be absolute — those arguments are not user-visible artifacts.
- **File headers**: every `.cs` file opens with the Starion copyright block carrying
  `SPDX-License-Identifier: Apache-2.0`, as configured in `Mycelium.Fabric.sln.DotSettings`. Copy the
  form used by `Mycelium.Fabric.ConcurrentServer/Program.cs`.
- **`using` directives go inside the namespace**, not above it — matching `Program.cs` and the generated
  modules.
- **Commit messages**: `Fix #<n> : <description>` (e.g. `Fix #1 : Solution scaffolding`,
  `Fix #2 : Core github actions`). The `Fix #<n>` prefix makes GitHub auto-close the issue on merge.
- **Branches**: default/integration branch is `development`; `main` is downstream only. **All feature
  work targets `development`** via PR. Feature branches are named `<issue-number>-<slug>`, e.g.
  `4-feature-code-generation-of-api-routes`.
- **CI**: GitHub Actions — `CodeQuality.yml` (build, test, SonarCloud), `codeql-analysis.yml`,
  `nuget-reference-check.yml`, `add-to-project.yml`. Actions are pinned to the **major tag only**
  (`@v7`, `@v6`, `@v2`).
- **License**: Apache 2.0.

## Branch & PR workflow (MANDATORY)

Direct pushes to `development` or `main` are forbidden. All work lives on a feature branch.

**Agent boundaries are strict and minimal:**

1. The agent **must NOT auto-commit, EVER.** `git commit` is the user's responsibility — no exceptions,
   no asking, no "for convenience". The user reviews `git diff` and commits manually.
2. The agent **must NOT push commits, open PRs, or merge by default.** Push + PR + merge are the user's
   job too. The agent performs them only if the user explicitly asks in-conversation; otherwise it stays
   out of git remote operations entirely.
3. **When the agent creates a branch**, it must create it locally with
   `git switch -c <branch> origin/development` AND **immediately push the empty branch** with
   `git push -u origin <branch>`, so the remote ref exists at the same commit as `origin/development` and
   the user's later push is a trivial fast-forward. This is the only push the agent performs by default,
   and it is safe: the branch tip equals `origin/development`'s tip — no new commits, no force flags.
4. **At the end of any task that creates a branch**, the agent stops with a final summary containing the
   in-scope files modified, the test counts, a **pre-filled commit message** (`Fix #<n> : <description>` —
   single line, no body, no `Co-Authored-By` trailer, no "🤖 Generated with …" footer), and a handoff
   line telling the user how to stage + commit + push themselves. Example:
   > Review `git diff`, stage the in-scope files (`git add <path> …` — NEVER `-A` / `.`), commit with the
   > message above, then `git push`. Open the PR yourself via the GitHub UI or
   > `gh pr create --base development`.

   This is the end of the agent's involvement. **The agent does NOT proceed to push the commit and does
   NOT open the PR** unless the user explicitly asks.

**If the user does explicitly ask the agent to push or open the PR** (user-initiated only):
- Verify the current branch is not `development`/`main`, `git log -1` matches the canonical form, and
  `git status --porcelain` is empty.
- Then `git push origin <branch>` — NEVER `--force`, NEVER `--force-with-lease`, NEVER `--no-verify`.
- Then `gh pr create --base development --head <branch> …` — NEVER `--base main`, NEVER `--draft` unless
  asked.

**Failure modes:**
- `git push -u origin <branch>` fails because the branch already exists on origin → abort, surface to the
  user, do not force.
- Branch creation requested but the current branch is `development` or `main` AND the user asked for
  in-place work → REFUSE. Feature work must live on a feature branch first.

**Why this split**: the user is the reviewer of record. The commit is the review and the push is the
delivery — both are the user's calls.

## Quality rules

- Prefer comparing `Count` to 0 rather than using `Any()`, both for clarity and for performance.
- Use `StringBuilder.Append(char)` instead of `StringBuilder.Append(string)` when the input is a constant
  unit string.
- Prefer `string.IsNullOrWhiteSpace` over `string.IsNullOrEmpty` when checking the non-nullable value of
  a string.
- Prefer switch expressions/statements over if-else chains when applicable.
- **Prefer LINQ as much as possible** — projection / filter / aggregation over collections
  (`items.Where(…).Select(…).ToList()`, `result.AddRange(items.Select(…))`) instead of hand-rolled
  `foreach` + `if` + `.Add()` loops. The ONE exception is straightforward positional or range access on a
  concrete `List`/array: `list[^1]` beats `list.Last()`, `array[1..^1]` beats
  `array.Skip(1).SkipLast(1)` — indexer/range syntax is more performant there.
- **Flatten a `foreach` with a leading-`if` filter by pushing the predicate into a `.Where(…)` clause on
  the iterated source.** Write `foreach (var x in xs.Where(x => predicate))` instead of
  `foreach (var x in xs) { if (!predicate) { continue; } … }`. Same for `.OfType<T>()` instead of a
  runtime `is`-check + cast. The body should be the action, not the guard. Exceptions: (a) the predicate
  has observable side-effects and iteration order must be preserved; (b) the predicate is too long to
  read inline — extract it to a named local function and still call it from the `.Where(…)`.
- Prefer C# **collection expressions** (`[a, b, c]`, `[..xs]`, `[]`) over `new[] { … }`,
  `new List<T> { … }`, `new T[] { … }`. Applies to production code AND tests (`return [];` not
  `return new List<T>();`). Fall back to explicit construction only when type inference cannot pick the
  right collection type.
- Use **meaningful variable names** instead of single-letter names in any context (`charIndex` not `i`,
  `currentChar` not `c`, `element` not `e`).
- Use **`NotSupportedException`** (not `NotImplementedException`) for placeholder/stub methods that
  require manual implementation. *(This governs C# stubs. It does not govern the HTTP surface: an
  unimplemented endpoint answers `500` with a JSON payload, it does not throw.)*
- Prefer C# **property patterns** (`x is IType { Prop: value }`) over the declared-variable-plus-predicate
  form (`x is IType name && name.Prop == value`) when the narrowed variable is only consulted once.
- **Always use C# auto-properties** (`public T Foo { get; private set; }`, `{ get; init; }`, `{ get; }`) —
  NEVER pair a private backing field with a full-getter property when there is no non-trivial logic
  (validation, normalisation, lazy init, event firing). Mere storage is never a justification for a
  backing field.
- **Prefer method-group syntax over a lambda when the lambda merely invokes a method.** Write
  `Assert.That(subject.Handle, Throws.TypeOf<X>())` rather than
  `Assert.That(() => subject.Handle(), …)`; pass `string.IsNullOrWhiteSpace` rather than
  `s => string.IsNullOrWhiteSpace(s)`. Fall back to a lambda only when the body does more than the bare
  call, the target is overloaded and cannot be inferred, or explicit type arguments are needed.
- **Surround every braced block** (`if`, `else if`, `while`, `for`, `foreach`, `switch`, `using`,
  `try`/`catch`/`finally`, `lock`, `do…while`, anonymous `{ }`) with a blank line on both sides. The rule
  does NOT apply at the very start/end of a method body, nor between a `}` and a continuation keyword
  (`else`, `catch`, `finally`, `while` of `do…while`) that belongs to the same control flow.
