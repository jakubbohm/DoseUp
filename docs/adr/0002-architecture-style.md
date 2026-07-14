# ADR-0002: Architecture style — hybrid modular monolith

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-13) · **Amended:** 2026-07-13 — persistence/outbox mechanics: Cosmos → Postgres (PRE-2) · 2026-07-13 — dispatch, unit-of-work & messaging mechanics (PRE-4) · 2026-07-14 — authorization rings (PRE-10) · 2026-07-14 — feature-handler split (endpoints = thin adapters), validation placement, domain-rule model, integration-event production model (published-language translators) (PRE-7)

## Context

DoseUp is small enough for plain vertical slices, but the showcase and learning goals (vision G3/G4) call for explicit architectural discipline that architecture tests can police. Jakub's requirement: clean-architecture domain isolation per bounded context, vertical slices for use cases, and all architectural decisions made explicitly by him.

## Decision

A **modular monolith inside the single API project** (`src/DoseUp.Api`) — boundaries are namespaces + architecture tests, not project references.

```
src/DoseUp.Api/
├── SharedKernel/            # Result union, Error model, typed IDs,
│                            # RuleCheck/RuleViolation/RuleSet (PRE-7),
│                            # AggregateRoot + domain-event plumbing
├── Modules/
│   └── <Module>/            # one bounded context = one module
│       ├── Domain/          # clean core: aggregates, VOs, invariants → Result,
│       │                    # domain events, ports (interfaces)
│       ├── Features/        # vertical slices: 1 folder = 1 use case
│       │                    # (thin FastEndpoints endpoint + feature handler
│       │                    #  + validator + DTOs)
│       ├── Infrastructure/  # adapters implementing this module's ports:
│       │                    # Postgres persistence, push sender, clock, …
│       └── <Module>Module.cs  # DI registration + endpoint grouping + declared grade
└── Platform/                # composition root and cross-cutting infrastructure:
                             # database bootstrap, auth pipeline, ProblemDetails
                             # mapping, outbox dispatch, feature flags, OTel
```

### Dependency rules (the architecture-test contract)

1. `Domain` references only `SharedKernel` (never Features, Infrastructure, Platform, FastEndpoints, Wolverine, or data-access libraries — Npgsql, EF Core, …).
2. `Features` orchestrate their own module's `Domain` through its ports; they never touch another module's internals.
3. Cross-module communication happens **only** via public contracts and integration events — never direct calls into another module's Domain/Features/Infrastructure.
4. `Infrastructure` implements its module's ports; concrete adapters are seen only by `Platform` (composition root).
5. `SharedKernel` references nothing project-internal.
6. Within `Features` (PRE-7): endpoint classes contain no use-case logic — they adapt HTTP to the slice's feature handler; feature handlers and validators reference no FastEndpoints/ASP.NET types.

### Slices are the application layer

A use case = one folder in `Features/`. The **feature handler** — a plain class with **no FastEndpoints/HTTP references** — *is* the application service: it owns the slice's DTOs, runs contract validation as its first step, orchestrates its module's Domain through ports, composes domain-rule checks ([conventions/domain-rules.md](../conventions/domain-rules.md)), and commits the unit of work. The **endpoint is a thin adapter** — route + OpenAPI spec + auth policies + `Result`→ProblemDetails via the Platform mapper — that directly injects the handler; Wolverine consumers are the same thin-adapter shape on the async side (inbox/idempotency + handler call), making the handler the transport-independent unit *(PRE-7 — resolves the ambiguity between this ADR's original "the handler is the application service" and the "endpoint = handler" shorthand elsewhere, in favor of the former)*.

**No mediator of any kind on the synchronous path** (PRE-4, reaffirmed PRE-7): dispatch indirection would blind ArchUnitNET and IDE/AI navigation; direct constructor injection keeps navigation compile-time. Pipeline-behavior creep around handlers is the mediator through the back door — cross-cutting concerns live in ASP.NET middleware, the `SaveChanges` interceptor, or Wolverine middleware. No separate application project.

**Logic placement (PRE-7):** single-aggregate behavior → Domain; multi-aggregate *orchestration* → the feature handler; multi-aggregate *pure policy* (e.g. recurrence computation) → Domain as a domain service/static function — handlers coordinate, they don't own policy. A handler orchestrates only its own module; cross-module remains integration-events-only (rule 3).

**Payloads (PRE-4):** the slice's request/response DTOs are simultaneously the API contract and the handler payload — no anticipatory mapping layer. A separate internal payload type appears only when the public contract must stay stable across an internal change. DTOs stop at the domain boundary: aggregates take values through their methods, never DTOs. No anti-corruption layer by default — cross-module translation *is* the contracts + integration-events boundary (rule 3); a real ACL appears only when consuming a model we don't own.

### Events — two kinds, two rules

- **Domain events:** raised by aggregates, handled **synchronously within the same module and unit of work** (e.g. `DoseLogged` → update adherence projection).
- **Integration events:** the only asynchronous cross-module channel, published **after commit** via **Wolverine's transactional outbox** (PRE-4 — supersedes the hand-rolled candidate design): the envelope is written in the **same EF Core transaction** as the aggregate change; the sending agent dispatches **immediately post-commit** (no polling latency); startup recovery plus a relaxed periodic sweep backstop crashes and transient broker outages; delivery is at-least-once, so consumers (Wolverine handlers) are idempotent via the inbox. Transport: Azure Service Bus Basic queues via managed identity (PRE-3; local queues suffice until a second consumer or node needs the broker).
- **Production model (PRE-7): integration events are translations of domain facts — feature handlers never publish.** The aggregate raises the domain event at the point of state change; one **translator per module** (its published language) maps selected facts to `<Module>.Contracts` types (past tense, no suffix — the namespace is the marker) and publishes via the SharedKernel `IIntegrationEventPublisher` port, implemented in Platform over the outbox — the translator runs in the interceptor drain, so the envelope joins the same transaction. Announcement is thereby correct-by-construction for every producer of the fact, and each module has one choke point enforcing **thin, id-only payloads** (consumers re-query; NFR-5 extends to messages — dead-letter queues are browsable storage). Only translators and Platform reference the port (arch-tested). Same-module side effects default to explicit handler orchestration; a domain event is reserved for reactions that must hold for every producer.

### Unit of work & side effects (PRE-4)

One request = one scoped `DbContext` = one transaction — **the `DbContext` is the unit of work**; no `IUnitOfWork` wrapper on top. Aggregates raise domain events; a `SaveChanges` interceptor drains and dispatches them **synchronously before the save** through an explicit ~30-line DI dispatcher (loop until no new events, depth-guarded), so handler changes join the same transaction. *(Hand-rolled reconfirmed at PRE-7 against martinothamar/Mediator — healthy, source-generated, but a MediatR-shaped framework we'd use a sliver of — Wolverine's local bus — confined to outbox-outward by this ADR — and FastEndpoints' event bus — FE types would enter module signatures. The no-mediator rule bans caller→use-case indirection, not event fan-out, which is inherently one-to-many.)* Integration events become Wolverine outbox envelopes **in that same transaction**; commit is explicit in the handler. Boundary rule: same-module + must-be-consistent → domain event inside the UoW; cross-module or eventually-consistent → integration event via the outbox. Accepted cost: synchronous side effects ride the request path and a failing handler fails the operation — correct at this scale; anything that must not block belongs in the outbox.

### Per-module rigor: sliding scale, declared

Rich domains (e.g. Scheduling: DST-safe recurrence) get the full core. Trivial modules may be honest CRUD slices — but each module's `<Module>Module.cs`/README **declares its grade**, so simplicity is a decision, not drift.

### Validation: two layers, two channels *(placement revised PRE-7)*

Request shape → FluentValidation executed as the **feature handler's first step** (not FastEndpoints' automatic pipeline — the handler validates identically for every transport) → `Result` `Validation` → 400. Domain rules → `RuleCheck`/`RuleSet` per [conventions/domain-rules.md](../conventions/domain-rules.md) → `Result` `RuleViolations` → 409 with the aggregated `violations` array. Both channels map to ProblemDetails through the single Platform mapper.

### Authorization: three rings, engine-free (PRE-10)

No policy engine — the dominant check (ownership) is domain data, not policy (rationale and rejected alternatives in ADR-0001):

- **Ring 0 — authenticate (Platform):** Entra External ID JWT bearer; endpoints are secure by default, `AllowAnonymous` is an explicit, architecture-tested allowlist. Entra's `oid` claim is the account key (stable; `sub` is pairwise per app).
- **Ring 1 — account gate + role (Platform, declarative):** a default `ActiveAccount` policy on every endpoint resolves `oid` → account row and rejects unknown/revoked callers (403) — tokens outlive revocation, so FR-2 bites on the next request, not at token expiry. Resolution yields the request-scoped **`CallerContext`** (AccountId, IsAdmin) — the only identity type handlers and ports see; claims never travel past Platform. Admin endpoints live in one FastEndpoints group carrying the `AdminOnly` policy. Account status and the admin flag are **database columns, not Entra app roles**: the invite/revoke lifecycle is in-app (FR-2), changes take effect immediately, tests need no IdP.
- **Ring 2 — ownership by construction (modules):** every profile-scoped read/write flows through queries scoped to the caller's account (join/`EXISTS` from `CallerContext`); a miss surfaces as `Result` `NotFound`. There is no ownership check to forget — unowned data is unqueryable. Aggregates stay caller-free: they enforce invariants (PRE-7's territory), not access. Defense-in-depth candidate for M1 design: EF Core global query filters keyed by `CallerContext` (caveat: caller-less system paths — reminder firing, outbox consumers — need an explicit system context).

**Admin ≠ data access:** the admin role grants account-lifecycle powers only; there is no admin bypass of Ring 2 (NFR-5 stance).

**Denial semantics:** 401 unauthenticated · 403 only for request-class denials that leak nothing resource-specific (inactive account; non-admin on admin endpoints) · 404 for any profile-scoped resource the caller can't see — foreign-or-nonexistent deliberately indistinguishable (anti-enumeration; RFC 9110 permits 404 to conceal existence), including foreign profile ids referenced in payloads. The Result union's `Forbidden → 403` case is narrowed to "visible but not permitted" — dormant until FR-21-style permission levels.

**The unauthenticated path never touches the database** — JWT validation is local (cached OIDC metadata), the `ActiveAccount` lookup runs only after token validation, health probes are DB-free — so bot-triggered scale-from-zero wakes never reach Neon (cost consequences in ADR-0001).

**Testing (M3 `harden-authz`, first rows in M0):** a behavioral matrix — endpoint catalog × caller classes {anonymous, member-owner, member-other, admin, revoked} → expected status; cross-account probes assert 404 (not 403, not 2xx); revocation asserts 403 with a still-valid token; new endpoints fail the matrix until classified. ArchUnitNET owns the structural rules (anonymous allowlist; admin endpoints ∈ admin group); ownership is enforced behaviorally by the matrix. Matrix mechanics slot into PRE-8's test organisation.

The **module list is not fixed here** — bounded contexts (likely candidates: Access, Tracking, Scheduling) are decided in each change's design artifact.

## Alternatives considered

- **Plain vertical slices** — fastest to ship; rejected: nothing for architecture tests to police, bounded contexts stay implicit.
- **Clean/onion multi-project** — classic; rejected: project sprawl and layer hopping without added enforcement value (arch tests police namespaces just as well).
- **Project-per-module** — strongest isolation; rejected for solution overhead at this size. The namespace rules keep this reversible.

## Consequences

- Architecture tests (ArchUnitNET) are **the** boundary mechanism → they must exist from M0 and run as a PR gate. They enforce dependency rules 1–6 plus PRE-10's structural authorization rules (anonymous-endpoint allowlist; admin endpoints confined to the admin group).
- Framework magic is confined to the async seam: the synchronous request path stays framework-free (thin FastEndpoints endpoints in front of framework-free feature handlers — rule 6 makes this arch-tested, not aspirational; explicit domain-event dispatcher); Wolverine conventions apply only from the outbox outward. Wolverine.HTTP is the named alternative if this seam chafes (ADR-0001).
- The Wolverine adoption details (outbox configuration, inbox/idempotency conventions, KEDA queue-depth wake on ACA, `codegen write` for reviewable generated code) get their own design artifact when the first integration event ships.
- Single project keeps builds/refactors fast; if the project ever splits, the namespace discipline maps 1:1 onto csproj boundaries.
