# DoseUp — Pre-implementation checklist

**Status:** living document · **Last updated:** 2026-07-14

Topics Jakub wants to consult and settle **before** implementation starts. Each item is a raw note — a reminder of a conversation to kick off, not a task to act on. Notes are recorded verbatim; nobody (human or agent) edits their wording. Work them one at a time, in an order Jakub picks.

Jakub is the pilot; Claude is the senior-architect advisor. Nothing here is decided until Jakub says so.

Each resolved item lands somewhere durable — an ADR, a convention doc, a requirement, or a roadmap change — and feeds Jakub's future **software factory** repo (see [software-factory.md](../software-factory.md)). Every completed item gets its own commit.

## Items

- [x] **PRE-1** — note the requirement of superb architectural quality
- [x] **PRE-2** — cosmos db -> neon (postgres)
- [x] **PRE-3** — adr 1 - container apps jobs vs SB scheduled delivery
- [x] **PRE-4** — mediator (yes/no), wolverine, no anti-corruption layer by default, unit of work container
- [ ] **PRE-5** — react, radix, zustand, TanStack vs generated facade
- [x] **PRE-6** — TS client generation
- [ ] **PRE-7** — domain, business checks, side effects, integration events, smartenum
- [ ] **PRE-8** — testing organisation (unit, integration, e2e, architecture, contract)
- [x] **PRE-9** — devops, branching strategy incl. neon
- [x] **PRE-10** — permissions, RBAC, casbin.net
- [ ] **PRE-11** — re-org the docs/adrs/skills/claude.md/memory
- [x] **PRE-12** — openspec change numbering
- [ ] **PRE-13** — setup codebase-memory-mcp
- [ ] **PRE-14** — design personas
- [ ] **PRE-15** — design code-review

## Outcomes

Filled in as items are processed — link the ADR / convention / spec / change that captured the decision.

| Item | Outcome |
| --- | --- |
| PRE-1 | 2026-07-13 — first working rule in [CLAUDE.md](../../CLAUDE.md): architecture is the highest priority, Claude reasons as a very senior architect, Jakub is always the decision maker; deposited as [software-factory F-42](../software-factory.md) |
| PRE-2 | 2026-07-13 — Neon serverless Postgres replaces Cosmos DB everywhere: [ADR-0001](../adr/0001-platform-and-stack.md) amended (+ ADR-0002 outbox, ADR-0003 test DB, ADR-0004 migrations), conventions, NFR-4/5/6, roadmap, vision, CLAUDE.md, openspec config; reversal + Neon-left-Azure finding recorded in [software-factory F-23](../software-factory.md) |
| PRE-3 | 2026-07-13 — reminder triggers = Storage Queue visibility-timeout alarms + KEDA `visibleonly` wake (ACA cron rejected at this scale; ASB Standard scheduled messages = the recorded paid upgrade path behind the `IReminderAlarm` port); transport consolidated CloudAMQP → ASB Basic (managed identity; M0 spike gates it); cost outlook ≈ €0/month — landed in [ADR-0001](../adr/0001-platform-and-stack.md)/[ADR-0002](../adr/0002-architecture-style.md), CLAUDE.md, openspec config, NFR-6, roadmap; deposited as [software-factory F-47](../software-factory.md) (+ F-44 revised) |
| PRE-4 | 2026-07-13 — no mediator (endpoint = handler); Wolverine (MIT) + CloudAMQP at the async seam only (MassTransit rejected: v9 commercial); EF Core 11 previews, `DbContext` = UoW, domain events sync in-UoW, integration events via Wolverine outbox in the same transaction; one-DTO payload rule, no ACL by default; scheduled messages rejected as reminder primitive — landed in [ADR-0001](../adr/0001-platform-and-stack.md) + [ADR-0002](../adr/0002-architecture-style.md), conventions, CLAUDE.md, openspec config; deposited as [software-factory F-43–F-46](../software-factory.md) |
| PRE-5 | |
| PRE-6 | 2026-07-13 — contract pipeline: FastEndpoints `--exportswaggerjson` → committed `openapi.json` → openapi-typescript → committed TS types, consumed via openapi-fetch (`{ data, error }` continues the Result union into TS); one regen script, explicit task in API-touching changes, CI drift-gates both artifacts; Kiota/FE.ClientGen.Kiota rejected on generated-output evidence (erases required-ness, `@ts-ignore`d output, throws vs Result edge, TS pipeline preview), hey-api = recorded fallback; hooks binding (openapi-react-query) + facade question moved to PRE-5 (reworded by Jakub) — landed in [ADR-0001](../adr/0001-platform-and-stack.md), [conventions](../conventions/README.md), roadmap M0, CLAUDE.md, openspec config; deposited as [software-factory F-22 firmed + new F-48](../software-factory.md) |
| PRE-7 | |
| PRE-8 | |
| PRE-9 | 2026-07-14 — delivery pipeline settled end-to-end: every openspec change auto-branches off freshest main (config.yaml rule); squash merge re-examined and confirmed (1 change = 1 revertable Conventional Commit; corollary: PR-head SHA never lands on main); **infra = hand-authored Bicep + Azure Deployment Stacks** (`action-on-unmanage deleteResources` — git deletions delete in Azure; per-env `.bicepparam`; GH→Azure OIDC federated creds; managed identities + minimal RBAC in Bicep; no portal/CLI mutations) — aspire publish/azd generated Bicep rejected on verification (C#-lambda escape hatches, no per-env parameterization, azd stacks alpha, regen clobbers customization ⇒ drift gate inexpressible; sync = PRE-6-style process rule + deploy smoke); CI/CD split: `ci.yml` = gates only, publishes nothing; `release.yml` builds once from the merge commit and promotes identical artifacts — infra stack → pre-deploy Neon branch → EF **migration bundle** → rollout → smoke; **expand/contract mandate dropped**: deploys carrying migrations take a maintenance-window recreate, migrations may be destructive, instant revert weakens to branch-restore-or-roll-forward (accepted at circle scale); Neon branching rejected for dev/CI (Aspire Postgres containers everywhere — ADR-0003) but adopted as instant pre-deploy restore points (Free-plan restore window = 6 h; prune to last 3) + scheduled `pg_dump` to Azure Blob (vendor-independent DR, OQ-4 drill in M3); EF seeding three tiers by post-insert ownership: `HasData` = static catalogs · manual migration data motion = one-time global seed · `UseSeeding`/`UseAsyncSeeding` = dev/test-only composition — landed in [ADR-0004](../adr/0004-delivery-and-process.md) (rewritten), [ADR-0001](../adr/0001-platform-and-stack.md) (amended), [conventions](../conventions/README.md), roadmap M0, CLAUDE.md, openspec config; deposited as [software-factory F-53–F-57](../software-factory.md) (+ F-23/F-31 refined) |
| PRE-10 | 2026-07-14 — layered, engine-free authorization ("three rings"): FastEndpoints secure-by-default + ASP.NET named policies at the edge (`ActiveAccount`: Entra `oid` → DB account row → request-scoped `CallerContext`; `AdminOnly` group; status/admin flag in DB so revocation bites next request), ownership **by construction** via account-scoped queries → `NotFound` (cross-account = 404 anti-enumeration, `Forbidden` narrowed to request-class denials), admin ≠ data access, unauthenticated path incl. health probes never touches the DB (authN can't gate ACA scale-from-zero — verified: auth sidecar is per-replica — so bot wakes are priced-in cents and Neon sleeps; Cloudflare front = recorded contingency); casbin.net rejected **on fit, not health** (v2.21.2 active, Apache-2.0, ran on net11 preview 5 — spike: complete ruleset ≈ 30 lines of runtime DSL vs two C# booleans, ownership needs the DB row loaded anyway), OpenFGA/SpiceDB = FR-21-scale escape hatch; M3 `harden-authz` = endpoint × caller-class matrix — landed in [ADR-0001](../adr/0001-platform-and-stack.md)/[ADR-0002](../adr/0002-architecture-style.md), [conventions](../conventions/README.md), NFR-4, roadmap M0/M3, CLAUDE.md, openspec config; deposited as [software-factory F-49–F-52](../software-factory.md) |
| PRE-11 | |
| PRE-12 | 2026-07-13 — change ids get a 3-digit sequential prefix (NNN-kebab-name, never resets) — rule in [openspec/config.yaml](../../openspec/config.yaml) (context + proposal rule), noted in roadmap intro and software-factory F-37 |
| PRE-13 | |
| PRE-14 | |
| PRE-15 | |
