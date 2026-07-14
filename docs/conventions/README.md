# DoseUp — Engineering Conventions

**Status:** living document, docs-first · **Last updated:** 2026-07-14

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

## Authorization (decided — see ADR-0002 § Authorization)

Three rings, engine-free (PRE-10): endpoints secure by default — `AllowAnonymous` is an explicit, arch-tested allowlist · the `ActiveAccount` default policy resolves Entra `oid` → account row into the request-scoped `CallerContext`, the only identity type past Platform (account status + admin flag are DB columns, so revocation bites on the next request) · admin endpoints in one group with `AdminOnly` · all profile-scoped queries are account-scoped by construction; misses are `NotFound` · admin has no data access · the unauthenticated path (incl. health probes) never touches the database. AuthZ test matrix: M3 `harden-authz`, first rows in M0.

## API conventions (decided at policy level; matrix finalized in the first API change)

- FastEndpoints REPR; one endpoint per use-case slice. No mediator — the endpoint *is* the handler (PRE-4).
- Request/response DTOs are simultaneously the API contract and the handler payload — no anticipatory mapping layer; a separate internal type appears only when the public contract must stay stable across an internal change; DTOs never cross the domain boundary (PRE-4).
- **Every non-2xx response is ProblemDetails** (RFC 9457) — including FluentValidation 400s and Result-mapped domain errors.
- Result-case → status-code matrix: `NotFound → 404`, `Validation → 400`, `Conflict → 409`, `Forbidden → 403`, `Unexpected → 500` (never leaks internals). Denial semantics (PRE-10): cross-account access to profile-scoped resources — including foreign profile ids in payloads — is `NotFound`/404, indistinguishable from nonexistence by design (anti-enumeration); `Forbidden`/403 is reserved for request-class denials (inactive account, non-admin on admin endpoints) and later FR-21-style "visible but not permitted". Auth-middleware 401/403 responses are ProblemDetails too (mechanism wired + verified in M0).
- OpenAPI is the contract (PRE-6): FastEndpoints exports `openapi.json` via `--exportswaggerjson` (committed); openapi-typescript generates the TS types file (committed, never hand-edited); the web app calls through openapi-fetch, whose `{ data, error }` result continues the Result pattern into TS. One script does export + regenerate; API-touching changes run it as an explicit task (CI drift-gates both artifacts).
- Wire payloads are plain object literals, never classes (structured clone and React state punish instances) and never hand-written wire types — the generated types are the only TS source of contract truth. React hooks binding (openapi-react-query) and any fluent facade layer: decided at PRE-5.
- Versioning: not before it hurts — revisit when the first breaking change threatens (record here).

## C# style beyond tooling (skeleton — fill in M0/M1)

Naming semantics (endpoints, handlers, ports, events) · file organization within a slice · when to extract a method/type · comment policy (constraints only) · `.claude/rules/` mirrors for path-scoped guidance.

## Persistence — Postgres (migrations/seeding decided; ground rules in M1 design)

Decided (PRE-4): EF Core 11 previews + Npgsql; `DbContext` is the unit of work (no wrapper).

Decided (PRE-9) — migrations & seeding:

- **Migrations may be destructive** — no expand/contract requirement; a deploy carrying migrations takes a maintenance-window recreate (ADR-0004). Applied in CD via the self-contained **migration bundle**; never `Database.Migrate()` at startup in prod; local dev auto-applies on `aspire start` (mechanics land M1).
- **Seeding, three tiers by post-insert ownership:** static catalogs → `HasData` (model managed data: versioned with migrations, identical everywhere per version, explicit PKs) · one-time global seed → manual data motion inside a migration (`migrationBuilder.InsertData`/`.Sql()` — runs once per database via the history table, later drift untouched) · dev/test data → `UseSeeding`/`UseAsyncSeeding`, registered **only** in the dev/test composition root (fires on `Migrate` even with nothing pending; implement both sync and async — tooling calls the sync one).

Still to fill in M1 design: id strategy · repository/port shape · what never goes in the database (secrets, oversized blobs).

## Events (decided — see ADR-0002)

Domain events: sync, in-module, in-UoW — drained by a `SaveChanges` interceptor and dispatched by the explicit DI dispatcher (PRE-4). Integration events: async, post-commit, via Wolverine's transactional outbox in the same transaction only; consumers idempotent (at-least-once). Naming: past tense (`DoseLogged`), payloads are contracts (versioned once cross-module).

## Testing conventions (skeleton — fill in M0 with the first real tests)

TUnit + Shouldly patterns · AAA structure · naming · what each pyramid layer is *for* (unit = domain behavior; integration = wiring + persistence semantics through the Aspire harness; E2E = user journeys; arch = ADR-0002 rules) · the authZ matrix (endpoint catalog × caller classes — ADR-0002 § Authorization; lands M3, first rows M0).

## Observability (skeleton — wire in M0)

OTel via ServiceDefaults everywhere · span/metric naming · **never log dose contents** (ids only — NFR-5) · correlation end-to-end.

## Git (decided — see [ADR-0004](../adr/0004-delivery-and-process.md))

Trunk-based, PRs always, squash merge, Conventional-Commit PR titles, branch naming `<type>/<change-id-or-topic>`, feature flags with removal tasks. Every openspec change auto-branches off the freshest main at creation (openspec config rule, PRE-9).

## Infrastructure & delivery (decided — see ADR-0004, PRE-9)

Azure is defined **only** by hand-authored Bicep in `infra/` + per-environment `.bicepparam`, applied as an Azure Deployment Stack (removals in git delete in Azure) — no portal or ad-hoc CLI mutations, ever. GitHub Actions authenticates via OIDC federated credentials; services use managed identities with minimal RBAC roles assigned in Bicep. The Aspire AppHost models local orchestration only — a change touching its resource graph includes an explicit "update infra Bicep + `.bicepparam`" task (mirror of the PRE-6 contract rule). PR CI publishes nothing; `release.yml` builds once from the merge commit and promotes identical artifacts.
