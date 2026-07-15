# c001-add-shared-kernel — Proposal

## Why

Every module the roadmap plans (M1 profiles/substances/logging onward) builds on the same two foundations: the SharedKernel primitives PRE-7 designed (`union Result`, the domain-rule model, base types, typed ids, event plumbing) and the test infrastructure PRE-8 designed (three per-layer test projects, the Aspire harness, the architecture-test catalog). None of it exists yet — the repo holds an empty AppHost skeleton and finished docs. This change turns those interview outcomes into code so the first domain module can validate them, per the roadmap sequencing note (2026-07-14): domain foundations run ahead of the M0 walking skeleton.

**Requirements served:** NFR-7 (quality & process — the test pyramid and its enforcement come into existence), NFR-4 (the test-identity pipeline and the authorization matrix's first rows), NFR-8 (ServiceDefaults/OTel on the new API). **No FR is delivered** — this is the enabling layer under all of them. **Milestone:** pre-M0 pull-forward per the roadmap sequencing note; everything else in M0 (Entra, contract pipeline, full CI/CD gate set, deploy) stays in M0.

## What Changes

- **`src/DoseUp.Api`** (net11.0, `LangVersion preview`) — the modular-monolith shell per ADR-0002:
  - **SharedKernel:** `union Result` + error model · `RuleCheck`/`RuleViolation`/`RuleSet` · `Entity<TId>`/`AggregateRoot<TId>`/`IAggregateRoot` · typed-id pattern + one generic uuid value converter · `IDomainEvent`/`IDomainEventHandler<T>` + the ~30-line dispatcher · `IIntegrationEventPublisher` port.
  - **Platform:** composition root · JWT bearer authentication, secure by default (the test authority is the only trust anchor wired here) · the single Result→ProblemDetails mapper implementing the domain-rules error taxonomy · `SaveChanges` interceptor draining domain events through the dispatcher · an **empty `DbContext` + initial migration** — proves the real schema path end-to-end before any table exists.
  - **Endpoints (FastEndpoints, minimal):** anonymous, DB-free health · one authenticated **identity-echo diagnostic endpoint** — M0's "response proves identity" endpoint minus the DB stage; M0 adds `ActiveAccount`/`CallerContext` on top.
- **`tests/`** — three per-layer projects (testing.md §1): **UnitTests** (exhaustive primitive coverage), **ArchitectureTests** (every catalog rule with a test owner ships now and passes vacuously, incl. rule 14 naming; rule 15 ships as `BannedSymbols.txt`, rule 16 as absence from central packages), **IntegrationTests** (session-shared Aspire harness fixture, test-JWT authority injection, health + echo slice tests, authorization-matrix scaffold with its first rows).
- **AppHost** wires `postgres` + a small `DoseUp.MigrationService` runner (applies EF migrations, then exits — Aspire's documented pattern, giving local dev and the test harness the identical migration path) + `api` waiting on the runner's completion (local/test orchestration only).
- **Tooling baseline (partial pull-forward from M0):** `Directory.Build.props` (TFM, `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`) · `Directory.Packages.props` (central package management — assigned to this change by testing.md §1) · `.editorconfig` (copied from Jakub's reference project) · ~~CSharpier (format-on-build + committed IDE settings)~~ *(dropped 2026-07-15 — scoping decision 2 amendment; IDE0055 build errors are the format gate)* · BannedApiAnalyzers + `BannedSymbols.txt` (PRE-7 time discipline; Platform's clock composition is the sole, file-scoped exemption). Third-party analyzer packs (Meziantou/Sonar curation) stay in M0.
- **Minimal `.github/workflows/ci.yml`:** PR-triggered build + all three suites, publishes nothing. M0's `add-ci-cd-gates` refines it into the fast/harness split and the full ADR-0004 gate set.
- **Docs:** walk the testing.md **§9 verification checklist** items this change can reach and correct testing.md where reality disagrees; tick/annotate the roadmap M0 items this pull-forward absorbs (incl. that the FastEndpoints-on-net11 spike is de-facto exercised here).

## Scoping decisions (Jakub, 2026-07-15)

1. Minimal `ci.yml` lands here (option: wait for M0 — rejected).
2. Tooling split: `.editorconfig` + CSharpier + strict-analysis MSBuild props now; analyzer packs M0. *(Amended 2026-07-15: CSharpier dropped mid-change — `.editorconfig` is the sole formatting authority, enforced as IDE0055 build errors; design D12 amendment, tasks 7.5.)* *(Amended 2026-07-15 (2): Roslynator.Analyzers adopted scoped to RCS1002 only — one-line blocks lose braces; the analyzer-packs-M0 boundary otherwise stands, pack-wide review deferred to PRE-16; tasks 7.6.)*
3. Empty `DbContext` + initial migration **in** (makes §9.5 verifiable).
4. Auth depth: bearer + secure-by-default + identity-echo endpoint; no Entra, no `ActiveAccount` (needs M0's account table).
5. FluentValidation→Result bridge + SmartEnum converters **deferred to first consumer** — their founding-seed membership (conventions § SharedKernel discipline) is unchanged; they enter with the first validator / first SmartEnum.
6. Change ids are `cNNN-…` (PRE-12 amended — OpenSpec CLI rejects digit-leading names; landed with this change).

## Capabilities

### New Capabilities

- `shared-kernel-primitives`: behavioral contracts of the founding seed — Result union cases and their flow, rule-check composition semantics (violation aggregation, stages, sequential evaluation), entity/aggregate identity equality and domain-event raising, typed-id + uuid persistence semantics, dispatcher drain semantics (loop-until-empty, depth guard), integration-event publisher port contract.
- `error-contract`: the wire error model — every non-2xx response is ProblemDetails (RFC 9457); the Result-case → status matrix; the 409 `violations` shape with stable `<aggregate>.<rule>` codes; auth-middleware 401/403 as ProblemDetails; 500 never leaks internals.
- `api-shell`: the minimal API surface — endpoints secure by default, anonymous allowlisted DB-free health probe, authenticated identity-echo diagnostic.

### Modified Capabilities

(none — `openspec/specs/` is empty; this is the repo's first change)

## Impact

- **New:** `src/DoseUp.Api`, `src/DoseUp.MigrationService`, three `tests/` projects, root tooling files (incl. `global.json` selecting the Microsoft.Testing.Platform runner), `.github/workflows/ci.yml`; `DoseUp.slnx` gains five projects.
- **Modified:** AppHost resource graph (api + postgres, plus a test-mode seam) · `docs/conventions/testing.md` (§9-driven corrections, if any) · `docs/product/roadmap.md` (pull-forward annotations).
- **Dependencies (all centrally pinned; exact versions verified against current sources in design.md):** FastEndpoints · EF Core 11 previews + Npgsql · JwtBearer authentication · TUnit (Engine only — the metapackage would pull TUnit.Assertions) · Shouldly · ArchUnitNET (+ its TUnit adapter) · Microsoft.CodeAnalysis.BannedApiAnalyzers · ~~CSharpier~~ *(dropped 2026-07-15)* · Aspire.Hosting.PostgreSQL/Testing + the Npgsql EF client integration · Microsoft.IdentityModel.JsonWebTokens (test-token minting). `FakeTimeProvider` deliberately waits for its first consumer.
- **Config-rule notes (explicit, not skipped):** no "update infra Bicep" task — `infra/` does not exist until M0 and this AppHost change is local/test orchestration only (ADR-0004 split honored). No "regenerate TS client" task — the PRE-6 pipeline doesn't exist yet; the new endpoints enter `openapi.json` when M0 lands the export.

## Non-goals

- No business module or aggregate — the first domain module is the next change and validates these primitives.
- No Entra wiring: the test JWT authority is the only trust anchor; `ActiveAccount`, `CallerContext`, and the account table are M0.
- No Wolverine, outbox, ASB, or Azurite wiring — the `IIntegrationEventPublisher` port ships without its Platform implementation; M0's spikes own the emulator story.
- No `infra/`, no deploy, no `release.yml`.
- No TS client generation, no `openapi.json` export.
- No FluentValidation→Result bridge, no SmartEnum/EFCore converter (deferred to first consumer).
- No third-party analyzer packs (M0).
