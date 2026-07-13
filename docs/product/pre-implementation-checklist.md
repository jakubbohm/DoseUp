# DoseUp — Pre-implementation checklist

**Status:** living document · **Last updated:** 2026-07-13

Topics Jakub wants to consult and settle **before** implementation starts. Each item is a raw note — a reminder of a conversation to kick off, not a task to act on. Notes are recorded verbatim; nobody (human or agent) edits their wording. Work them one at a time, in an order Jakub picks.

Jakub is the pilot; Claude is the senior-architect advisor. Nothing here is decided until Jakub says so.

Each resolved item lands somewhere durable — an ADR, a convention doc, a requirement, or a roadmap change — and feeds Jakub's future **software factory** repo (see [software-factory.md](../software-factory.md)).

## Items

- [x] **PRE-1** — note the requirement of superb architectural quality
- [ ] **PRE-2** — cosmos db -> neon (postgres)
- [ ] **PRE-3** — adr 1 - container apps jobs vs SB scheduled delivery
- [ ] **PRE-4** — mediator (yes/no), wolverine, no anti-corruption layer by default, unit of work container
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
| PRE-2 | |
| PRE-3 | |
| PRE-4 | |
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
