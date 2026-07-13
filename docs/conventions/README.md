# DoseUp — Engineering Conventions

**Status:** living document, docs-first · **Last updated:** 2026-07-13

This directory is the **source of truth for conventions** (decided in the founding interview): a convention is authored here first, then mirrored into enforcement (formatters, analyzers, architecture tests, CI gates) and into `.claude/rules/` so Claude follows it while writing code. If tooling can't express a rule, this doc is still authoritative. Changing a convention = PR touching this doc **and** its enforcement together.

Several sections below are deliberately skeletal — they get filled by the change that first makes them real (mostly M0/M1), never invented in advance.

## Formatting (decided)

- **C# layout is owned by CSharpier** — nobody hand-formats; the one-line-vs-multi-line question is answered by `printWidth`, not by style debates. Config lives in `.editorconfig` (CSharpier reads it). Also formats `.csproj`/XML.
- Enforcement: IDE format-on-save (committed `.vscode` settings) + `CSharpier.MsBuild` on build + CI check.
- **TypeScript/React:** ESLint + Prettier, CI-checked.
- No pre-commit hooks (deliberate — on-save/on-build/CI cover it).

## Static analysis (decided; packs finalized in M0)

- `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`.
- Curated third-party packs (candidates: Meziantou.Analyzer, SonarAnalyzer) tuned via `.editorconfig`; every disabled rule carries a comment saying why.

## Architecture (decided — see [ADR-0002](../adr/0002-architecture-style.md))

Modular monolith in one project; dependency rules 1–5 of ADR-0002 are enforced by ArchUnitNET tests. Module grades are declared. Cross-module = contracts + integration events only.

## API conventions (decided at policy level; matrix finalized in the first API change)

- FastEndpoints REPR; one endpoint per use-case slice. No mediator — the endpoint *is* the handler (PRE-4).
- Request/response DTOs are simultaneously the API contract and the handler payload — no anticipatory mapping layer; a separate internal type appears only when the public contract must stay stable across an internal change; DTOs never cross the domain boundary (PRE-4).
- **Every non-2xx response is ProblemDetails** (RFC 9457) — including FluentValidation 400s and Result-mapped domain errors.
- Result-case → status-code matrix (seed, to finalize): `NotFound → 404`, `Validation → 400`, `Conflict → 409`, `Forbidden → 403`, `Unexpected → 500` (never leaks internals).
- OpenAPI is the contract; TS types are generated, committed, and never hand-edited. API-touching changes regenerate them as an explicit task (CI drift gate verifies).
- Versioning: not before it hurts — revisit when the first breaking change threatens (record here).

## C# style beyond tooling (skeleton — fill in M0/M1)

Naming semantics (endpoints, handlers, ports, events) · file organization within a slice · when to extract a method/type · comment policy (constraints only) · `.claude/rules/` mirrors for path-scoped guidance.

## Persistence — Postgres (skeleton — ground rules in M1 design)

Decided (PRE-4): EF Core 11 previews + Npgsql; `DbContext` is the unit of work (no wrapper). Still to fill in M1 design: EF Core migration discipline (forward-safe expand/contract — ADR-0004) · id strategy · repository/port shape · what never goes in the database (secrets, oversized blobs).

## Events (decided — see ADR-0002)

Domain events: sync, in-module, in-UoW — drained by a `SaveChanges` interceptor and dispatched by the explicit DI dispatcher (PRE-4). Integration events: async, post-commit, via Wolverine's transactional outbox in the same transaction only; consumers idempotent (at-least-once). Naming: past tense (`DoseLogged`), payloads are contracts (versioned once cross-module).

## Testing conventions (skeleton — fill in M0 with the first real tests)

TUnit + Shouldly patterns · AAA structure · naming · what each pyramid layer is *for* (unit = domain behavior; integration = wiring + persistence semantics through the Aspire harness; E2E = user journeys; arch = ADR-0002 rules).

## Observability (skeleton — wire in M0)

OTel via ServiceDefaults everywhere · span/metric naming · **never log dose contents** (ids only — NFR-5) · correlation end-to-end.

## Git (decided — see [ADR-0004](../adr/0004-delivery-and-process.md))

Trunk-based, PRs always, squash merge, Conventional-Commit PR titles, branch naming `<type>/<change-id-or-topic>`, feature flags with removal tasks.
