# c001-add-shared-kernel — Design

## Context

The repo holds an empty Aspire AppHost skeleton (`Aspire.AppHost.Sdk/13.4.6`, net10.0), a net10.0 ServiceDefaults, and finished docs. This change materializes PRE-7 (SharedKernel primitives) and PRE-8 (test infrastructure) per the proposal. Everything below implements ADR-0001/0002/0003 and the conventions docs — **no new architectural direction is introduced, so no new ADR is required** (design rule satisfied); two existing doc lines get factual corrections (D15).

All ecosystem facts were verified against live sources on **2026-07-15** (three research passes: test stack, platform, Aspire testing). Facts that contradicted our docs' assumptions are called out inline and in §Corrections.

## Goals / Non-Goals

**Goals:** compile-and-run proof of the PRE-7 primitives on the real preview toolchain · the PRE-8 test topology operational end-to-end (unit exhaustive, arch catalog vacuously green, Aspire harness session-shared with real JWT auth) · the §9 checklist items c001 can reach, verified · tooling baseline per Jakub's verdicts · minimal PR gate.

**Non-Goals:** proposal Non-goals apply (no business module, Entra, Wolverine, infra, TS pipeline, FV bridge, SmartEnum converter, analyzer packs). Additionally design-level: no `Result<T>` yet (enters with the first read slice — same growth rule as the FV bridge), no `FakeTimeProvider` package (no component takes `TimeProvider` yet), no MTP reporter choice (testing.md §7 assigns it to M0).

## Decisions

### D1 — Project topology and TFMs

```
DoseUp.slnx
├─ src/
│  ├─ DoseUp.AppHost/            net10.0 (Aspire SDK pin — unchanged)
│  ├─ DoseUp.ServiceDefaults/    net11.0 + LangVersion preview (bumped: it is a service-side
│  │                             project; only the AppHost is net10-pinned, and nothing net10
│  │                             references it)
│  ├─ DoseUp.Api/                net11.0 + preview — SharedKernel/ + Platform/ (+ Modules/ arrives M1)
│  └─ DoseUp.MigrationService/   net11.0 + preview — EF migration runner (D8)
└─ tests/                        all net11.0 + preview (testing.md §1)
   ├─ DoseUp.UnitTests/
   ├─ DoseUp.ArchitectureTests/
   └─ DoseUp.IntegrationTests/
```

The net10 AppHost referencing net11 projects is legal because Aspire AppHost `ProjectReference`s are metadata-only (the SDK generates `Projects.*` classes; it never consumes the target assembly). IntegrationTests (net11) references the AppHost (net10) normally — higher-to-lower is always fine.

### D2 — Version strategy: preview-6 SDK, newest-available packages

c001 is greenfield, so it starts on the **freshest preview** rather than the installed one (Jakub's verdict 2026-07-15 — the "upgrades are separate PRs" policy protects existing code, not a change that creates the codebase; and the motivating union feature moved in preview 6). Concretely:

- **SDK / runtime / compiler: .NET 11 preview 6** — SDK `11.0.100-preview.6.26359.118`, runtime `11.0.0-preview.6.26359.118` (exact numbers confirmed against the installed toolchain 2026-07-15; the earlier `26328.106` transcription was wrong). SDK 10.0.301 stays installed for the net10 AppHost runtime.
- **ASP.NET packages: preview.6** (`11.0.0-preview.6.26359.118`).
- **EF Core family: held at preview.5.** `Npgsql.EntityFrameworkCore.PostgreSQL` has **no preview.6** (newest is `11.0.0-preview.5`, verified 2026-07-15; the provider historically lags EF by days–weeks), and it pins `Microsoft.EntityFrameworkCore` **exactly** at `11.0.0-preview.5.26302.115` — so the whole EF family stays preview.5 until Npgsql ships. The day it does, the standing preview-upgrade activity bumps both (watch item). EF preview.5 assemblies on the preview-6 runtime is within-major compatibility — expected to hold; the harness startup + migration runner exercise it on first run (risk logged below).

| Package | Version | Verified |
|---|---|---|
| FastEndpoints | 8.2.0 (2026-06-24; TFMs net8/9/10 — **no net11**, consumed via roll-forward onto the preview-6 runtime) | nuget.org |
| Microsoft.EntityFrameworkCore.* | 11.0.0-preview.5.26302.115 (**held** — Npgsql exact-pin constraint, D2) | nuget.org |
| Npgsql.EntityFrameworkCore.PostgreSQL | 11.0.0-preview.5 (no preview.6 published as of 2026-07-15) | nuget.org |
| Microsoft.AspNetCore.Authentication.JwtBearer | 11.0.0-preview.6.26359.118 | nuget.org |
| Aspire.Hosting.PostgreSQL / Aspire.Hosting.Testing / Aspire.Npgsql.EntityFrameworkCore.PostgreSQL | 13.4.6 (client-integration exact version confirmed at restore) | nuget.org |
| TUnit.Engine | 1.60.0 (2026-07-15) | nuget.org |
| Shouldly | 4.3.0 (v5 still preview — not taken) | nuget.org |
| TngTech.ArchUnitNET | 0.13.3 (2026-03-05; the TUnit adapter was dropped at implementation — it hard-depends on TUnit.Assertions 0.52.51, see D11) | nuget.org |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 (2026-07-02) | nuget.org |
| CSharpier.MSBuild | 1.3.0 — **removed 2026-07-15**, formatting authority flipped to `.editorconfig`/IDE0055 (D12 amendment) | nuget.org |
| Microsoft.IdentityModel.JsonWebTokens (tests, token minting) | current 8.x at restore | — |

All versions land in **`Directory.Packages.props`** (central package management; ServiceDefaults' inline versions migrate into it).

### D3 — Union implementation (C# 15, preview-6 reality)

Two facts from verification **correct our docs' sketches**:

1. **No polyfill needed:** `UnionAttribute`/`IUnion` ship in the framework (`System.Runtime.CompilerServices`) — introduced at preview 5 and reconfirmed in the preview-6 release notes. ADR-0001's "check whether the current preview still does" is answered — a small ADR-0001 amendment task records it. (`closed` hierarchies still need a hand-declared `ClosedAttribute` — not used here.)
2. **Real grammar:** `union Result { … }` is not valid C#. A union declaration takes a **mandatory parenthesized case-type list** of pre-existing types, with an optional members-only body: `public union Result(Success, Validation, NotFound, RuleViolations, Conflict, Forbidden, Unexpected);` — grammar unchanged in preview 6 (its notes use `public union Pet(Dog, Cat);`). Case types are declared separately (records / record structs). Value-type cases box through the generated `object? Value` — known and accepted (ADR-0001; circle scale).
3. **Preview-6 refinements** (release notes, 2026-07-14): case types may now have **non-public single-parameter constructors** (invariant-guarding case types stay possible) · the **`not` pattern now applies to the union value itself**, not the contained case value — a semantics change versus preview 5 the spike must pin down · custom union types support inherited `Create` methods · clearer compiler errors for missing required APIs · System.Text.Json serializes union values directly (noted only — `Result` never crosses the wire; the edge speaks ProblemDetails/DTOs).

**Adopted shape (post-spike):** case types are declared **nested inside the union body**, giving the `Result.NotFound` / `RuleCheck.Fail` qualified naming domain-rules.md sketches — no namespace pollution from generic names like `Conflict`, and one consistent construction/match form everywhere:

```csharp
public union Result(
    Result.Success, Result.Validation, Result.NotFound, Result.RuleViolations,
    Result.Conflict, Result.Forbidden, Result.Unexpected)
{
    public readonly record struct Success;
    public sealed record Validation(/* field → messages map, tuned in task 2.2 */);
    public readonly record struct NotFound;
    public sealed record RuleViolations(IReadOnlyList<RuleViolation> Violations);
    public readonly record struct Conflict;    // reserved (class 7)
    public readonly record struct Forbidden;   // dormant until FR-21-style permissions
    public readonly record struct Unexpected;  // bugs throw; this case is the mapped-500 marker
}

public union RuleCheck(RuleCheck.Pass, RuleCheck.Fail)
{
    public readonly record struct Pass;
    public sealed record Fail(IReadOnlyList<RuleViolation> Violations);
}
```

#### Spike results (task 1.1, 2026-07-15, SDK 11.0.100-preview.6.26359.118)

- **Nested case types work — but the case list must qualify them.** `union RuleCheck(RuleCheck.Pass, RuleCheck.Fail) { … }` compiles; unqualified `(Pass, Fail)` fails CS0246 (the case list resolves in the *enclosing* scope, while patterns and union-body members resolve nested names unqualified as usual). Unions may combine a case list with a members body (helper properties like `IsPass => this is Pass` work).
- **Exhaustiveness is compiler-tracked.** A switch expression covering every case with no default arm compiles with no diagnostic; a missing arm raises **CS8509 naming the missing case** — under our `TreatWarningsAsErrors` that is a build break, i.e. compile-time exhaustiveness enforcement (the closed-union property ADR-0001 wanted).
- **Value semantics:** implicit case→union conversion is a compiler lowering to a generated per-case ctor (no `op_Implicit` members); **`==` does not compile** on union values (CS0019) — compare with `Equals` (same case + equal payload ⇒ true, cross-case ⇒ false) or patterns; union `ToString()` returns just the union type name, it does **not** forward to the case value — assertion/diagnostic helpers must format `Value`.
- **Representation:** the union is a struct wrapping a generated `object Value` property (value-type cases box — accepted, ADR-0001); `System.Runtime.CompilerServices.UnionAttribute` + `IUnion` are present on it from `System.Private.CoreLib` — **no polyfill**, confirming D3 fact 1. A **boxed union does not unwrap**: `(object)result is Success` is `false`; case tests only work on expressions statically typed as the union.
- **`default(Result)` is a hole:** `Value` is `null` and *no* case pattern matches (every `is X` false, every `is not X` true); an exhaustive switch over it throws `SwitchExpressionException` at runtime. Never construct/expose default union values — a runaway `default` is bug class 8; unit tests pin this behavior.
- **Preview-6 refinements confirmed empirically:** a case type with a non-public single-param ctor (private ctor + static factory) converts and matches fine; the `not` battery behaves as plain negation of the case test (`(NotFound) is not Success` → true, `(Success) is not Success` → false); generic unions (`union Option<T>(Some<T>, None)`) compile and match.
- **Implementation-time additions (task 2.2):** unions accept the `readonly` modifier and an interface base list after the case list (`readonly union Result(…) : IEquatable<Result>`), and body members can read the generated `Value`. Unions do **not** generate `Equals`/`GetHashCode` — the spike's working equality was inherited reflection-based `ValueType.Equals` — so Result/RuleCheck hand-write `Equals(other)`/`GetHashCode` plus `==`/`!=` (CA1815/CS0660 forced the issue; same observable semantics, non-reflective). Construction ergonomics: a static member cannot share a nested case type's name, so domain-rules.md's value sketches (`RuleCheck.Pass`, `Result.NotFound`) compile as `new RuleCheck.Pass()` / `new Result.NotFound()` — uniform `new Union.Case(…)` everywhere; the doc gets a one-line note in task 7.2 (its header already declares signatures directional).

**`Result` stays non-generic in c001.** A `Result<T>`-style read shape enters with the first read slice that needs it (growth rule — same logic as the FV-bridge deferral).

### D4 — SharedKernel internals

- **Dispatcher:** `IDomainEvent` (marker), `IDomainEventHandler<TEvent>`, and a ~30-line `DomainEventDispatcher` resolving handlers from `IServiceProvider` (SharedKernel may reference `Microsoft.Extensions.DependencyInjection.Abstractions` — external, not project-internal, so ADR-0002 rule 5 holds). Loop-until-quiescent over aggregates' drained events; `MaxDepth` guard (const, ~10) throws `UnreachableException`-adjacent invalid-operation — a runaway cascade is a bug (class 8).
- **Base types:** `Entity<TId>` (identity equality: concrete type + id), `AggregateRoot<TId> : Entity<TId>, IAggregateRoot` with `protected void Raise(IDomainEvent)` and an internal-drain surface consumed by the interceptor.
- **Typed ids:** `readonly record struct XxxId(Guid Value)` per aggregate, hand-written; a tiny `ITypedId` shape (`Guid Value` + static-abstract `From` — what the converter needs) lets **one generic converter** (`TypedIdConverter<TId>`) and one Platform model-builder convention cover every id. Generation: each id's own one-line `Create() => new(Guid.CreateVersion7())` — implementation found a static-virtual interface default unreachable on the concrete type (C# exposes static virtuals only via constrained type parameters), so the mint is part of the hand-written pattern, demonstrated by the unit suite's `TestId`. No id types ship in c001 beyond what tests define — the pattern + converter are the deliverable.
- **Publisher port:** `IIntegrationEventPublisher` with a transport-free `PublishAsync` signature; no implementation in c001 (arch rule 9 ships vacuous; Platform's outbox implementation is M0+).

### D5 — Authentication: one bearer scheme, config-driven trust anchors

Single JWT bearer scheme whose `TokenValidationParameters` are built from configuration: an optional `Auth:TestAuthority` section (`Issuer`, `Audience`, `SigningKey` base64) adds a **second accepted authority** — exactly testing.md §3c's mechanism. In c001 it is the *only* authority (no Entra yet); M0 adds Entra as the primary. Platform refuses the `TestAuthority` section when `IHostEnvironment.IsProduction()` — cheap hardening now, no M0 rework. Secure-by-default is the ASP.NET **`FallbackPolicy` = require-authenticated-user**, which covers FastEndpoints *and* non-FE mapped surface; anonymous endpoints opt out explicitly (arch rule 12's allowlist).

### D6 — Health: ASP.NET health checks, not a FastEndpoints endpoint

`MapDefaultEndpoints()` (ServiceDefaults) keeps providing `/health` + `/alive` — Aspire's `WaitForResourceHealthyAsync` and future ACA probes ride them; a duplicate FE health endpoint would be a second mechanism for a solved problem. The probe mappings carry explicit `AllowAnonymous()` (the api's authorization FallbackPolicy would otherwise demand a token on probe traffic). The Npgsql/EF client integration registers its DB health check by default — **disabled** (`DisableHealthChecks = true`) so the probe path stays database-free (ADR-0001; api-shell spec). Verification of "no DB command on probe" ended up simpler than the predicted EF command-count interceptor: nothing in c001 opens an application DB connection, so the slice test bursts the probe and asserts `pg_stat_activity` shows **zero backends** for the database — any probe-path DB touch would leave a pooled connection visible (verified green 2026-07-15).

**Consequence (predicted testing.md §4 correction):** the authZ-matrix census reflects FastEndpoints endpoint classes, and health checks live outside it. The matrix table gains explicit manual rows for non-FE anonymous surface (`/health`, `/alive`); the completeness gate still diffs the FE census. testing.md §4 gets one clarifying sentence — written when the implementation confirms the mechanics (§9 walk).

### D7 — ProblemDetails wiring

`AddProblemDetails()` + status-code-pages + exception-handler middleware so that **every** non-2xx — including bare auth-middleware 401s and unhandled exceptions — carries an RFC 9457 body (error-contract spec). The **single Platform mapper** converts `Result` failure cases to PD per the conventions matrix (`RuleViolations` → 409 + `violations[]`, distinct `type` from `Conflict`'s 409). Thin FE endpoints send the mapper's output; nothing else crafts error responses. Exception path is sanitized (no internals). The exact 401-body behavior of the middleware stack is §9.2's verification target — wiring is expected to work per current ASP.NET PD integration, and the slice test proves it.

### D8 — Persistence plumbing and the migration runner

Empty `DoseUpDbContext` (Platform) registered via the Aspire Npgsql/EF client integration (OTel + retries; health check off per D6) with the `SaveChanges` interceptor that drains aggregate events through the dispatcher *before* save (ADR-0002 — its in-UoW proof deferred to the first aggregate). One **initial (empty-model) migration** proves the schema pipeline.

**Migrations apply via a `DoseUp.MigrationService` worker in the AppHost graph** — Aspire's documented pattern (official `database-migrations` sample, 13.4-era): `migration` project `WaitFor(postgres)`, `api.WaitForCompletion(migration)`; the worker runs `MigrateAsync()` under the execution strategy and stops. Because the harness runs the real AppHost, **local dev and the test session apply migrations through the identical path** — §3a's requirement, §9.5 verified. This pre-answers M1's "local dev auto-applies" mechanics with the ecosystem-blessed shape; production is untouched (PRE-9 CD bundle; the AppHost never runs there). No data volumes/persistent lifetimes anywhere (session DB is disposable by design).

### D9 — The harness fixture

- **Lifecycle:** `AspireAppFixture : IAsyncInitializer, IAsyncDisposable` (namespaces verified: `TUnit.Core.Interfaces` / BCL), shared via `[ClassDataSource<AspireAppFixture>(Shared = SharedType.PerTestSession)]` — API confirmed current in TUnit 1.60.0.
- **Start:** `DistributedApplicationTestingBuilder.CreateAsync<Projects.DoseUp_AppHost>(...)` (confirmed current in Aspire.Hosting.Testing 13.4.6; dashboard off + randomized ports are testing defaults). Test-authority injection: `appHost.CreateResourceBuilder<ProjectResource>("api").WithEnvironment("Auth__TestAuthority__…", …)` before `BuildAsync()` — the documented per-resource pattern (§9.4's mechanism).
- **Readiness:** `app.ResourceNotifications.WaitForResourceHealthyAsync("api", ct)` after waiting `migration` reaches `KnownResourceStates.Finished`; every wait wrapped in `.WaitAsync(timeout)` with the timeout stretched when `CI` is set (aspire.dev testing-in-CI guidance).
- **Clients:** `app.CreateHttpClient("api")` + `ConfigureHttpClientDefaults(AddStandardResilienceHandler())`; `GetConnectionStringAsync("doseupdb")` available for the rare direct-DB arrange (§3e exception).
- **Identity:** fixture generates a per-session HS256 key, mints tokens with `JsonWebTokenHandler`. Caller classes shipped in c001: **anonymous**, **authenticated** (valid token + `oid`), **untrusted-key** (valid shape, unknown signer). Member-owner/other, admin, revoked arrive with M0's account table.
- **Resource exclusion:** none needed yet (no web resource exists). The AppHost adopts the config-conditional pattern (`builder.Configuration.GetValue("…", true)`) the day the web scaffold lands — noted so M1 doesn't invent a second mechanism; testing.md §9.3 stays unverifiable here (recorded as deferred, not failed).

### D10 — Test project packaging

**`TUnit.Engine` only** — the `TUnit` metapackage force-pulls `TUnit.Assertions`, which testing.md §6.6 explicitly keeps unreferenced; Engine (which owns Core + MTP MSBuild entry point) is the verified minimal authoring+running set. Shouldly 4.3.0 is the assertion library. **`global.json`** gains `{ "test": { "runner": "Microsoft.Testing.Platform" } }` — the *current* MTP switch for `dotnet test` (the `dotnet.config` mechanism died at .NET 10 RC2; testing.md §1's "one entry point" holds, all three projects are MTP so the all-or-nothing rule is satisfied). No SDK pin in global.json (preview-riding stays frictionless; CI pins exactly, D13). TRX/reporter packages deliberately omitted — testing.md §7 assigns the reporter choice to M0 (`--report-gh` via `Microsoft.Testing.Extensions.GitHubActionsReport` noted as the leading candidate).

### D11 — Architecture tests

ArchUnitNET 0.13.3, **without** the TngTech.ArchUnitNET.TUnit adapter — implementation found it depends directly on `TUnit.Assertions 0.52.51` (a stale 0.x pin against our 1.60 train, and §6.6 keeps TUnit.Assertions out entirely); a ~10-line Shouldly-based `ShouldHold(this IArchRule)` helper replaces it, failing with the rule's own violation descriptions. One static `Architecture` from `ArchLoader` over the Api assembly. Every catalog rule with a test owner ships now, each test naming the exact doc line it enforces (testing.md convention 1): rules 1–9, 12–14 as ArchUnitNET rules (vacuously green until targets exist); rule 10 as reflection over the offline-built EF model; rule 11 as reflection + TUnit data source; rule 12's anonymous allowlist is **data in the test** (first entries: none — health/alive are non-FE, per D6). Rule 15 is `BannedSymbols.txt`; rule 16 is the absence of banned providers from `Directory.Packages.props` (convention + review — no fake mechanization).

### D12 — Tooling files

- **`Directory.Build.props`:** `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`, CSharpier.MSBuild + BannedApiAnalyzers package refs, `AdditionalFiles BannedSymbols.txt` — uniform across src and tests (AppHost included). TFM + `LangVersion` stay per-csproj (explicit beats magic; the AppHost must differ anyway). Implementation added `GenerateDocumentationFile=true` (Roslyn requires it for IDE0005 unnecessary-usings on build) with `NoWarn CS1591` (XML docs stay convention-owned, not compiler-forced).
- **`BannedSymbols.txt`** (exact, verified format):
  `P:System.DateTime.Now;Inject TimeProvider (PRE-7)` · same for `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`. Applies to tests too (§6.4). The Platform clock exemption has **zero call sites in c001** (`TimeProvider.System` registration isn't banned); the documented exemption mechanism when one appears: path-scoped `.editorconfig` section setting `dotnet_diagnostic.RS0030.severity = none`, or `#pragma` in the single file (both verified workable).
- **`.editorconfig`:** copied verbatim from the Tastytrade reference per Jakub, plus an appended, documented **DoseUp adjustments** section. The "layout preferences are inert" assumption was wrong: `EnforceCodeStyleInBuild` turns them into IDE0055 build errors, and the reference file's brace style contradicts CSharpier's output — two layout gates cannot coexist, so `IDE0055 = none` (CSharpier, check-mode in CI, is the only format gate) and `IDE0058 = suggestion` (the reference config's own intent — its `:suggestion` suffix doesn't bind under `latest-all`, and fluent builder chains discard return values by design). M0's analyzer-pack change extends this file with severity tuning. **Amended 2026-07-15 (Jakub, task 7.5):** the IDE0055 resolution ran the wrong way — muting it let CSharpier's hard-coded Allman override the reference file's end-of-line braces, the owner's actual style. Reversed: CSharpier removed everywhere (props, CI flag, `.vscode`), IDE0055 re-armed as the sole format gate (build error, probe-verified), tree reformatted via `dotnet format whitespace`, soft 200-char width guideline unenforced. Conventions § Formatting, ADR-0001/0004, software-factory F-34, roadmap updated.
- **`.vscode/settings.json`:** format-on-save wiring (conventions § Formatting).
- **Implementation notes (task 1.2):** ServiceDefaults' template `Extensions` class squatted in the `Microsoft.Extensions.Hosting` namespace (IDE0130/CA1724 under `latest-all`) — now `DoseUp.ServiceDefaults.ServiceDefaultsExtensions`; consumers add one using. `DoseUp.slnx` referenced project paths in lowercase — case-blind on Windows but a guaranteed miss on the ubuntu CI runner — normalized to on-disk casing (and per-project solution folders flattened to `/src/`).

### D13 — Minimal CI

`.github/workflows/ci.yml`: one job on `ubuntu-latest` (Docker preinstalled — verified current guidance), `actions/setup-dotnet` pinned to the **exact** SDK `11.0.100-preview.6.26359.118` (deterministic; each future preview bumps it via the standing upgrade PR), `dotnet build DoseUp.slnx` then `dotnet test --solution DoseUp.slnx`, `timeout-minutes: 30`. Fixture timeouts stretch on `CI` (D9). No publishing, no reporters, no split — M0's `add-ci-cd-gates` owns those (testing.md §7).

### D14 — Config-rule notes

No infra Bicep task (no `infra/` until M0; AppHost = local orchestration only). No TS-regen task (no pipeline until M0). Both recorded in the proposal; restated here per the design rules.

### D15 — Doc corrections this change carries

1. **ADR-0001** union-caveat consequence line: polyfill resolved (union attrs framework-shipped since preview 5, per the preview-5/6 notes), real case-list grammar and preview-6 `not`-pattern semantics noted.
2. **testing.md §4** census clarification (health outside the FE census) — pending implementation confirmation (D6).
3. **testing.md §9** items annotated with c001 outcomes: 2 (401 half verified), 4, 5, 6 verified; 1 deferred with the FV bridge; 3 deferred (no web resource); 7 M0; 8 M0.

## Risks / Trade-offs

- **[FastEndpoints on net11 previews is unverified in the field]** (no net11 TFM; zero community signals either way) → this change *is* the verification; first build/run answers it. Fallbacks stand ready per ADR-0001 (pin previous preview; Wolverine.HTTP as named alternative). Precedent risk: FE broke once on a .NET 10 alpha via an ASP.NET metadata change. **Outcome (task 2.1, 2026-07-15):** FE 8.2.0 restores onto net11.0 and the Api builds and boots on the preview-6 runtime with zero warnings; the runtime half (endpoint discovery, request pipeline) is proven at 4.4/6.x.
- **[C# unions are "In Progress" in Roslyn]** (feature branch; more of the proposal lands in future previews) → pinned SDK + exhaustive unit tests freeze today's semantics; preview-upgrade PRs surface breaks in isolation. The union spike (task 1.1) runs before mass adoption.
- **[Aspire-in-CI hangs]** (documented failure mode #1) → every wait time-boxed, CI-stretched timeouts, `timeout-minutes: 30`, `DisposeAsync` known-issue awareness (microsoft/aspire #7139).
- **[EF/Npgsql preview.5 packages on the preview-6 runtime]** (Npgsql preview.6 unpublished as of 2026-07-15) → within-major runtime compatibility is the norm; the harness startup + migration runner exercise EF end-to-end on first run, so a break surfaces immediately with an obvious cause. Npgsql's preview.6 release triggers the alignment bump (standing upgrade activity). **Outcome (task 2.1, 2026-07-15):** the mixed restore resolves cleanly (no NU1xxx warnings) and the Api boots; end-to-end EF proof lands with 4.1/4.2/6.1.
- **[TUnit weekly cadence]** → 1.60.0 pinned centrally; release-notes review found no breaking changes to our APIs in 2 months; public-API verification suite upstream is a stability signal. Upgrades ride the standing preview-upgrade activity.
- **[Migration-runner adds a project]** → accepted: it is the documented Aspire pattern and buys dev/test path identity (§3a) — the alternative (fixture-side `MigrateAsync`) would verify a path production tooling never uses.
- **[2-space C# indentation project-wide]** (copied .editorconfig) → deliberate (Jakub's reference style); CSharpier enforces it uniformly from day one, so no churn later.

## Implementation-time verification list

Called out so tasks can gate on them explicitly: union nested-case grammar + pattern-match forms incl. preview-6 `not` semantics (spike, task 1.1) · FE 8.2.0 restore/run on the net11 preview-6 runtime (first build) · the EF-preview.5 / ASP.NET-preview.6 package mix restores and runs cleanly (first build + harness) · 401 PD body via the middleware stack (§9.2 slice test) · exact version of the Aspire client integration at first restore · `MapDefaultEndpoints`'s environment guard behavior inside the harness (health must answer in the test session).
