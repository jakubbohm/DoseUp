# DoseUp — Product Requirements

**Status:** living document · **Last updated:** 2026-07-13

Scope-level requirements only — *what*, not precise behavior. Precise, testable behavior lives in `openspec/specs/` and grows change by change. OpenSpec proposals **must cite the FR/NFR ids** they serve (rule in `openspec/config.yaml`).

Priorities: MoSCoW (**M**ust / **S**hould / **C**ould / **W**on't-for-now). Status: `Idea` → `Specced` (link to capability) → `Done`.

## Functional requirements

### Accounts & access

| ID | Prio | Requirement | Status |
|----|------|-------------|--------|
| FR-1 | M | Users sign in via Microsoft Entra External ID; only invited/approved accounts can use the app | Idea |
| FR-2 | M | The instance admin can invite new accounts and revoke access | Idea |
| FR-3 | M | An account contains one or more profiles (self, child, …); every substance, schedule, and dose belongs to exactly one profile; switching the active profile is instant | Idea |

### Substances

| ID | Prio | Requirement | Status |
|----|------|-------------|--------|
| FR-4 | M | A profile's substances are user-defined (freeform): name, strength + unit, default dose amount, optional notes. No external drug database | Idea |
| FR-5 | M | Substances with logged history are archived, never deleted, so history stays intact | Idea |

### Dose logging & history

| ID | Prio | Requirement | Status |
|----|------|-------------|--------|
| FR-6 | M | Logging a routine dose takes ≤ 2 interactions from opening the app (substance with prefilled default amount and "now") | Idea |
| FR-7 | M | Amount and timestamp are adjustable at log time; entries can be backdated, edited, and deleted | Idea |
| FR-8 | M | Both scheduled and ad-hoc (unscheduled) doses can be logged | Idea |
| FR-9 | M | History timeline per profile, filterable by substance and date range | Idea |

### Schedules & reminders

| ID | Prio | Requirement | Status |
|----|------|-------------|--------|
| FR-10 | M | Per substance, a profile can define schedules: dose amount, times of day, days of week, optional start/end dates | Idea |
| FR-11 | M | Schedule times are local to the profile's time zone and remain correct across DST transitions | Idea |
| FR-12 | M | Web-push reminders fire at scheduled times; acting on a reminder logs the dose as taken, skips, or snoozes it (notification actions where the platform supports them, otherwise via the due view) | Idea |
| FR-13 | M | A due/overdue view in the app shows pending doses (also serves users who decline push) | Idea |
| FR-14 | S | Adherence overview per schedule: taken / skipped / missed over time | Idea |

### PWA

| ID | Prio | Requirement | Status |
|----|------|-------------|--------|
| FR-15 | M | Installable PWA (manifest, icons, service worker), designed mobile-first | Idea |

### Later (backlog, not scheduled)

| ID | Prio | Requirement | Status |
|----|------|-------------|--------|
| FR-16 | C | Stock tracking: remaining amount per substance, "runs out in N days", low-stock alert | Idea |
| FR-17 | C | Effects journal: notes/symptoms attached to doses or days; simple pattern view | Idea |
| FR-18 | C | Per-profile data export (JSON/CSV) | Idea |
| FR-19 | C | Shared/curated substance catalog, or copying substances between profiles | Idea |
| FR-20 | W | Offline logging with sync — revisit only if online-only demonstrably hurts | Idea |
| FR-21 | W | Cross-account caregiver sharing (granting another account access to a profile) | Idea |

## Non-functional requirements

| ID | Requirement |
|----|-------------|
| NFR-1 | **Platform reach:** current Android Chrome and iOS Safari (≥ 16.4, installed to home screen for push) |
| NFR-2 | **Performance:** the core loop feels instant; API reads P95 < 300 ms at circle scale (≤ 50 accounts); app-open → dose-logged < 10 s on a mid-range phone over LTE |
| NFR-3 | **Reliability:** best-effort availability (hobby operations, no SLO) — but reminder computation and delivery are server-side, so an idle client or scaled-down app never loses them |
| NFR-4 | **Security:** HTTPS only; every API call authenticated (Entra External ID); per-account authorization enforced server-side and covered by tests; secrets never in the repo — managed identity where available, the Neon connection string via ACA secrets/Key Vault |
| NFR-5 | **Privacy:** encryption at rest (Azure + Neon); automated backups with point-in-time restore (Neon); dose contents never appear in logs/telemetry (ids only); in-app "not medical advice" note |
| NFR-6 | **Cost:** fits a hobby budget at circle scale — working target ≤ €20/month (OQ-1): Neon serverless Postgres + CloudAMQP free tiers (2026-07), minimal compute footprint |
| NFR-7 | **Quality & process:** behavior changes flow through OpenSpec; test pyramid and gates per [ADR-0003](../adr/0003-testing-stack.md) and [ADR-0004](../adr/0004-delivery-and-process.md) |
| NFR-8 | **Observability:** OpenTelemetry end-to-end via ServiceDefaults — traces, metrics, logs usable in the Aspire dashboard (dev) and Azure (prod) |
| NFR-9 | **i18n readiness:** UI strings externalized; English first, Czech possible later (OQ-2) |

## Traceability

- Roadmap milestones reference the FR ids they deliver: [roadmap.md](roadmap.md)
- OpenSpec proposals cite FR/NFR ids and the milestone (enforced by `openspec/config.yaml` rules)
- When a change is archived: update the `Status` column here and tick the roadmap
