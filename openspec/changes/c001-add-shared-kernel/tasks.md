# c001-add-shared-kernel — Tasks

Each task ends with `dotnet build DoseUp.slnx` and `dotnet test DoseUp.slnx` green (tests that exist keep passing). Design decision references (D1–D15) point into design.md; §-references into docs/conventions/testing.md.

## 1. Toolchain baseline

- [x] 1.1 **Union spike (scratchpad, not repo code):** scratch net11.0/`LangVersion preview` project on the **preview-6 SDK** proving the union grammar — parenthesized case list, whether case types can nest inside the union body (`RuleCheck.Fail` match form), generic unions, pattern-match forms incl. the preview-6 `not`-pattern semantics (applies to the union value itself) and non-public case constructors, implicit case conversions, framework `UnionAttribute`/`IUnion` presence without polyfill. Append findings to design.md D3 as "Spike results".
- [x] 1.2 Root tooling files: `Directory.Build.props` (Nullable, ImplicitUsings, `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`, CSharpier.MSBuild, BannedApiAnalyzers + `AdditionalFiles` BannedSymbols.txt), `Directory.Packages.props` (central versions per D2; ServiceDefaults' inline versions migrate in), `BannedSymbols.txt` (the four time-API lines, D12), `.editorconfig` copied verbatim from `D:\source\jakubbohm\Tastytrade.Api.Net\.editorconfig`, `global.json` with the MTP test runner (D10), `.vscode/settings.json` format-on-save. Bump ServiceDefaults to net11.0 + `LangVersion preview` (D1). Existing projects build green (CSharpier reformat of AppHost/ServiceDefaults included).

## 2. DoseUp.Api skeleton + SharedKernel primitives

- [x] 2.1 Create `src/DoseUp.Api` (net11.0, preview) with `SharedKernel/` + `Platform/` roots, reference ServiceDefaults, add FastEndpoints + JwtBearer (preview.6) + EF/Npgsql (preview.5, held per D2) + Aspire-client packages, minimal `Program` that boots. Add to slnx. **This build/run is the FE-on-net11(preview-6) verification gate and proves the EF-preview.5/ASP.NET-preview.6 mix restores cleanly** (D2 risks) — record both outcomes in design.md.
- [x] 2.2 `Result` union + case types + error model per spike grammar (D3); XML-doc the case semantics against the taxonomy table.
- [x] 2.3 `RuleViolation`, `RuleCheck` union, `RuleSet` (stage composition, deferred + strictly sequential async, aggregate-within-stage, `ToResult` bridge) per domain-rules.md §5.
- [x] 2.4 `Entity<TId>`, `AggregateRoot<TId>` + `IAggregateRoot` (raise/drain), typed-id pattern (`ITypedId` shape, `Guid.CreateVersion7()` factory) + the one generic uuid value converter (D4).
- [x] 2.5 `IDomainEvent`/`IDomainEventHandler<T>` + ~30-line DI dispatcher (loop-until-quiescent, depth guard) + `IIntegrationEventPublisher` port (D4).

## 3. UnitTests — exhaustive primitive coverage

- [x] 3.1 Create `tests/DoseUp.UnitTests` (TUnit.Engine + Shouldly only — no TUnit.Assertions, D10), path-mirrored layout (§1), one smoke test; `dotnet test DoseUp.slnx` executes it via MTP (global.json path proven).
- [x] 3.2 Result + error-model tests: every spec scenario of `shared-kernel-primitives` R1–R2 (case distinguishability, payload fidelity, multi-field validation aggregation).
- [x] 3.3 RuleCheck/RuleSet tests: R3–R6 scenarios (single-rule fail shape, within-stage aggregation + ordering, stage gating, nothing-runs-before-check, sequential-async interleaving probe, lossless conversion). Build the domain-shaped Shouldly extensions here (`ShouldBeFailWith(code)` …, §6.6).
- [x] 3.4 Base-type + id tests: R7–R9 scenarios (equality matrix incl. cross-type, event ordering, drain-once, v7 version nibble, converter round-trip to `Guid` provider type).
- [x] 3.5 Dispatcher + port tests: R10–R11 scenarios (multi-handler fan-out, handler-raised cascade, depth-guard throw, no-handler no-op, hand-rolled fake publisher receives the instance).

## 4. Platform: persistence, ProblemDetails, auth, endpoints

- [x] 4.1 Empty `DoseUpDbContext` + Aspire Npgsql EF client integration with `DisableHealthChecks = true` (D6) + `SaveChanges` interceptor draining via the dispatcher (D8) + EF `Design` package + **initial empty-model migration** committed.
- [x] 4.2 `src/DoseUp.MigrationService` worker (`MigrateAsync` under execution strategy, then stop) + AppHost graph: `postgres` → `migration` (`WaitFor`) → `api` (`WaitForCompletion`) (D8). Verify locally via `aspire start`: api reaches healthy, migration exits 0. *(Verified 2026-07-15: migration applied `20260715090247_Initial`, state `Finished`; api healthy in 26.7s.)*
- [x] 4.3 Auth + PD wiring: JwtBearer with config-driven `Auth:TestAuthority` trust anchor (refused in Production), `FallbackPolicy` = authenticated (D5); `AddProblemDetails` + status-code-pages + exception handler (D7); Platform Result→PD mapper + its unit tests (full status matrix, 409 `violations` shape + distinct `type` vs `Conflict`, 500 carries no internals — `error-contract` R2–R5 mapper-level scenarios).
- [x] 4.4 FastEndpoints wiring + the **identity-echo endpoint** (authenticated, echoes `oid`, no DB) and health exposure via `MapDefaultEndpoints` (D6). **Matrix classification in the same task (config rule):** echo = FE `Authenticated` kind row; `/health` + `/alive` = manual non-FE `AnonymousAllowed` rows; FE `AllowAnonymous` allowlist stays empty. Record the census subtlety for 7.2.

## 5. ArchitectureTests — the catalog, vacuously green

- [x] 5.1 Create `tests/DoseUp.ArchitectureTests` (ArchUnitNET 0.13.3 + TUnit adapter); one shared `ArchLoader` architecture; implement catalog rules 1–9 + 12–14 as ArchUnitNET rules, each test quoting its exact ADR-0002/conventions line (convention 1); all pass vacuously; rule 12's allowlist is in-test data (currently empty per D6/4.4). *(Adapter dropped — hard-depends on TUnit.Assertions 0.52.51; Shouldly `ShouldHold` helper instead, design D11. Capture-group rules 2/3/4/7 walk the ArchUnitNET model; vacuous fluent rules use `WithoutRequiringPositiveResults`.)*
- [x] 5.2 Rules 10 (reflection over the offline-built EF model — every `DbSet` entity is `IAggregateRoot`; vacuous on the empty model) + 11 (colocation via reflection + TUnit data source). Confirm rules 15/16 owners need no test code (BannedSymbols.txt / central-package absence) — assert BannedSymbols exists as a repo-hygiene test if trivial, else note. *(Rule 15 hygiene test ships; rule 16 stays convention + review — no test, single-owner honored.)*

## 6. IntegrationTests — the Aspire harness

- [x] 6.1 Create `tests/DoseUp.IntegrationTests` + `AspireAppFixture` (`IAsyncInitializer`/`IAsyncDisposable`, `[ClassDataSource<…>(Shared = SharedType.PerTestSession)]`): `CreateAsync<Projects.DoseUp_AppHost>`, wait `migration` = Finished then `api` healthy, CI-stretched timeouts, resilient HTTP clients (D9). First slice test: anonymous `GET /health` → 2xx (`api-shell` R2 scenario 1). **Verifies §9.4 mechanism reachability, §9.5 (real migration path), §9.6 (PerTestSession under MTP).**
- [x] 6.2 Test identity: per-session HS256 key, `JsonWebTokenHandler` minting, `WithEnvironment` injection of `Auth:TestAuthority` into the api resource; caller classes anonymous / authenticated / untrusted-key (D9). Slice tests: echo 200 + `oid` echoed (`api-shell` R3), no-token → 401 **with ProblemDetails body** (`error-contract` R1 — the §9.2 401-half verification), untrusted-key → 401 (`api-shell` R1).
- [x] 6.3 AuthZ-matrix scaffold (§4 mechanics): reflection census over the API assembly's FE endpoint classes + kind-classification table + completeness gate (unclassified endpoint fails) + the manual non-FE rows; cell-per-(endpoint × caller) via TUnit data source. Plus the DB-free-probe test (`api-shell` R2 scenario 2) using the harness's EF command-count mechanism (D6).

## 7. CI, §9 walk, docs

- [x] 7.1 `.github/workflows/ci.yml`: ubuntu-latest, exact SDK pin `11.0.100-preview.6.26359.118`, `dotnet build DoseUp.slnx`, `dotnet test --solution DoseUp.slnx`, `timeout-minutes: 30` (D13). Verify green on this change's PR. *(Verified 2026-07-15: PR #1 run 29406179807 green in 1m34s — build with CSharpier check-mode + all three suites incl. the harness; a `.gitattributes` forcing CRLF for `*.cs` was required so the Linux checkout matches the .editorconfig ending CSharpier enforces.)*
- [x] 7.2 Doc corrections (D15): annotate testing.md §9 items with c001 outcomes (2 = 401-half verified · 4/5/6 verified · 1 deferred with the FV bridge · 3 deferred, no web resource · 7/8 = M0); add the §4 census clarification sentence if 4.4/6.3 confirmed the non-FE-health shape; amend ADR-0001's union-caveat consequence line (union attrs ship in the framework per the preview-5/6 notes — no polyfill; mandatory case-list grammar; preview-6 `not`-pattern semantics); annotate roadmap M0 (pulled-forward items + FE-on-net11 spike absorbed here).
- [x] 7.3 Config-rule confirmations, recorded not skipped: **no TS-client regeneration task** (PRE-6 pipeline does not exist until M0 — new endpoints enter `openapi.json` when the export lands) · **no infra Bicep task** (`infra/` does not exist until M0; the AppHost change is local/test orchestration only, PRE-9 split honored) · no feature flags introduced.
- [x] 7.4 Final sweep: full `dotnet build` + `dotnet test` green locally and in CI; CSharpier check clean; `openspec validate --changes c001-add-shared-kernel` passes; traceability pass — every spec scenario across the three capabilities maps to a named test (or a recorded deferral in 7.2). *(Done 2026-07-15: 73/73 locally and on PR #1 CI; CSharpier check clean; validate passes; all 27 spec scenarios traced — sole scoped deferral: error-contract R5's unhandled-exception wire path, mapper-level verified, no throwing endpoint exists yet per the spec's own scoping header.)*
