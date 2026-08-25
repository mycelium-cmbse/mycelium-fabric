# TESTING.md — Unit-test conventions for Mycelium Fabric

> **When to read this file:** load it whenever you are writing or modifying unit tests in
> `Mycelium.Fabric.ConcurrentServer.Tests/`. It is the authoritative companion to `CLAUDE.md` for
> everything test-related. Outside of test authoring, you do not need it.

These conventions are carried over from the sibling `SysML2.NET` solution, where they were distilled from
explicit author-review feedback. Diverging from them produces churn and PR back-and-forth, so treat them
as binding.

---

## 1. Test framework

- **Framework:** NUnit (with **Moq** for test doubles, `coverlet` for coverage)
- **Fixture attribute:** `[TestFixture]`
- **Test attribute:** `[Test]`
- **Project layout:** mirror the production namespace under the test project — e.g.
  `Mycelium.Fabric.ConcurrentServer/Modules/BranchApi.cs` →
  `Mycelium.Fabric.ConcurrentServer.Tests/Modules/BranchApiTestFixture.cs`.
- **Fixture naming:** `{TypeUnderTest}TestFixture`.

---

## 2. Test doubles — MOCK, do not STUB

**This is a hard preference and it is not negotiable: use a mock, never a stub.**

When a test needs a collaborator, create it with **Moq** and assert the interaction. Do not hand-write a
class that implements the interface just to return canned values, and do not reach for a no-op
implementation such as `NullLogger<T>.Instance`.

**Why:** a mock states the interaction the code under test is expected to have, and *fails* when that
interaction does not happen or happens with the wrong arguments. A stub only supplies a value so the code
does not crash — it passes whether or not the behaviour is correct. Tests exist to assert behaviour, not
to satisfy the compiler.

**Do:**

```csharp
var logger = new Mock<ILogger<BranchApi>>();

await BranchApi.GetBranchesByProject(context, projectId, logger.Object);

logger.Verify(
    log => log.Log(
        LogLevel.Information,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("getBranchesByProject")),
        null,
        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
    Times.Once);
```

**Don't:**

```csharp
BranchApi.GetBranchesByProject(context, projectId, NullLogger<BranchApi>.Instance);   // no-op double
```

```csharp
private sealed class FakeBranchService : IBranchService   // hand-rolled stub
{
    public Task<IEnumerable<Branch>> GetBranchesAsync(Guid projectId) => Task.FromResult(branches);
}
```

Naming follows the rule: **do not name test types `Stub*`** — name them for what they are
(`Mock<T>` instances need no name; genuine infrastructure gets a descriptive one).

**The one carve-out — test infrastructure that must really work.** A type whose *own behaviour* is the
point is not a stub, and Moq is the wrong tool for it. `TestHelpers/TestEndpointRouteBuilder.cs` is the
canonical example: it must genuinely collect `EndpointDataSource` entries so `AddRoutes` can be
exercised, so it is a real (if minimal) implementation, not a canned-value double. Use judgement, and
prefer a mock whenever the double exists only to be *passed in*.

---

## 3. One `[Test]` per method-under-test (default)

**Default to a single `[Test]` method per method-under-test** and pack every scenario — happy path, edge
cases, null guards, alternate inputs — into multiple `Assert.That` calls inside that one test.

**Why this is the default:** it keeps the test list compact, removes duplicated arrange boilerplate, and
makes the intent obvious — one method under test has one combined coverage test.

### When separated `[Test]` methods ARE appropriate

The combined form is the default, **not absolute**. Split into separate `[Test]` methods when **each
scenario has a genuinely distinct, complex setup** that would tangle if packed into one test — a
different mock configuration per scenario, a multi-step request pipeline that exists for only one case,
or a malformed-request shape needing its own local fixtures.

Rule of thumb: if you find yourself naming locals `context1` / `context2` / `handler1` / `handler2`, or
writing more than ~3 lines of arrange between asserts, that scenario probably wants its own `[Test]`.

---

## 4. Cover positive AND negative cases in the same test

Each `[Test]` MUST exercise both directions of the method-under-test. Build up state incrementally and
assert the delta after each arrange step.

**Negative cases (always include where applicable):**

| Negative scenario | Assertion |
| --- | --- |
| Null argument | `Assert.That(() => …, Throws.TypeOf<ArgumentNullException>())` |
| Empty / unpopulated result | `Assert.That(result, Has.Count.EqualTo(0))` |
| Wrong-target scenario | populated subject whose contents don't match the filter — assert `Has.Count.EqualTo(0)` |
| Unimplemented handler | assert the HTTP contract it does serve — see §9 |

**Positive cases:** populated subject, multiple elements with verified ordering, dedup semantics where the
specification says so.

---

## 5. Always use `Assert.That` — no legacy forms

Every assertion (including exception assertions) MUST use the fluent `Assert.That(…)` form.

| Don't write | Write instead |
| --- | --- |
| `Assert.Throws<T>(…)` | `Assert.That(() => …, Throws.TypeOf<T>())` |
| `Assert.IsTrue(x)` | `Assert.That(x, Is.True)` |
| `Assert.IsFalse(x)` | `Assert.That(x, Is.False)` |
| `Assert.AreEqual(a, b)` | `Assert.That(b, Is.EqualTo(a))` |
| `Assert.IsNull(x)` | `Assert.That(x, Is.Null)` |
| `Assert.IsNotNull(x)` | `Assert.That(x, Is.Not.Null)` |

**Exception-message assertions** are a single fluent chain — no scope wrapper:

```csharp
Assert.That(() => parser.Parse(input),
    Throws.TypeOf<FormatException>().With.Message.Contains("unexpected token"));
```

Moq's own `Verify` calls are the exception to the `Assert.That` rule — `mock.Verify(…, Times.Once)` is
the idiomatic form and needs no wrapper.

---

## 6. `Assert.EnterMultipleScope` — only for consecutive asserts

Use `using (Assert.EnterMultipleScope()) { … }` **only** when **two or more consecutive** `Assert.That`
calls follow each other and you want all of them evaluated even if earlier ones fail.

**Do:**

```csharp
using (Assert.EnterMultipleScope())
{
    Assert.That(invocation.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    Assert.That(invocation.ContentType, Does.Contain("application/problem+json"));
}
```

**Don't** wrap a single fluent assertion in a scope — even a long chain like
`Throws.TypeOf<T>().With.Message.Contains("…")`. **Don't** put an early standalone null-guard
`Assert.That` inside a scope that ends before the next assert.

---

## 7. Test naming

**Combined-form tests (the default — §3):**

- One test method = `public void Verify{MethodUnderTest}()` — e.g. `VerifyGetBranchesByProject`,
  `VerifyAddRoutes`.
- Do **not** suffix with scenario names — scenarios live inside the body.

**Separated-form tests (the §3 exception):**

- Use `{MethodUnderTest}_{ScenarioDescription}_{ExpectedOutcome}` — e.g.
  `GetBranchesByProject_WithUnknownProject_Returns404`. Each method should be self-explanatory from its
  name alone.

---

## 8. Arrange / Act / Assert inside a combined test

- Separate arrange / act / assert blocks with **blank lines**.
- Add short inline comments only when the delta from the previous step is non-obvious.
- Re-call the method-under-test after each new arrange step so each `Assert.That` describes one increment
  of state.
- Reuse the same locals (`context`, `module`, `result`) across the test — don't fork into `context1` /
  `context2` unless you really need two independent subjects.

---

## 9. Scope discipline — assert, don't fix

If a test crosses into a method that is not implemented yet, **assert its current behaviour** — do **not**
implement it to make the test pass.

For a C# placeholder that throws:

```csharp
Assert.That(() => subject.SomeMember(), Throws.TypeOf<NotSupportedException>());
```

For an **API handler that is not yet implemented**, the contract is an HTTP response, not an exception —
assert the `500` and the RFC 7807 body rather than a throw:

```csharp
var invocation = await HandlerInvocation.CaptureAsync(
    context => BranchApi.GetBranchesByProject(context, projectId, logger.Object));

using (Assert.EnterMultipleScope())
{
    Assert.That(invocation.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    Assert.That(invocation.Problem.Detail, Does.Contain("getBranchesByProject"));
}
```

When the handler is implemented later (in its own scoped change), the assertion is replaced with the real
positive case in the same `[Test]`.

This is the testing-side companion to the broader scope-discipline rule: a task scoped to file X must not
silently grow to also modify file Y, even if file Y is "one line away from working".

---

## 10. Assertion idiom preferences

| Concern | Prefer | Avoid |
| --- | --- | --- |
| Collection count | `Has.Count.EqualTo(n)` | `result.Count, Is.EqualTo(n)` |
| Reference equality | `Is.SameAs(expected)` | `Is.EqualTo(expected)` (relies on `Equals`) |
| Order-irrelevant collection equality | `Is.EquivalentTo([…])` | `Is.EqualTo([…])` (forces order) |
| Order-relevant collection equality | `Is.EqualTo([…])` | hand-rolled `for` loops |
| First / last element | `result[0]` / `result[^1]` (indexer) | `result.First()` / `result.Last()` |
| Range / slice | `result[1..^1]` | `result.Skip(1).Take(n)` |
| String empty/null | `Is.Null.Or.Empty` | `string.IsNullOrEmpty(x), Is.True` |
| Substring | `Does.Contain("…")` or `Contains.Substring("…")` | `x.Contains("…"), Is.True` |

The indexer preference aligns with the project-wide quality rule in `CLAUDE.md`.

---

## 11. Testing Carter modules

- **Route registration** is generated code. `AddRoutes` is worth a regression guard — assert the route
  templates, endpoint names and declared response codes — but never edit `Modules/AutoGenModules/` to
  make such a test pass. Use `TestHelpers/TestEndpointRouteBuilder.cs` to collect the endpoints without a
  web host.
- **Declared response codes** come from the `[ProducesResponseType]` attributes on the handler. Note that
  `Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute` implements `IApiResponseMetadataProvider`, not
  `IProducesResponseTypeMetadata` — query the concrete attribute type when reading endpoint metadata.
- **Handlers** are `static`, take an `HttpContext` plus the route's guid parameters plus an
  `ILogger<TModule>`, and return `Task`. Call them directly, passing a `Mock<ILogger<TModule>>` (§2). Use
  a `DefaultHttpContext` with a seekable `MemoryStream` assigned to `Response.Body` so the written payload
  can be read back and asserted.
- Prefer asserting the **observable HTTP contract** — status code, content type, response body — over
  interactions with internal collaborators, except for the logging call, which is asserted through the
  logger mock.

---

## 12. Anti-pattern checklist (what NOT to do)

- ❌ **Hand-rolled stubs, fakes, or `Null*` no-op doubles where a `Mock<T>` belongs (§2).**
- ❌ Naming a test type `Stub*`.
- ❌ Splitting one method-under-test into many `…_WhenX_DoesY` tests when the scenarios share setup.
- ❌ `Assert.Throws<T>` / `Assert.IsTrue` / `Assert.AreEqual` / `Assert.IsNull` (any legacy NUnit API).
- ❌ Wrapping a single `Assert.That` inside `Assert.EnterMultipleScope`.
- ❌ Suffixing the combined-form `Verify{MethodUnderTest}` name with a scenario.
- ❌ Implementing an out-of-scope placeholder from within a test fixture change.
- ❌ Asserting only the positive case (or only the negative case).
- ❌ `result.First()` / `result.Last()` / `result.Count() == 0` where indexer / `Has.Count.EqualTo` works.
- ❌ Editing anything under `Modules/AutoGenModules/` to make a test pass.
