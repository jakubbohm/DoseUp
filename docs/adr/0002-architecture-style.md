# ADR-0002: Architecture style — hybrid modular monolith

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-13) · **Amended:** 2026-07-13 — persistence/outbox mechanics: Cosmos → Postgres (PRE-2) · 2026-07-13 — dispatch, unit-of-work & messaging mechanics (PRE-4)

## Context

DoseUp is small enough for plain vertical slices, but the showcase and learning goals (vision G3/G4) call for explicit architectural discipline that architecture tests can police. Jakub's requirement: clean-architecture domain isolation per bounded context, vertical slices for use cases, and all architectural decisions made explicitly by him.

## Decision

A **modular monolith inside the single API project** (`src/DoseUp.Api`) — boundaries are namespaces + architecture tests, not project references.

```
src/DoseUp.Api/
├── SharedKernel/            # Result union, Error model, typed IDs,
│                            # AggregateRoot + domain-event plumbing
├── Modules/
│   └── <Module>/            # one bounded context = one module
│       ├── Domain/          # clean core: aggregates, VOs, invariants → Result,
│       │                    # domain events, ports (interfaces)
│       ├── Features/        # vertical slices: 1 folder = 1 use case
│       │                    # (FastEndpoints endpoint + validator + handler + DTOs)
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

### Slices are the application layer

A use case = one folder in `Features/`. The handler *is* the application service — **no mediator of any kind on the synchronous path** (PRE-4): dispatch indirection would blind ArchUnitNET and IDE/AI navigation, and every pipeline concern it once carried lives natively in FastEndpoints (validation, processors) or middleware (OTel). No separate application project. FastEndpoints' REPR model provides the request→handler→response shape.

**Payloads (PRE-4):** the slice's request/response DTOs are simultaneously the API contract and the handler payload — no anticipatory mapping layer. A separate internal payload type appears only when the public contract must stay stable across an internal change. DTOs stop at the domain boundary: aggregates take values through their methods, never DTOs. No anti-corruption layer by default — cross-module translation *is* the contracts + integration-events boundary (rule 3); a real ACL appears only when consuming a model we don't own.

### Events — two kinds, two rules

- **Domain events:** raised by aggregates, handled **synchronously within the same module and unit of work** (e.g. `DoseLogged` → update adherence projection).
- **Integration events:** the only asynchronous cross-module channel, published **after commit** via **Wolverine's transactional outbox** (PRE-4 — supersedes the hand-rolled candidate design): the envelope is written in the **same EF Core transaction** as the aggregate change; the sending agent dispatches **immediately post-commit** (no polling latency); startup recovery plus a relaxed periodic sweep backstop crashes and transient broker outages; delivery is at-least-once, so consumers (Wolverine handlers) are idempotent via the inbox. Transport: Azure Service Bus Basic queues via managed identity (PRE-3; local queues suffice until a second consumer or node needs the broker).

### Unit of work & side effects (PRE-4)

One request = one scoped `DbContext` = one transaction — **the `DbContext` is the unit of work**; no `IUnitOfWork` wrapper on top. Aggregates raise domain events; a `SaveChanges` interceptor drains and dispatches them **synchronously before the save** through an explicit ~30-line DI dispatcher (loop until no new events, depth-guarded), so handler changes join the same transaction. Integration events become Wolverine outbox envelopes **in that same transaction**; commit is explicit in the handler. Boundary rule: same-module + must-be-consistent → domain event inside the UoW; cross-module or eventually-consistent → integration event via the outbox. Accepted cost: synchronous side effects ride the request path and a failing handler fails the operation — correct at this scale; anything that must not block belongs in the outbox.

### Per-module rigor: sliding scale, declared

Rich domains (e.g. Scheduling: DST-safe recurrence) get the full core. Trivial modules may be honest CRUD slices — but each module's `<Module>Module.cs`/README **declares its grade**, so simplicity is a decision, not drift.

### Validation: two layers, two channels

Request shape → FluentValidation validators in FastEndpoints (automatic 400 ProblemDetails). Business invariants → domain code returning `Result` errors, mapped to ProblemDetails at the edge (ADR-0001).

The **module list is not fixed here** — bounded contexts (likely candidates: Access, Tracking, Scheduling) are decided in each change's design artifact.

## Alternatives considered

- **Plain vertical slices** — fastest to ship; rejected: nothing for architecture tests to police, bounded contexts stay implicit.
- **Clean/onion multi-project** — classic; rejected: project sprawl and layer hopping without added enforcement value (arch tests police namespaces just as well).
- **Project-per-module** — strongest isolation; rejected for solution overhead at this size. The namespace rules keep this reversible.

## Consequences

- Architecture tests (ArchUnitNET) are **the** boundary mechanism → they must exist from M0 and run as a PR gate.
- Framework magic is confined to the async seam: the synchronous request path stays framework-free (FastEndpoints endpoint = handler, explicit domain-event dispatcher); Wolverine conventions apply only from the outbox outward. Wolverine.HTTP is the named alternative if this seam chafes (ADR-0001).
- The Wolverine adoption details (outbox configuration, inbox/idempotency conventions, KEDA queue-depth wake on ACA, `codegen write` for reviewable generated code) get their own design artifact when the first integration event ships.
- Single project keeps builds/refactors fast; if the project ever splits, the namespace discipline maps 1:1 onto csproj boundaries.
