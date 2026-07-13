# DoseUp — Pre-implementation checklist

**Status:** living document · **Last updated:** 2026-07-13

Topics Jakub wants to consult and settle **before** implementation starts. Each item is a raw note — a reminder of a conversation to kick off, not a task to act on. Notes are recorded verbatim; nobody (human or agent) edits their wording. Work them one at a time, in an order Jakub picks.

Jakub is the pilot; Claude is the senior-architect advisor. Nothing here is decided until Jakub says so.

Each resolved item lands somewhere durable — an ADR, a convention doc, a requirement, or a roadmap change — and feeds Jakub's future **software factory** repo (see [software-factory.md](../software-factory.md)). Every completed item gets its own commit.

## Items

- [x] **PRE-1** — note the requirement of superb architectural quality
- [x] **PRE-2** — cosmos db -> neon (postgres)
- [ ] **PRE-3** — adr 1 - container apps jobs vs SB scheduled delivery
- [x] **PRE-4** — mediator (yes/no), wolverine, no anti-corruption layer by default, unit of work container
- [ ] **PRE-5** — react, radix, zustand
- [ ] **PRE-6** — TS client generation
- [ ] **PRE-7** — domain, business checks, side effects, integration events, smartenum
- [ ] **PRE-8** — testing organisation (unit, integration, e2e, architecture, contract)
- [ ] **PRE-9** — devops, branching strategy incl. neon
- [ ] **PRE-10** — permissions, RBAC, casbin.net
- [ ] **PRE-11** — re-org the docs/adrs/skills/claude.md/memory
- [ ] **PRE-12** — openspec change numbering
- [ ] **PRE-13** — setup codebase-memory-mcp
- [ ] **PRE-14** — design personas
- [ ] **PRE-15** — design code-review

## Outcomes

Filled in as items are processed — link the ADR / convention / spec / change that captured the decision.

| Item | Outcome |
| --- | --- |
| PRE-1 | 2026-07-13 — first working rule in [CLAUDE.md](../../CLAUDE.md): architecture is the highest priority, Claude reasons as a very senior architect, Jakub is always the decision maker; deposited as [software-factory F-42](../software-factory.md) |
| PRE-2 | 2026-07-13 — Neon serverless Postgres replaces Cosmos DB everywhere: [ADR-0001](../adr/0001-platform-and-stack.md) amended (+ ADR-0002 outbox, ADR-0003 test DB, ADR-0004 migrations), conventions, NFR-4/5/6, roadmap, vision, CLAUDE.md, openspec config; reversal + Neon-left-Azure finding recorded in [software-factory F-23](../software-factory.md) |
| PRE-3 | |
| PRE-4 | 2026-07-13 — no mediator (endpoint = handler); Wolverine (MIT) + CloudAMQP at the async seam only (MassTransit rejected: v9 commercial); EF Core 11 previews, `DbContext` = UoW, domain events sync in-UoW, integration events via Wolverine outbox in the same transaction; one-DTO payload rule, no ACL by default; scheduled messages rejected as reminder primitive — landed in [ADR-0001](../adr/0001-platform-and-stack.md) + [ADR-0002](../adr/0002-architecture-style.md), conventions, CLAUDE.md, openspec config; deposited as [software-factory F-43–F-46](../software-factory.md) |
| PRE-5 | |
| PRE-6 | |
| PRE-7 | |
| PRE-8 | |
| PRE-9 | |
| PRE-10 | |
| PRE-11 | |
| PRE-12 | |
| PRE-13 | |
| PRE-14 | |
| PRE-15 | |
