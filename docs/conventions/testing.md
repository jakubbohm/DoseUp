# DoseUp — Testing conventions

**Status:** conventions **binding**; concrete APIs/snippets directional until the first real tests exist (M0 calibrates) · **Decided:** design interview 2026-07-14/15 (this document is the outcome) · **Stack:** fixed in [ADR-0003-testing-stack](../adr/0003-testing-stack.md) (TUnit + Shouldly · Aspire-only integration harness · ArchUnitNET · @playwright/test) — this document is about **organisation**: where tests live, what each layer is for, and the conventions that make test placement a lookup, not a judgment call.

## 1. Test solution layout

```
DoseUp.slnx
├─ src/
│  ├─ DoseUp.AppHost/            local orchestration (net10)
│  ├─ DoseUp.ServiceDefaults/
│  ├─ DoseUp.Api/                the modular monolith (arrives with the shared-kernel change)
│  └─ web/                       React PWA; e2e/ lives inside it (ADR-0003)
└─ tests/
   ├─ DoseUp.UnitTests/          pure, in-memory, no I/O — Domain + SharedKernel
   ├─ DoseUp.ArchitectureTests/  ArchUnitNET rules, each quoting the ADR line it enforces
   └─ DoseUp.IntegrationTests/   Aspire harness — slice tests over HTTP + persistence semantics
```

- **Per-layer projects, not per-module.** The API is one project, so tests mirror modules as **folders/namespaces**, never as projects — per-module test projects would multiply projects for zero isolation benefit. Growth rule (same spirit as the SharedKernel one): split a test project only when build/run time actually forces it, and record the split here.
- **Project names say the layer, not the target.** `DoseUp.UnitTests`, not `DoseUp.Domain.Tests` — the unit project also covers SharedKernel primitives (`RuleSet` semantics get exhaustive unit tests), so layer-naming is the honest one.
- **Mirroring is mechanical in both directions.** A test file sits at the same folder path as its target with the project root swapped: `DoseUp.Api/Modules/Scheduling/Domain/Schedule.cs` ↔ `DoseUp.UnitTests/Modules/Scheduling/Domain/ScheduleTests.cs`; namespaces follow folders. Finding the tests for a type — or the type for a test — is path substitution, no search required.
- **One entry point; layers selected by project.** `dotnet test DoseUp.slnx` runs everything. CI selects layers by *project*, never by category attributes — there is no `[Category("Integration")]` bookkeeping to forget. (The smoke-vs-full split for E2E is a Playwright concern in `web/e2e`, outside the .NET solution.)
- Test projects ride `net11.0` + `LangVersion preview` like the service projects (CLAUDE.md). Package/version mechanics (`Directory.Packages.props`, TUnit/Shouldly/ArchUnitNET references) land with the shared-kernel change.

## 2. Placement — "I wrote X, where does its test go?"

Three principles decide placement; the table is their lookup form. When the principles and the table ever disagree, the principles win — fix the table.

**P1 — Test behavior at the outermost boundary that owns it; go lower only under combinatorial pressure.** The unit layer absorbs combinatorial explosion (domain rules, edge cases, DST math — hundreds of cheap cases). The integration layer proves each slice's wiring, persistence semantics, and wire contract — once per path, not per permutation. E2E proves user journeys — a handful. The pyramid is a decision rule here, not a shape metaphor. Corollary: **a slice with no interesting domain logic gets no unit tests** — its slice test is the complete story; unit-testing a thin handler only restates the implementation.

**P2 — Feature handlers are tested against a real Postgres or not at all.** The module's `DbContext` is the data-access API by design ([ADR-0002-architecture-style](../adr/0002-architecture-style.md), no repositories), so there is no honest seam to fake. Named bans:

- **EF InMemory provider — banned.** No relational semantics: the set-rule **constraint backstop can never fire** on it, so a green handler test would be lying about the very mechanism the domain-rules model relies on.
- **SQLite-in-memory — banned.** Same lie in milder form (constraint, `uuid`, and concurrency behavior diverge from Npgsql).
- **Mocking `DbContext` — banned.** Asserts implementation, not behavior.
- **Ports over data access for testability — already rejected** by the anticipatory-abstraction rule ([conventions/README.md § Persistence](README.md)).

This is precisely why ADR-0003 accepted the Aspire harness's full-graph startup cost. Handler tests live at integration altitude; the unit layer stays honestly pure.

**P3 — Prefer the real collaborator at a higher altitude over a double at a lower one.** Every test double is a second implementation of the dependency that nobody keeps honest — each one narrows what the test can catch. An "overly mocked unit test" is therefore not a cheaper integration test; it is a weaker one. A double must be justified by *"the real thing cannot run in this test"*. Standing justifications: **time** (`FakeTimeProvider` — determinism, and Microsoft owns the fake) and the **external IdP trust anchor** (§3 test identity — Entra cannot run in a test). Anything else needs its justification stated in the PR. Shorthand: **fake only what you don't own** — DoseUp owns everything in the test loop except Entra's signature and the wall clock. Corollary: **no mocking framework by default** — the few legitimate doubles are hand-rolled fakes of SharedKernel ports, which can assert domain-meaningful things; revisit only if the port count genuinely grows.

| You wrote… | Its test goes… | Form |
|---|---|---|
| Aggregate method, affordance (`CanXxx`), domain service, VO, SmartEnum | UnitTests | pure, exhaustive — the pyramid's base |
| SharedKernel primitive (`Result`, `RuleCheck`/`RuleSet`, base types, id-converter logic) | UnitTests | exhaustive; these are load-bearing |
| FluentValidation validator | UnitTests (`FluentValidation.TestHelper`) | exhaustive rules; the slice test asserts only that the 400 channel is wired (one bad-request case per endpoint) |
| Feature handler | IntegrationTests — **slice test over HTTP** | happy path + each `Result` branch worth the wire: 404 scoping, 409 with exact violation codes |
| RuleSet composition incl. constraint backstop | IntegrationTests | provoke the race path: the backstop violation maps to the *same* code as the advisory check |
| Wolverine consumer | IntegrationTests | deliver the message, assert effect **and** idempotency (deliver twice → once-effect) |
| Published-language translator | mapping = UnitTests (pure); outbox enrollment = IntegrationTests | |
| Domain-event dispatcher, PD mapper | UnitTests (loop/depth-guard, mapping table); in-UoW dispatch semantics + wire shape = one integration test each | |
| EF configuration / migration | implicit via harness startup + one `HasPendingModelChanges` guard test per module context | |
| Endpoint auth/status wiring | the authZ matrix (§4) + slice tests | |
| UI journey | `web/e2e` (Playwright) | few, journey-level; never API permutations |

### Slice tests: through HTTP, not handler-direct

The endpoints are deliberately thin, so an HTTP-level test *is* the handler test plus the adapter, the auth rings, and the ProblemDetails contract — for one extra hop. Handler-direct DI resolution is the **exception**, used only where HTTP cannot reach the scenario (consumer paths, outbox behavior); then the test calls the handler exactly as the Wolverine consumer would.

### Contract testing: what we have is the contract suite

- **Artifact freshness** is CI-gated by the contract pipeline ([conventions/README.md § API conventions](README.md)): code → `openapi.json` → TS types, drift fails the build. This guarantees the TS client matches what the code *declares*.
- **Declaration vs runtime** is covered by slice tests asserting the wire shapes that matter: status codes, ProblemDetails shapes, stable violation codes.
- **Module contracts** (`<Module>.Contracts`) are compiler-checked inside the single repo.
- **Consumer-driven contract tooling (Pact et al.) — rejected.** It buys nothing until a second deployable or an external consumer exists; that event reopens this decision. If a runtime-vs-spec mismatch ever bites despite green gates (a FastEndpoints spec misdeclaration), the recorded escalation is response-schema validation against `openapi.json` inside slice tests — not adopted in advance.

## 3. Integration harness

Theme: **fake nothing except the two things that cannot run in a test** — Entra's signature and wall-clock control. Everything else in the harness is the production code path.

### 3a. Lifecycle: one running AppHost per test session

A TUnit fixture (`[ClassDataSource<AspireAppFixture>(Shared = SharedType.PerTestSession)]`, implementing `IAsyncInitializer`/`IAsyncDisposable`) starts the AppHost once via `DistributedApplicationTestingBuilder`, waits for resource health, applies migrations the same way local dev does (**the real schema path — never `EnsureCreated`**; migrations are themselves a deployed artifact — [ADR-0004-delivery-and-process](../adr/0004-delivery-and-process.md)), and hands out HTTP clients per caller identity. ADR-0003 accepted full-graph startup cost; paying it once per session is what makes that acceptance cheap. Non-API resources a slice test doesn't need (the web frontend) are excluded in test mode — mechanism decided at implementation.

### 3b. DB isolation by construction — no cleanup

TUnit runs tests in parallel; isolation comes from **data, not cleanup**. Each test mints its own account + profile — ring 2 of [ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md) makes cross-account data mutually invisible, so the same mechanism that isolates production tenants isolates parallel tests. The session database accumulates rows; nobody cares. Supporting conventions:

- Global-uniqueness set rules use unique test data (Guid-suffixed names).
- Assertions on admin/global collections are **containment, never equality** ("contains mine", never "equals exactly these").
- Respawn is the recorded fallback if a genuine global-state test class ever appears — it would run serialized, as the exception.

### 3c. Test identity: real JWT pipeline, test-only trust anchor

The fixture mints self-signed JWTs with a test signing key; the harness injects a second accepted authority into the API's bearer configuration (the testing builder mutates the app model before start). Bearer middleware, `oid` claim mapping, `ActiveAccount` policy, `CallerContext` resolution, 401/403 semantics — all production code. **Caller classes are (token, seeded-account-state) pairs**: anonymous = no token · member-owner / member-other = two seeded accounts · admin = the DB flag · revoked = valid token + revoked row, which exercises [ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md)'s "revocation bites on the next request" exactly as specced. Rejected: a custom test auth scheme (fakes the middleware — P3 violation); real-Entra ROPC test users (external dependency, secrets, throttling in CI — the only coverage it adds is Entra itself, which the E2E login journey against the deployed environment covers).

### 3d. Async seam: real transports via emulators

The harness runs the **ASB emulator** (`RunAsEmulator()`) and **Azurite** for Storage Queue alarms: real Wolverine over AMQP, real outbox, real visibility-timeout semantics. Environment split for topology:

- **Dev/test only:** Wolverine `AutoProvision()` creates missing entities at startup (it is opt-in config, not default behavior).
- **Production:** all queue topology is **created by CD from Bicep** ([ADR-0004-delivery-and-process](../adr/0004-delivery-and-process.md) — Azure is defined only by `infra/`); the runtime's managed identity holds **data-plane roles only** (Send/Receive), no control-plane rights, and Wolverine runs with auto-provisioning off **and `SystemQueuesAreEnabled(false)`**. System queues are per-node queues (`wolverine.response.<node>` …) Wolverine otherwise auto-creates at startup for **bus request-reply and node-coordination signaling — neither of which DoseUp uses**: the sync path is in-proc feature handlers (request-reply over the bus would be the mediator-over-the-network antipattern), and the async seam is one-way events + alarms, with node bookkeeping living in the Postgres-backed message store. Left enabled, startup under a data-plane-only identity fails trying to create them. The deployed smoke verifies clean startup + outbox recovery under the restricted identity.

Caveat carried by the M0 spike: Wolverine provisions via control-plane calls, and the ASB emulator only gained native .NET admin-client support in v2.0.0 (2026-01). The M0 ASB spike ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md) messaging decision) **extends to "Wolverine × ASB emulator inside the Aspire harness"**; recorded fallback: Wolverine's stub transport for slice tests + the deployed-environment smoke covering real ASB.

### 3e. Seeding & time

- **Arrange through the front door** (API calls) by default — it dogfoods the contract and exercises the whole write path. Direct `DbContext` arranging (connection string from the harness) is the exception for states the API deliberately cannot create (bootstrap account, backdated rows), and the test states why.
- **No shared seed catalog for tests** — implicit shared data rots into mystery-guest tests. The `UseSeeding` tier ([conventions/README.md § Persistence](README.md)) serves local dev UX, not the suite.
- **Integration tests take real time as-is.** `FakeTimeProvider` is process-local and the API runs in its own process. All time-travel testing (DST, recurrence, due-ness) lives at unit altitude — the `now`-as-parameter design ([conventions/README.md § SharedKernel discipline](README.md)) exists exactly for this. If a slice ever truly needs frozen time, the recorded mechanism is a config-injected fixed `TimeProvider` in Platform; deferred until a real need exists.

## 4. The authorization matrix (mechanics — behavior specced in ADR-0002 § Authorization)

- **Catalog by reflection over the API assembly**: every FastEndpoints endpoint class is enumerated — code cannot hide from reflection. (`openapi.json` rejected as the census: a swagger-excluded endpoint would silently escape the matrix. It remains the contract artifact only.) The ASP.NET health probes (`/health`, `/alive`) are not FastEndpoints endpoints — they enter the matrix as **manual `AnonymousAllowed` rows** outside the reflection census, and the completeness gate diffs the FE census only (confirmed at c001).
- **Classification = the endpoint's *kind* + one arrange recipe.** The expected-status vector is derived from the kind per the rings of [ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md) — never hand-written per endpoint:

  | Kind | anonymous | revoked | member-other | non-admin | member-owner |
  |---|---|---|---|---|---|
  | `AnonymousAllowed` (health, …) | 2xx | — | — | — | — |
  | `ProfileScoped` (the default) | 401 | 403 | **404** | n/a | 2xx |
  | `AdminOnly` | 401 | 403 | n/a | **403** | n/a |

  The arrange recipe (a small delegate creating the probe target under account A and returning route args) is the only bespoke part — writing it *is* the deliberate act of classification. Member-owner asserts 2xx-class only; deep happy-path behavior belongs to slice tests.
- **Completeness gate:** one test diffs the reflection catalog against the classification table and fails on any unclassified endpoint — ADR-0002's "new endpoints fail the matrix until classified", made mechanical.
- **One test case per (endpoint × caller class)** via a TUnit data source — a failure reads as `Schedules_Update × member-other: expected 404, got 200`: the exact leak, named.
- Caller identities come from the §3c fixtures (token + seeded account state).
- **Placement & cadence:** `DoseUp.IntegrationTests/Authorization/`, every PR (N endpoints × ≤4 probes = cheap HTTP against the shared harness). First rows land in M0 with the first endpoints; M3 `harden-authz` completes the catalog and adds payload-embedded foreign-id probes. Structural rules (anonymous allowlist, admin endpoints ∈ admin group) belong to ArchUnitNET (§5).

## 5. Architecture tests — the catalog

Two framing conventions govern the whole catalog:

- **Convention 1 — the doc is the rule; the test quotes it.** Every arch test names/cites the exact source line it enforces (ADR-0002 dependency rule #N, a conventions section). Tests mirror written rules; they never invent them (software-factory F-28).
- **Convention 2 — single enforcement owner per rule.** Each rule is enforced by exactly one mechanism — ArchUnitNET, an analyzer, the authZ matrix, or a CI gate — recorded here. Double enforcement is drift waiting to happen.

| # | Rule (source) | Owner |
|---|---|---|
| 1 | Domain references only SharedKernel — never Features/Infrastructure/Platform/FastEndpoints/Wolverine/EF/Npgsql (ADR-0002 rule 1) | ArchUnitNET |
| 2 | Features orchestrate only their own module's Domain through its ports (rule 2) | ArchUnitNET |
| 3 | Cross-module = public contracts + integration events only (rule 3) | ArchUnitNET |
| 4 | The module's `DbContext` is consumed only by its own Features (the repository-free carve-out); every other Infrastructure type is seen only by the composition root, or by nothing (rule 4, revised 2026-07-15) | ArchUnitNET |
| 5 | SharedKernel references nothing project-internal (rule 5) | ArchUnitNET |
| 6 | Feature handlers + validators reference no FastEndpoints/ASP.NET types (rule 6 — the mechanically testable proxy for "endpoints carry no use-case logic") | ArchUnitNET |
| 7 | Slice independence: use-case slice namespaces never reference sibling slices; sharing moves down (Domain) or up (module shared) ([ADR-0002-architecture-style](../adr/0002-architecture-style.md) rule 7) | ArchUnitNET |
| 8 | No C# `enum` in Domain namespaces — SmartEnum only; the plain-enum DTO carve-out lives in Features ([conventions/README.md § Domain enumerations](README.md)) | ArchUnitNET |
| 9 | Only per-module published-language translators call `IIntegrationEventPublisher` ([ADR-0002-architecture-style § Events](../adr/0002-architecture-style.md)) | ArchUnitNET |
| 10 | For every `DbContext` discovered in the API assembly, every mapped entity type implements `IAggregateRoot` ([conventions/README.md § Domain model base types](README.md)) | reflection over the offline-built EF models (no DB), in ArchitectureTests |
| 11 | Colocation: one use case's endpoint + handler + validator + DTOs share one namespace (ADR-0002 slice anatomy) | reflection + TUnit data source — ArchUnitNET cannot express relational-namespace rules |
| 12 | `AllowAnonymous` = explicit allowlist ([ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md)) — **the allowlist is the test's data**: adding an anonymous endpoint means editing the list in the same PR | ArchUnitNET |
| 13 | Admin endpoints ∈ the `AdminOnly` group ([ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md)) | ArchUnitNET |
| 14 | Naming: feature handlers end `Handler`, validators end `Validator` (+ the reverse: `*Validator`-named classes reside in Features namespaces — catches misplacement), endpoints end `Endpoint` | ArchUnitNET — **activates when §6 fixes the naming conventions** |
| 15 | Time APIs (`DateTime(Offset).Now/UtcNow`) banned outside Platform ([conventions/README.md § SharedKernel discipline](README.md)) | **BannedApiAnalyzers** — owner of record; ArchUnitNET never duplicates it |
| 16 | EF InMemory/SQLite providers never referenced (§2 P2) | **Convention + review** (absent from `Directory.Packages.props`); an arch test only if it ever actually slips — honest owner, not fake mechanization |
| 17 | A module's context maps only its own module's Domain types (ADR-0002 § Persistence is module property, 2026-07-15) | reflection over the offline-built EF models (no DB), in ArchitectureTests |
| 18 | A module's context is consumed only by its own module and the composition root (ADR-0002 § Persistence is module property) | ArchUnitNET |
| 19 | Every mapped entity sits in the module's schema (ADR-0002 § Persistence is module property) | reflection over the offline-built EF models (no DB), in ArchitectureTests |

All ArchUnitNET rules **ship with the shared-kernel change and pass vacuously** until their targets exist — no phased introduction to forget.

**Rejected: visibility tests (`internal` handlers/configurations).** In a single-assembly modular monolith `internal` is powerless between modules — every module sees every other's internals. The boundary work is done by namespace rules 2/3/7; blanket-`internal` would add `InternalsVisibleTo` ceremony for test projects with zero enforcement gain. (Real rule in multi-project layouts; theater here.)

**Deliberate deviation from the classic guard set:** there is **no** "Features must not reference EF" rule — feature handlers are EF-aware *by design* ([ADR-0002-architecture-style](../adr/0002-architecture-style.md): no repositories, the module's `DbContext` is the data-access API). The dependency guard is Domain-only (rule 1). Do not import the classic Clean-Architecture guard reflexively.

Deliberately *not* arch-tested (behavioral, owned elsewhere): ownership scoping → the §4 matrix; consumer idempotency and domain-events-dispatch-before-save → integration tests.

*Catalog cross-checked 2026-07-15 against current arch-testing practice (Jovanović's architecture-tests and VSA-testing guidance): naming, colocation, and slice-independence adopted/adapted; visibility rejected; layer-guard rules confirmed already covered.*

## 6. Style

### 6.1 Naming: behavior sentences

Test names are snake_case sentences stating **behavior and outcome in domain language** — `Revoked_account_gets_403_with_a_still_valid_token`, `Logging_a_dose_on_a_paused_schedule_is_rejected` — never the implementation. Classes: `<TypeName>Tests` (unit), `<UseCase>Tests` (slice). Rejected: `Method_Scenario_ExpectedResult` — encodes the method name (rots on rename, reads as implementation notes); "which tests cover X" navigation comes from §1 path mirroring and §5 colocation, so names don't need to carry it.

### 6.2 AAA

Blank-line-separated phases; no `// Arrange` comments (noise) unless a section is genuinely non-obvious. One act per test. Multiple Shouldly asserts are fine when they check one logical outcome. Zero control flow (`if`/loops) in test bodies.

### 6.3 Builders: hand-rolled, through real factories

Fluent test-data builders with sensible defaults (`aSchedule().Paused().Build()`), hand-written. Aggregates are **never constructed via reflection or serialization bypass** — a test that sidesteps invariants tests a state production cannot reach. No AutoFixture/Bogus initially (P3: no magic layers); Bogus is the recorded candidate if data variety becomes real toil.

### 6.4 Time

Unit tests pass literal `DateTimeOffset` values; meaningful edges get **named constants** (`CetSpringForwardGap2026`). `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) wherever a component takes `TimeProvider`. The BannedApiAnalyzers time ban **applies to test projects too** — determinism by construction, not discipline.

### 6.5 Data-driven

TUnit `[Arguments]` for inline cases; `[MethodDataSource]` for computed matrices (recurrence/DST tables). Case values are boundary or representative — **never random** (randomness in tests is flakiness with extra steps; property-based testing would be a deliberate separate adoption, not a drift).

### 6.6 Assertions: Shouldly — confirmed against the TUnit-native alternative

ADR-0003 picked Shouldly before the TUnit-native question was properly weighed; re-examined 2026-07-15:

- **TUnit.Assertions** is async-first — every assertion is `await Assert.That(x).IsEqualTo(y)` and the containing test must be `async Task`. That buys framework-integrated scopes and an analyzer against un-awaited assertions, but puts `await` ceremony on every line of the pyramid's base — our **pure, sync domain tests**, exactly where most assertion lines live — and forces async signatures onto sync code. It also version-churns pre-1.0 (0.6x) on top of TUnit's own weekly cadence, and welds every assertion line to the framework we already flagged as the risky bet.
- **Shouldly** is terse and sync where our code is sync (`check.ShouldBeFail(...)`), framework-agnostic (survives any test-framework migration untouched), and healthy (4.3.0 stable, Duende sponsorship 2025, v5 modernization in preview with AOT). TUnit detects failures via exceptions, so any assertion library works.

**Decision: Shouldly everywhere; no bare `Assert.*`; TUnit.Assertions not referenced.** Domain-shaped assertion extensions where they earn their keep (`ruleCheck.ShouldBeFailWith("schedule.not-active")`, `result.ShouldBeNotFound()`) — tests read as spec, union-unwrapping centralized. Revisit trigger: TUnit.Assertions reaches a stable 1.0+ *and* a concrete integration gap (scoped multi-assert, analyzer coverage) actually bites.

### 6.7 Shared test utilities: growth rule

No shared `TestKit` project up front; helpers live beside their consumers. `tests/DoseUp.TestKit` is extracted **when the second test project needs the same helper** — the SharedKernel entry rule, applied to tests.

## 7. CI & quality jobs

Cadence fixed by [ADR-0003-testing-stack](../adr/0003-testing-stack.md)/[ADR-0004-delivery-and-process](../adr/0004-delivery-and-process.md) (`ci.yml` gates only · E2E smoke-PR / full-nightly · coverage nightly, no PR threshold · mutation nightly-if-viable · ubuntu runners, health-probe waits + explicit timeouts). This document adds:

- **PR gates: all four .NET suites on every PR, split for fail-fast.** *Fast job* = build + UnitTests + ArchitectureTests (seconds, no Docker); *harness job* = IntegrationTests (Docker: Postgres + emulators + full-graph startup). Both required to merge — integration is the primary coverage layer (§2 P2), it can never be nightly-only; the split only buys a cheap red ✗ fast.
- **Stryker × TUnit spike is scheduled into the first domain-module change** (not M0): it needs a meaningful corpus of real domain tests to mutate; the shared-kernel change has only primitives. Decision logic unchanged (ADR-0003): viable → nightly with dashboard baseline, never a PR gate.
- **Flake policy: zero auto-retry at every layer; quarantine with a paper trail.** Unit/arch are deterministic by construction (§6.4/§6.5); integration flake is a *bug* in the harness model — isolation by construction makes it rare and meaningful (often a real race caught by parallel account-scoped tests). A genuinely flaky test gets `[Skip]` + a linked issue in the same PR that discovers it. Blanket retries would convert the suite's strongest concurrency signal into noise.
- **Deferred mechanics, named:** test-result reporting (TRX artifacts + PR annotations — exact MTP reporter picked in M0's CI change); what the PR-time E2E smoke targets (ephemeral local compose vs deployed env — decided with the web scaffold in M1; the smoke-on-PR intent stands).

## 8. Considered and not adopted (with reopening triggers)

- **ProblemDetails snapshot tests (Verify):** slice tests already assert the exact contract fields (codes, `violations[]`); an approval workflow adds ceremony for shapes that are hand-asserted on purpose. Trigger: PD assertions become repetitive boilerplate across many slices.
- **Integration-event contract tooling:** compiler-checked in one repo (§2); versioning discipline activates with the first cross-module contract (conventions § Events). Trigger: a second deployable or external consumer (same trigger as Pact, §2).
- **Performance/load testing:** nothing at circle scale warrants it; E2E timings give a coarse nightly signal for free. Trigger: a real latency complaint or FR-21-scale growth; k6 is the candidate.
- **a11y automation (axe-core in Playwright):** belongs to the web-scaffold design (M1 — the open React-stack design decision, tracked in [#26](https://github.com/jakubbohm/DoseUp/issues/26)), where it's a strong candidate for the PWA-showcase bar — deferred to that decision, not rejected.
- **Property-based testing:** §6.5's stance — a deliberate future adoption (CsCheck/FsCheck candidates) if the recurrence engine's edge space outgrows curated tables; never a drift-in.

## 9. M0 verification checklist

Mechanics this document asserts directionally — each verified (and this doc corrected if wrong) by the shared-kernel/M0 changes:

1. FastEndpoints does **not** auto-bind plain `AbstractValidator<T>` (handler-step-1 validation stays the only channel — conventions § API). — **c001:** deferred with the FV bridge; no validator ships yet.
2. ProblemDetails wiring covers auth-middleware 401/403 responses. — **c001:** 401 half **verified** (the bare middleware denial carries `application/problem+json` via `AddProblemDetails` + status-code pages); the 403 half lands with M0's `ActiveAccount`.
3. Harness excludes the web resource in test mode (mechanism: args/env via the testing builder). — **c001:** deferred — no web resource exists; the AppHost adopts the config-conditional exclusion when the scaffold lands.
4. Second accepted JWT authority injectable through `DistributedApplicationTestingBuilder` config mutation (§3c). — **c001:** **verified** — `CreateResourceBuilder<ProjectResource>("api").WithEnvironment("Auth__TestAuthority__…")` before build; the harness's minted tokens are trusted end-to-end.
5. Migrations apply in the harness via the same path local dev uses (§3a — never `EnsureCreated`). — **c001:** **verified** — the `DoseUp.MigrationService` runner executes in the harness session exactly as under `aspire start`; the api resource is gated on its completion.
6. TUnit `ClassDataSource(Shared = PerTestSession)` fixture behavior under `dotnet test`/MTP matches §3a's assumptions (TUnit churns weekly — re-verify at implementation). — **c001:** **verified** on TUnit 1.60.0 — one AppHost start served the whole session across test classes.
7. Wolverine × ASB emulator inside the harness (extends the M0 ASB spike — [ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md); §3d fallback stands ready). — M0.
8. MTP test reporter / TRX annotation choice (§7). — M0.
