# ADR-0002: Architecture style — hybrid modular monolith

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-13) · **Amended:** 2026-07-13 — persistence/outbox mechanics: Cosmos → Postgres (PRE-2)

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

1. `Domain` references only `SharedKernel` (never Features, Infrastructure, Platform, FastEndpoints, or data-access libraries — Npgsql, EF Core, …).
2. `Features` orchestrate their own module's `Domain` through its ports; they never touch another module's internals.
3. Cross-module communication happens **only** via public contracts and integration events — never direct calls into another module's Domain/Features/Infrastructure.
4. `Infrastructure` implements its module's ports; concrete adapters are seen only by `Platform` (composition root).
5. `SharedKernel` references nothing project-internal.

### Slices are the application layer

A use case = one folder in `Features/`. The handler *is* the application service — no MediatR, no separate application project. FastEndpoints' REPR model provides the request→handler→response shape.

### Events — two kinds, two rules

- **Domain events:** raised by aggregates, handled **synchronously within the same module and unit of work** (e.g. `DoseLogged` → update adherence projection).
- **Integration events:** the only asynchronous cross-module channel, published **after commit** via an **outbox**. Candidate design (M-phase): outbox row written in the **same database transaction** as the aggregate change; a background **dispatcher** publishes after commit (polling first; Postgres LISTEN/NOTIFY as a wake-up candidate). Transport starts in-process with a broker-shaped seam.

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
- The outbox/dispatcher design deserves its own design artifact (and possibly ADR) when the first integration event ships.
- Single project keeps builds/refactors fast; if the project ever splits, the namespace discipline maps 1:1 onto csproj boundaries.
