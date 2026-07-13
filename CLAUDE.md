# CLAUDE.md

## What this is

DoseUp — a medication & supplement dose tracker for an invite-only circle (family/friends), hosted by Jakub on Azure. Mobile-first React PWA + FastEndpoints API + Neon Postgres, orchestrated by .NET Aspire. The repo deliberately doubles as a **showcase of spec-driven, AI-assisted development** — process and documentation quality are first-class goals, not overhead.

## Read before deciding anything product- or architecture-shaped

- [docs/product/vision.md](docs/product/vision.md) — goals, non-goals, open questions
- [docs/product/requirements.md](docs/product/requirements.md) — FR-x / NFR-x (proposals cite these ids)
- [docs/product/roadmap.md](docs/product/roadmap.md) — milestones → openspec changes
- [docs/product/pre-implementation-checklist.md](docs/product/pre-implementation-checklist.md) — PRE-x topics Jakub still wants to settle before implementation; several supersede what the ADRs currently say. Check it before treating an ADR as final; never act on an item, and never reword one — Jakub kicks each off himself.
- [docs/adr/](docs/adr/) — 0001 platform/stack · 0002 architecture · 0003 testing · 0004 delivery/process
- [docs/conventions/](docs/conventions/) — **source of truth for conventions** (docs-first; tooling and `.claude/rules/` mirror it)
- `openspec/specs/` — precise behavioral truth once capabilities ship

Division of labor: product docs say **why**, specs say **precisely what**, conventions say **how**. Never duplicate — link. `docs/software-factory.md` is a meta decision catalog for Jakub's future project-setup repo: keep it updated when foundational decisions are made or reversed, but it does not govern this repo.

## Stack

.NET 11 previews for service/domain/test projects (`net11.0` + `LangVersion preview`); **AppHost stays net10** · Aspire 13.4 · FastEndpoints · React + Vite + TypeScript PWA · Neon serverless Postgres (Aspire Postgres container locally) · Entra External ID · Azure Container Apps · GitHub Actions · TUnit + Shouldly · ArchUnitNET · @playwright/test.

## Working rules (non-negotiable)

- **Architecture is this project's highest priority.** Always think as a very senior software architect: never settle for what merely works — establish what is architecturally correct, and flag when the two diverge. Jakub is always the decision maker, Claude the senior-architect advisor: no architectural decision is adopted until Jakub understands and agrees with it.
- **Jakub gates stage progression.** Propose, summarize state, wait for his explicit go. Never declare an interview, phase, or plan complete on your own.
- Behavior changes go through **OpenSpec** (opsx skills). Trivial non-behavioral fixes may be direct.
- **Result end-to-end:** expected failures return the SharedKernel `union Result`, mapped to ProblemDetails at the edge. Exceptions only for bugs/infrastructure.
- ADR-0002's module/dependency rules are enforced by ArchUnitNET tests — **never weaken a failing architecture test to make it pass**; raise the conflict instead.
- Changes touching the API contract include an explicit "regenerate TS client" task; CI only gates drift.
- UI-heavy changes get a **Claude Design mockup + handoff** before implementation.
- **Never log dose contents** — ids only (NFR-5).
- This project rides previews: **verify ecosystem facts against current sources** before recommending; model memory is stale by definition here.

## Commands

- Build: `dotnet build DoseUp.slnx`
- Test: `dotnet test DoseUp.slnx` (TUnit via Microsoft.Testing.Platform; test projects arrive in M0)
- Run/stop the app: `aspire start` / `aspire stop` via the aspire skills — never `dotnet run` the AppHost directly
- Frontend commands land with the web scaffold (M0/M1)

## Git

Trunk-based. **PRs always** (all gates always run), squash merge, PR title = Conventional Commit. Feature flags for incomplete work, each with a removal task. Commit or push only when Jakub asks.

## Claude memory

Auto-memory lives **in-repo** at `.claude/memory/` (committed). Per machine, `.claude/settings.local.json` (gitignored) must contain:

```json
{ "autoMemoryDirectory": "<absolute-repo-path>/.claude/memory" }
```

A canary MEMORY.md sits at the default global location — if its line ever appears in loaded memories, the redirect is not applied; fix the local setting before writing memories.
