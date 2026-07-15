# DoseUp — Engineering Conventions

**Status:** living document, docs-first · **Last updated:** 2026-07-15

This directory is the **source of truth for conventions** (decided in the founding interview): a convention is authored here first, then mirrored into enforcement (formatters, analyzers, architecture tests, CI gates) and into `.claude/rules/` so Claude follows it while writing code. If tooling can't express a rule, this doc is still authoritative. Changing a convention = PR touching this doc **and** its enforcement together.

Several sections below are deliberately skeletal — they get filled by the change that first makes them real (mostly M0/M1), never invented in advance.

## Formatting (decided; formatter reversed 2026-07-15)

- **C# layout is owned by `.editorconfig`** — the file is the single formatting authority (Jakub's reference style: end-of-line braces, 2-space indent, CRLF). CSharpier, the founding pick, was **dropped 2026-07-15**: it reads only the indent/width/EOL basics from `.editorconfig` and hard-codes the rest — Allman braces, forced final newline — so the owner's layout was inexpressible, and coexistence had forced `IDE0055 = none`, silencing exactly the rules that do express it.
- **Enforcement: the build is the format gate** — layout violations are IDE0055 **build errors** (`EnforceCodeStyleInBuild` + `TreatWarningsAsErrors`; probe-verified to fire). Fixer: `dotnet format whitespace DoseUp.slnx`. IDE format-on-save (committed `.vscode` settings, C# extension) applies the same rules.
- **Line width: soft ~200-char guideline, deliberately unenforced** (`max_line_length` declares it; no tool reads it). The guideline's positive half: **a statement that fits stays on one line** — a line break expresses structure (a fluent stage list, a multi-property initializer, one condition per line), never a printer's default. The accepted trade of dropping CSharpier: no deterministic width-based wrapping, and `.csproj`/XML formatting is hand-kept.
- **One-line blocks carry no braces** (decided 2026-07-15); the statement sits on the next line, indented. Enforcement is bidirectional: Roslynator **RCS1002** owns the remove direction (build error; fires only when the single statement fits one line — the pack is otherwise off, see Static analysis) and IDE0011 (`when_multiline`) owns the add direction for multi-line bodies. The next-line *placement* itself is convention, not tooling (`allow_embedded_statements_on_same_line` stays permissive).
- **TypeScript/React:** ESLint + Prettier, CI-checked — unchanged: Prettier's opinions are adopted wholesale there, which is exactly the condition CSharpier failed on the C# side.
- No pre-commit hooks (deliberate — on-save/on-build/CI cover it).

## Static analysis (decided; packs finalized in M0)

- `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`.
- Curated third-party packs (candidates: Meziantou.Analyzer, SonarAnalyzer) tuned via `.editorconfig`; every disabled rule carries a comment saying why.
- **Roslynator.Analyzers is installed but scoped to a single rule** (RCS1002, one-line brace removal — see Formatting): the `category-Roslynator` kill-switch keeps its other 200+ rules off until **PRE-16 ("Review roslynator config")** decides how much of the pack to adopt.
- **Microsoft.CodeAnalysis.BannedApiAnalyzers** enforces the PRE-7 time discipline: `BannedSymbols.txt` bans `DateTime.Now/UtcNow` and `DateTimeOffset.Now/UtcNow` (Platform's clock composition is the sole exemption) — lands with the shared-kernel change.

## Architecture (decided — see [ADR-0002](../adr/0002-architecture-style.md))

Modular monolith in one project; dependency rules 1–5 of ADR-0002 are enforced by ArchUnitNET tests. Module grades are declared. Cross-module = contracts + integration events only.

## Authorization (decided — see ADR-0002 § Authorization)

Three rings, engine-free (PRE-10): endpoints secure by default — `AllowAnonymous` is an explicit, arch-tested allowlist · the `ActiveAccount` default policy resolves Entra `oid` → account row into the request-scoped `CallerContext`, the only identity type past Platform (account status + admin flag are DB columns, so revocation bites on the next request) · admin endpoints in one group with `AdminOnly` · all profile-scoped queries are account-scoped by construction; misses are `NotFound` · admin has no data access · the unauthenticated path (incl. health probes) never touches the database. AuthZ test matrix: M3 `harden-authz`, first rows in M0.

## API conventions (decided at policy level; matrix finalized in the first API change)

- One vertical slice per use case: a **thin FastEndpoints endpoint** (route + OpenAPI spec + auth policies + `Result`→ProblemDetails via the Platform mapper) directly injecting the slice's **feature handler** — a plain class owning the DTOs, contract validation (first step), and use-case orchestration, with **no FastEndpoints/HTTP references** (ArchUnitNET rule 6); Wolverine consumers call the same handlers on the async side (PRE-4, refined PRE-7 — resolves the "endpoint *is* the handler" shorthand). Still **no mediator**: direct constructor injection, compile-time navigation, no pipeline behaviors — behavior-creep around handlers is the mediator through the back door.
- Contract validation (FluentValidation) runs as the handler's first step for every transport → `Result` `Validation` → 400; FE's automatic validator pipeline stays unused (plain `AbstractValidator<T>`, which FE does not auto-bind — expected mechanic, verify in M0).
- Request/response DTOs are simultaneously the API contract and the handler payload — no anticipatory mapping layer; a separate internal type appears only when the public contract must stay stable across an internal change; DTOs never cross the domain boundary (PRE-4).
- **Every non-2xx response is ProblemDetails** (RFC 9457) — including FluentValidation 400s and Result-mapped domain errors.
- Result-case → status-code matrix: `NotFound → 404`, `Validation → 400`, `RuleViolations → 409` (domain rules — aggregated `violations` array per [domain-rules.md](domain-rules.md), PRE-7), `Conflict → 409` (reserved for concurrency/double-submit; same status, distinct PD `type`), `Forbidden → 403`, `Unexpected → 500` (never leaks internals). Denial semantics (PRE-10): cross-account access to profile-scoped resources — including foreign profile ids in payloads — is `NotFound`/404, indistinguishable from nonexistence by design (anti-enumeration); `Forbidden`/403 is reserved for request-class denials (inactive account, non-admin on admin endpoints) and later FR-21-style "visible but not permitted". Auth-middleware 401/403 responses are ProblemDetails too (mechanism wired + verified in M0).
- OpenAPI is the contract (PRE-6): FastEndpoints exports `openapi.json` via `--exportswaggerjson` (committed); openapi-typescript generates the TS types file (committed, never hand-edited); the web app calls through openapi-fetch, whose `{ data, error }` result continues the Result pattern into TS. One script does export + regenerate; API-touching changes run it as an explicit task (CI drift-gates both artifacts). **Regeneration is always dev-side, inside the change** — CI re-runs the generator only to *compare* and fails on mismatch; it never generates or commits fixes. A change that **modifies** an existing contract (not a pure addition) also updates every TS consumer of it — the facade layer if PRE-5 adopts one, and all usages — in the same change, so main never carries a frontend broken against the committed client (refined during PRE-8).
- Wire payloads are plain object literals, never classes (structured clone and React state punish instances) and never hand-written wire types — the generated types are the only TS source of contract truth. React hooks binding (openapi-react-query) and any fluent facade layer: decided at PRE-5.
- Versioning: not before it hurts — revisit when the first breaking change threatens (record here).

## C# style beyond tooling (skeleton — fill in M0/M1)

Naming semantics (endpoints, handlers, ports, events) · file organization within a slice · when to extract a method/type · comment policy (constraints only) · `.claude/rules/` mirrors for path-scoped guidance.

## SharedKernel discipline (decided — PRE-7)

- **Guards (bug-class checks only):** BCL throw-helpers — `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, the `ArgumentOutOfRangeException.ThrowIf*` family — plus `UnreachableException` for impossible branches. **No guard library, no custom `Guard` type** (CommunityToolkit.Diagnostics and ardalis/GuardClauses rejected on fit, not health: they predate the BCL helpers and now sell synonyms; Roslyn analyzers push the BCL idiom anyway — one idiom, zero deps). Rule-of-three escape: a repeated bespoke guard earns a tiny SharedKernel `Guard` then. Boundary reminder ([domain-rules.md](domain-rules.md) §1): guards protect against *bugs* (throw); rules and validation protect against *expected states* (`Result`).
- **Time discipline:** never `DateTime.Now/UtcNow` or `DateTimeOffset.Now/UtcNow` outside Platform — the BCL `TimeProvider` is the injectable clock (no custom `IClock`). Sharper still: **aggregates never see `TimeProvider` — domain methods take `DateTimeOffset now` as a parameter** (the handler reads the injected clock and passes the instant), keeping domain logic a pure function of its inputs — load-bearing for M2's DST-safe recurrence testing. Enforced by BannedApiAnalyzers (see Static analysis).
- **The negative list — deliberately *not* in SharedKernel:** no repository base (PRE-7), no specification pattern, no CQRS marker interfaces, no `DomainException` (expected failures are `Result`; bugs throw standard exceptions), no ValueObject base (records are the mechanism), no domain-specific value objects (`Quantity`/`DoseUnit` belong to their module). Growth rule: **something enters SharedKernel only when a second module needs it** — the founding seed (Result, Error model, RuleCheck/RuleSet, typed-id + SmartEnum converters, Entity/AggregateRoot, event + publisher ports, FluentValidation→Result bridge) is the exemption.

## Domain model base types (decided — PRE-7)

- **Entities:** `Entity<TId>` base (identity-based equality). **Aggregate roots:** `AggregateRoot<TId> : Entity<TId>` — owns the domain-event collection (protected `Raise(...)`; drained by the `SaveChanges` interceptor per ADR-0002) — plus a non-generic `IAggregateRoot` marker it implements, which is what ArchUnitNET rules and generic constraints key on (e.g., only aggregate roots exposed as `DbSet`).
- **Value objects are plain C# records — no base class.** Record structural equality *is* value-object semantics; `GetEqualityComponents()` bases are pre-record legacy.
- **Strongly-typed ids:** one `readonly record struct XxxId(Guid Value)` per aggregate, hand-written (no source-gen library — SharedKernel stays dependency-free). Rationale: below the edge C# goes positional, and the account-scoped `Where` clauses that *are* ring-3 authorization (PRE-10, no repository layer) must not compile when ids are swapped. The unwrapped edge is confined to one wrap line per id per endpoint, adjacent to the named DTO property.
- **Id generation: client-side `Guid.CreateVersion7()` in the aggregate factory** — the aggregate is born with its identity, so domain events raised at construction and same-transaction outbox messages carry real ids with no save-order dependency; v7 time-ordering keeps Postgres B-tree inserts append-mostly. Stored as native `uuid` (16 bytes, never strings) via one generic value converter. Contract DTOs stay plain `Guid`.

## Domain rules & error model (decided — PRE-7)

The full design lives in **[domain-rules.md](domain-rules.md)**: the error taxonomy (which mechanism owns which error class), `RuleCheck`/`RuleViolation`/`RuleSet`, two-layer checking (aggregate = guarantee, handler = courtesy), pure affordances with the static-first projection form, sequential-never-parallel evaluation (DbContext), the 409 `violations` ProblemDetails contract, and the set-rule constraint backstop.

## Domain enumerations (decided — PRE-7)

- **Every closed value set in the domain layer is an [Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum); C# `enum` is banned in domain assemblies.** Enforced by an ArchUnitNET rule (lands with the shared-kernel change). No behavior-richness threshold to judge — the rule is uniform on purpose.
- **Persistence: the int `Value`**, via the `Ardalis.SmartEnum.EFCore` converter. Values are explicit, **append-only, never renumbered, never reused** — the failure mode bought with int storage is silent meaning-drift, and this discipline is the price.
- **Contract edge: DTOs use plain C# enums** so `openapi.json` emits real enum schemas → openapi-typescript literal unions (PRE-6). The endpoint maps DTO enum ↔ SmartEnum; unmappable values surface as `Result` validation failures, never exceptions.
- **SmartEnum vs C# union:** SmartEnum for closed *value sets* (named instances, lookup, per-instance data/behavior); unions for closed *shape alternatives* (`Result`). SmartEnum has no exhaustive-switch checking — prefer polymorphic behavior *on* the instances over switching *over* them.

## Persistence — Postgres (migrations/seeding decided; ground rules in M1 design)

Decided (PRE-4): EF Core 11 previews + Npgsql; `DbContext` is the unit of work (no wrapper).

Decided (PRE-7): **no repository layer** — the `DbContext` is the data-access API as well as the UoW; endpoints and domain services query it directly (account-scoped by construction, PRE-10). An abstraction over data access appears only when a genuine second implementation exists — never anticipatory.

Decided (PRE-9) — migrations & seeding:

- **Migrations may be destructive** — no expand/contract requirement; a deploy carrying migrations takes a maintenance-window recreate (ADR-0004). Applied in CD via the self-contained **migration bundle**; never `Database.Migrate()` at startup in prod; local dev auto-applies on `aspire start` (mechanics land M1).
- **Seeding, three tiers by post-insert ownership:** static catalogs → `HasData` (model managed data: versioned with migrations, identical everywhere per version, explicit PKs) · one-time global seed → manual data motion inside a migration (`migrationBuilder.InsertData`/`.Sql()` — runs once per database via the history table, later drift untouched) · dev/test data → `UseSeeding`/`UseAsyncSeeding`, registered **only** in the dev/test composition root (fires on `Migrate` even with nothing pending; implement both sync and async — tooling calls the sync one).

Decided (PRE-7) — ids: client-generated Guid v7 wrapped in per-aggregate typed ids, stored as native `uuid` via one generic converter (see *Domain model base types*).

Still to fill in M1 design: what never goes in the database (secrets, oversized blobs).

## Events & side effects (decided — ADR-0002; production model PRE-7)

- **Domain events:** sync, in-module, in-UoW — drained by a `SaveChanges` interceptor and dispatched by the explicit **hand-rolled** DI dispatcher (`IDomainEvent`/`IDomainEventHandler<T>` in SharedKernel; PRE-4, reaffirmed PRE-7: the no-mediator rule bans caller→use-case indirection, not one-to-many fan-out — and we'd use a sliver of any library; martinothamar/Mediator rejected on fit not health, Wolverine local bus confined to outbox-outward, FE event bus puts FE types in module signatures).
- **Side-effect placement:** explicit orchestration in the feature handler is the **default**. A domain event is justified only when the reaction must hold for *every* producer of the fact, forever (invariant coupling is the cause; multiple producers is the symptom).
- **Integration events are translated domain facts — feature handlers never publish.** Aggregates raise domain events where state changes; one **translator per module** (its published language, e.g. `TrackingPublishedLanguage`) maps selected facts to public contracts and publishes through the SharedKernel `IIntegrationEventPublisher` port (Platform implements it over Wolverine's outbox — translator runs in the interceptor drain, envelope joins the same transaction). Only translators and Platform reference the port (arch-tested). Escape hatch for a message that is genuinely not a domain fact: direct port use from a handler — expected ~never; each occurrence is a review flag.
- **Payloads are thin and id-only:** ids + the facts that changed, never full state (at-least-once delivery makes stale fat payloads a correctness trap — consumers re-query). **NFR-5 extends to event payloads**: dead-letter queues are browsable storage, so no dose contents in messages, ids only.
- **Naming:** past tense; domain event = plain name in Domain (`DoseLogged`); integration contract = same name in `<Module>.Contracts` — the namespace is the marker, no `IntegrationEvent` suffix. Payloads are contracts (versioned once cross-module). Consumers idempotent (at-least-once).

## Testing conventions (decided — PRE-8)

Everything lives in **[testing.md](testing.md)**: layout (three per-layer test projects, path-mirrored) · the placement decision tree ("I wrote X, where does its test go?" — feature handlers = slice tests over HTTP, no repositories ⇒ no honest seam to fake) · Aspire harness mechanics (one session-shared AppHost; isolation **by construction** — each test its own account, no cleanup; real JWT pipeline with a test trust anchor; ASB emulator + Azurite) · authZ-matrix mechanics (reflection census × kind classification, completeness gate) · the architecture-test catalog (16 rules, single enforcement owner each, incl. ADR-0002 rule 7 slice independence) · style (behavior-sentence names, AAA, hand-rolled builders, Shouldly confirmed against TUnit.Assertions) · CI cadence (fast job / harness job split; zero auto-retry, quarantine with paper trail). Headline bans: EF InMemory · SQLite-in-memory · mocked `DbContext` · mocking frameworks by default — **fake only what you don't own** (Entra's signature, the wall clock).

## Observability (skeleton — wire in M0)

OTel via ServiceDefaults everywhere · span/metric naming · **never log dose contents** (ids only — NFR-5) · correlation end-to-end.

## Git (decided — see [ADR-0004](../adr/0004-delivery-and-process.md))

Trunk-based, PRs always, squash merge, Conventional-Commit PR titles, branch naming `<type>/<change-id-or-topic>`, feature flags with removal tasks. Every openspec change auto-branches off the freshest main at creation (openspec config rule, PRE-9).

## Infrastructure & delivery (decided — see ADR-0004, PRE-9)

Azure is defined **only** by hand-authored Bicep in `infra/` + per-environment `.bicepparam`, applied as an Azure Deployment Stack (removals in git delete in Azure) — no portal or ad-hoc CLI mutations, ever. GitHub Actions authenticates via OIDC federated credentials; services use managed identities with minimal RBAC roles assigned in Bicep. The Aspire AppHost models local orchestration only — a change touching its resource graph includes an explicit "update infra Bicep + `.bicepparam`" task (mirror of the PRE-6 contract rule). PR CI publishes nothing; `release.yml` builds once from the merge commit and promotes identical artifacts.
