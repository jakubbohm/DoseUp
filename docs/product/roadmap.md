# DoseUp — Roadmap

**Status:** living document · **Last updated:** 2026-07-15

A milestone is a coherent, shippable slice delivered through OpenSpec changes. When a change is archived: tick it here and update the `Status` column in [requirements.md](requirements.md). Candidate change names are just that — candidates; the actual slicing is decided when each change is proposed. Final change ids carry a letter-prefixed 3-digit sequence (PRE-12, amended 2026-07-15 — the OpenSpec CLI requires letter-first ids), e.g. `c001-add-shared-kernel`.

**Sequencing note (2026-07-14):** the domain-layer foundations run ahead of the M0 walking skeleton: PRE-7/PRE-8 design interviews → a SharedKernel + test-infrastructure change (pulling those pieces forward from M0 scope) → the first domain module as its validation slice. The domain layer is framework-free (pure C# + TUnit), so nothing in M0 gates it; the consciously accepted trade-off is that this code lands on main before the pipe (CI gates, deploy) is proven end-to-end.

## M0 — Walking skeleton on Azure

**Goal:** prove the entire pipe — code → gates → prod — before building features on it.

Scope:

- Aspire AppHost wires the real stack: FastEndpoints API (`net11.0` preview) + React/Vite PWA shell + Neon Postgres (Aspire Postgres container locally), ServiceDefaults/OTel on everything
- Tooling baseline: `.editorconfig`-owned formatting (IDE0055 as build error) + strict analyzers, ESLint + Prettier, TUnit + Shouldly + ArchUnitNET + Playwright scaffolds — three per-layer test projects and the full arch-test catalog shipping vacuously green per [conventions/testing.md](../conventions/testing.md) (PRE-8), plus its §9 verification checklist *(largely landed by `c001-add-shared-kernel`: .editorconfig-owned formatting (CSharpier adopted, then dropped 2026-07-15 mid-change — conventions § Formatting) + strict analysis, three test projects, the 16-rule catalog, §9 walked — remaining for M0: analyzer packs, ESLint/Prettier, Playwright scaffold)*
- SharedKernel seed: `union`-based `Result`, Error model, `RuleCheck`/`RuleSet` domain-rule primitives, domain-event + integration-publisher ports (PRE-7), ProblemDetails mapping *(landed by `c001-add-shared-kernel`, incl. the single Result→ProblemDetails mapper and the empty-DbContext migration path)*
- Entra External ID sign-in end-to-end: SPA login → API validates token → response proves identity and one database round-trip — the round-trip *is* PRE-10's `ActiveAccount` resolution (Entra `oid` → account row → `CallerContext`); health probes stay DB-free so bot-triggered wakes never touch Neon
- Contract pipeline wired (PRE-6): FastEndpoints `--exportswaggerjson` → committed `openapi.json` → openapi-typescript types + openapi-fetch client, one regen script, CI drift check on both artifacts
- GitHub Actions (PRE-9): `ci.yml` = all PR gates from [ADR-0004](../adr/0004-delivery-and-process.md), publishing nothing; `release.yml` builds once from the merge commit and deploys prod — hand-authored `infra/` Bicep applied as a deployment stack (OIDC federated login) → pre-deploy Neon branch → EF migration bundle (maintenance-window recreate only when migrations are pending) → image rollout → smoke — plus the scheduled `pg_dump`-to-Blob DR job (restore drill lands M3/OQ-4)
- FastEndpoints- and Wolverine-on-net11-preview compatibility spikes, incl. Wolverine on ASB Basic queue-only, **the per-module message-store go/no-go** (2026-07-15 — Wolverine must persist its outbox/inbox/saga/scheduled-message state per module, in the module's schema, envelope writes joining the module context's transaction — hard precondition per [ADR-0002 § Events](../adr/0002-architecture-style.md); fail ⇒ Wolverine rejected and the async seam re-decided), **and Wolverine × ASB emulator inside the Aspire test harness** (PRE-8 — fallback: Wolverine stub transport for slice tests + deployed smoke covers real ASB); other fallbacks: pin the last working preview; EF Core 10 GA as the data-access pin; CloudAMQP if Basic can't carry Wolverine *(the FastEndpoints spike is de-facto passed in `c001-add-shared-kernel`: FE 8.2.0 builds, boots, and serves authenticated endpoints over HTTP on the preview-6 runtime; a minimal PR-gate `ci.yml` also landed there — M0 refines it into the fast/harness split and full ADR-0004 gate set)*

**Done when:** Jakub signs in on his phone against the live prod URL and sees his identity echoed through the full stack; a deliberately broken PR is demonstrably blocked by each gate class.

Delivers: FR-1 (partial), FR-15 (shell) · foundations of NFR-4/7/8 · validates OQ-1 (first bill)

Candidate changes: `add-app-skeleton`, `add-entra-auth`, `add-ci-cd-gates` (or one combined `add-walking-skeleton`)

- [ ] change(s) proposed
- [ ] archived

## M1 — Core loop: log & see

**Goal:** DoseUp becomes Jakub's daily dose logger.

Scope: profiles (FR-3), substances CRUD with archive semantics (FR-4, FR-5), ≤ 2-interaction logging (FR-6, FR-7, FR-8), history timeline (FR-9). First real module(s) — drawn from the initial module set fixed 2026-07-15 (**Membership**, **Scheduling** — capability-renamed 2026-07-16; a floor, not a ceiling; [ADR-0002](../adr/0002-architecture-style.md)) — and the per-module Postgres schema/migration baseline land here; whichever change lands the first module context also removes the `Platform/Persistence` bootstrap placeholder (the empty `DoseUpDbContext` + `Initial` migration that proved c001's schema pipeline); first Claude Design mockup + handoff for the logging UI.

**Done when:** Jakub logs every real dose through the app for a week without friction notes.

Candidate changes: `add-profiles`, `add-substances`, `add-dose-logging`, `add-history-view`

- [ ] changes proposed
- [ ] archived

## M2 — Schedules & reminders

**Goal:** G1 — DoseUp replaces memory.

Scope: schedule model + DST-safe recurrence engine (FR-10, FR-11 — heavy domain unit-testing), server-side reminder triggers that work while the app is scaled down (NFR-3 — storage-queue visibility alarms per PRE-3; fine-grained design in the change), web push subscribe/delivery with taken/skip/snooze (FR-12), due/overdue view (FR-13). Adherence (FR-14) if it fits.

**Done when:** four consecutive weeks of G1 behavior (reminder-triggered logging).

Candidate changes: `add-schedules`, `add-reminder-engine`, `add-web-push`, `add-due-view`

- [ ] changes proposed
- [ ] archived

## M3 — Circle release

**Goal:** first external users (G2).

Scope: invite/revoke admin (FR-2), onboarding + PWA install UX polish (FR-15 finished, OQ-3 validated), adherence view (FR-14) if not in M2, authZ hardening tests (NFR-4 — PRE-10 matrix completed per [conventions/testing.md §4](../conventions/testing.md): kind classification, completeness gate, payload-embedded foreign-id probes), backup restore verified (OQ-4), cost check (OQ-1), "not medical advice" note (NFR-5), language decision (OQ-2).

**Done when:** ≥ 2 non-Jakub users active for 2+ consecutive weeks without support.

Candidate changes: `add-invites-admin`, `harden-authz`, `polish-onboarding`

- [ ] changes proposed
- [ ] archived

## Backlog (unscheduled)

FR-16 stock tracking · FR-17 effects journal · FR-18 export · FR-19 shared catalog · FR-20 offline (won't, revisit) · FR-21 caregiver sharing (won't, revisit) · Czech i18n (OQ-2) · Stryker mutation testing (spike scheduled into the first domain-module change — PRE-8; if viable → nightly, never a PR gate) · Claude Design ↔ DesignSync design-system sync maturing

## Standing activities (no milestone)

- **Preview riding:** each monthly .NET 11 preview and Aspire release gets a dedicated, CI-gated upgrade PR promptly ([ADR-0001](../adr/0001-platform-and-stack.md))
- **Dependency hygiene:** bot-driven update PRs once M0 wiring exists
