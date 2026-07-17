# CLAUDE.md

## What this is

DoseUp — a medication & supplement dose tracker for an invite-only circle (family/friends), hosted by Jakub on Azure. Mobile-first React PWA + FastEndpoints API + Neon Postgres, orchestrated by .NET Aspire. The repo deliberately doubles as a **showcase of spec-driven, AI-assisted development** — process and documentation quality are first-class goals, not overhead.

## Read before deciding anything product- or architecture-shaped

- [docs/product/vision.md](docs/product/vision.md) — goals, non-goals, open questions
- [docs/product/requirements.md](docs/product/requirements.md) — FR-x / NFR-x (proposals cite these ids)
- [docs/product/roadmap.md](docs/product/roadmap.md) — milestones → openspec changes
- Open design decisions live as `decision`-labeled GitHub issues under the evergreen **"Design decisions"** parent issue; several may supersede what the ADRs currently say. Check them before treating an ADR as final; never act on one unprompted, and never reword one — Jakub kicks each off himself.
- [docs/adr/](docs/adr/) — 0001 platform/stack · 0002 architecture · 0003 testing · 0004 delivery/process
- [docs/conventions/](docs/conventions/) — **source of truth for conventions** (docs-first; tooling and `.claude/rules/` mirror it)
- [docs/conventions/project-management.md](docs/conventions/project-management.md) — work items, backlog, GitHub workflow
- `openspec/specs/` — precise behavioral truth once capabilities ship

Division of labor: product docs say **why**, specs say **precisely what**, conventions say **how**. Never duplicate — link. `docs/software-factory.md` is a meta decision catalog for Jakub's future project-setup repo: keep it updated when foundational decisions are made or reversed, but it does not govern this repo.

## Stack

.NET 11 previews for service/domain/test projects (`net11.0` + `LangVersion preview`); **AppHost stays net10** · Aspire 13.4 · FastEndpoints · React + Vite + TypeScript PWA · openapi-typescript + openapi-fetch (generated TS contract client — [ADR-0001-platform-and-stack](docs/adr/0001-platform-and-stack.md)) · Neon serverless Postgres (Aspire Postgres container locally) · EF Core 11 previews + Npgsql · Ardalis.SmartEnum (domain closed value sets) · Wolverine + Azure Service Bus Basic (async seam) · Storage Queue visibility alarms (reminders) · Entra External ID · Azure Container Apps · Bicep + Azure Deployment Stacks (hand-authored IaC — [ADR-0004-delivery-and-process](docs/adr/0004-delivery-and-process.md)) · GitHub Actions · TUnit + Shouldly · ArchUnitNET · @playwright/test.

## Working rules (non-negotiable)

- **Architecture is this project's highest priority.** Always think as a very senior software architect: never settle for what merely works — establish what is architecturally correct, and flag when the two diverge. Jakub is always the decision maker, Claude the senior-architect advisor: no architectural decision is adopted until Jakub understands and agrees with it.
- **Jakub gates stage progression.** Propose, summarize state, wait for his explicit go. Never declare an interview, phase, or plan complete on your own.
- Behavior changes go through **OpenSpec** (opsx skills). Trivial non-behavioral fixes may be direct.
- **Result end-to-end:** expected failures return the SharedKernel `union Result`, mapped to ProblemDetails at the edge. Exceptions only for bugs/infrastructure.
- **Authorization is layered and engine-free ([ADR-0002-architecture-style § Authorization](docs/adr/0002-architecture-style.md)):** edge policies gate active-account + admin (`CallerContext` is the only identity past Platform); profile-scoped data is enforced by account-scoped queries — cross-account access is `NotFound`/404 (anti-enumeration), `Forbidden`/403 only for request-class denials; the unauthenticated path (incl. health probes) never touches the database.
- **Sync path is framework-free; the async seam is Wolverine's.** No mediator — thin FastEndpoints endpoints (route/spec + auth + ProblemDetails mapping) directly inject **feature handlers**: plain framework-free classes owning the slice's DTOs, contract validation (first step), and orchestration, equally callable from Wolverine consumers ([ADR-0002-architecture-style](docs/adr/0002-architecture-style.md)); domain events dispatch synchronously inside the unit of work (the module's `DbContext` = UoW); integration events leave only via Wolverine's transactional outbox in the same transaction; consumers are idempotent ([ADR-0002-architecture-style](docs/adr/0002-architecture-style.md)).
- **Time & guards ([conventions/README.md § SharedKernel discipline](docs/conventions/README.md)):** never `DateTime.Now/UtcNow`/`DateTimeOffset.Now/UtcNow` outside Platform — inject `TimeProvider`; domain methods take `DateTimeOffset now` as a parameter; bug-guards are BCL throw-helpers + `UnreachableException` — no guard library, no custom `Guard` type.
- **Side effects & events ([ADR-0002-architecture-style § Events](docs/adr/0002-architecture-style.md)):** explicit orchestration in the feature handler is the default — a domain event only when the reaction must hold for every producer of the fact; handlers never publish integration events — aggregates raise facts, one translator per module publishes thin, id-only `Contracts.*` events via the SharedKernel publisher port; the domain-event dispatcher stays hand-rolled (~30 lines).
- **Domain rules ([conventions/domain-rules.md](docs/conventions/domain-rules.md)):** aggregates self-protect with pure, sync `CanXxx()` checks (static-first for projections); handlers compose `RuleSet` for aggregated violations (`RuleViolations` → 409, `violations` PD array); affordances are pure; set rules get a DB-constraint backstop; rule checks never run in parallel on one `DbContext`.
- ADR-0002's module/dependency rules are enforced by ArchUnitNET tests — **never weaken a failing architecture test to make it pass**; raise the conflict instead.
- Changes touching the API contract include an explicit "regenerate TS client" task; contract *modifications* (not pure additions) also update every TS consumer in the same change. CI only gates drift — it never generates or commits fixes.
- **Azure is defined only by hand-authored Bicep in `infra/`** (deployment stacks — [ADR-0004-delivery-and-process](docs/adr/0004-delivery-and-process.md)) — never the portal or ad-hoc CLI; the AppHost models local orchestration only, and changes touching its resource graph include an explicit "update infra Bicep" task. Migrations may be destructive (bundle in CD, maintenance-window recreate — never `Migrate()` at startup in prod).
- UI-heavy changes get a **Claude Design mockup + handoff** before implementation.
- **Testing ([conventions/testing.md](docs/conventions/testing.md)):** placement is a lookup, not a judgment call — feature handlers get slice tests over HTTP against the Aspire harness (no repositories ⇒ no honest seam to fake; EF InMemory/SQLite/`DbContext`-mocks banned); the unit layer is pure domain only; isolation by construction (each test mints its own account — no cleanup, no auto-retries); fake only what you don't own (Entra's signature, the wall clock).
- **Never log dose contents** — ids only (NFR-5).
- This project rides previews: **verify ecosystem facts against current sources** before recommending; model memory is stale by definition here.

## Commands

- Build: `dotnet build DoseUp.slnx`
- Test: `dotnet test DoseUp.slnx` (TUnit via Microsoft.Testing.Platform; test projects arrive in M0)
- Run/stop the app: `aspire start` / `aspire stop` via the aspire skills — never `dotnet run` the AppHost directly
- Frontend commands land with the web scaffold (M0/M1)

## Git

Trunk-based. **PRs always** (all gates always run), squash merge, PR title = Conventional Commit. Feature flags for incomplete work, each with a removal task. Commit or push only when Jakub asks.

Work items live in GitHub issues/milestones/project (`gh` CLI is the only channel — see [conventions/project-management.md](docs/conventions/project-management.md)); GitHub mutations only after Jakub signs off a plan.

## Claude memory

Auto-memory lives **in-repo** at `.claude/memory/` (committed). Per machine, `.claude/settings.local.json` (gitignored) must contain:

```json
{ "autoMemoryDirectory": "<absolute-repo-path>/.claude/memory" }
```

A canary MEMORY.md sits at the default global location — if its line ever appears in loaded memories, the redirect is not applied; fix the local setting before writing memories.
