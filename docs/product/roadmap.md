# DoseUp — Roadmap

**Status:** living document · **Last updated:** 2026-07-13

A milestone is a coherent, shippable slice delivered through OpenSpec changes. When a change is archived: tick it here and update the `Status` column in [requirements.md](requirements.md). Candidate change names are just that — candidates; the actual slicing is decided when each change is proposed. Final change ids carry a 3-digit sequential prefix (PRE-12), e.g. `001-add-walking-skeleton`.

## M0 — Walking skeleton on Azure

**Goal:** prove the entire pipe — code → gates → prod — before building features on it.

Scope:

- Aspire AppHost wires the real stack: FastEndpoints API (`net11.0` preview) + React/Vite PWA shell + Neon Postgres (Aspire Postgres container locally), ServiceDefaults/OTel on everything
- Tooling baseline: CSharpier + `.editorconfig` + strict analyzers, ESLint + Prettier, TUnit + Shouldly + ArchUnitNET + Playwright scaffolds, module skeleton with first architecture tests
- SharedKernel seed: `union`-based `Result`, Error model, ProblemDetails mapping
- Entra External ID sign-in end-to-end: SPA login → API validates token → response proves identity and one database round-trip
- OpenAPI → TS types generation wired (committed output + drift check)
- GitHub Actions: all PR gates from [ADR-0004](../adr/0004-delivery-and-process.md); merge to main deploys the single prod environment on Azure Container Apps
- FastEndpoints- and Wolverine-on-net11-preview compatibility spikes, incl. Wolverine on ASB Basic queue-only (fallbacks: pin the last working preview; EF Core 10 GA as the data-access pin; CloudAMQP if Basic can't carry Wolverine)

**Done when:** Jakub signs in on his phone against the live prod URL and sees his identity echoed through the full stack; a deliberately broken PR is demonstrably blocked by each gate class.

Delivers: FR-1 (partial), FR-15 (shell) · foundations of NFR-4/7/8 · validates OQ-1 (first bill)

Candidate changes: `add-app-skeleton`, `add-entra-auth`, `add-ci-cd-gates` (or one combined `add-walking-skeleton`)

- [ ] change(s) proposed
- [ ] archived

## M1 — Core loop: log & see

**Goal:** DoseUp becomes Jakub's daily dose logger.

Scope: profiles (FR-3), substances CRUD with archive semantics (FR-4, FR-5), ≤ 2-interaction logging (FR-6, FR-7, FR-8), history timeline (FR-9). First real module(s) and the Postgres schema/migration baseline land here; first Claude Design mockup + handoff for the logging UI.

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

Scope: invite/revoke admin (FR-2), onboarding + PWA install UX polish (FR-15 finished, OQ-3 validated), adherence view (FR-14) if not in M2, authZ hardening tests (NFR-4), backup restore verified (OQ-4), cost check (OQ-1), "not medical advice" note (NFR-5), language decision (OQ-2).

**Done when:** ≥ 2 non-Jakub users active for 2+ consecutive weeks without support.

Candidate changes: `add-invites-admin`, `harden-authz`, `polish-onboarding`

- [ ] changes proposed
- [ ] archived

## Backlog (unscheduled)

FR-16 stock tracking · FR-17 effects journal · FR-18 export · FR-19 shared catalog · FR-20 offline (won't, revisit) · FR-21 caregiver sharing (won't, revisit) · Czech i18n (OQ-2) · Stryker mutation testing (pending TUnit spike, ADR-0003) · Claude Design ↔ DesignSync design-system sync maturing

## Standing activities (no milestone)

- **Preview riding:** each monthly .NET 11 preview and Aspire release gets a dedicated, CI-gated upgrade PR promptly ([ADR-0001](../adr/0001-platform-and-stack.md))
- **Dependency hygiene:** bot-driven update PRs once M0 wiring exists
