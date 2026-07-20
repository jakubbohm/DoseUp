# DoseUp — Roadmap

**Status:** living document · **Last updated:** 2026-07-19

A milestone is a coherent, shippable slice delivered through OpenSpec changes. When a change is archived: tick it here and update the `Status` column in [requirements.md](requirements.md). Candidate change names are just that — candidates; the actual slicing is decided when each change is proposed. Final change ids carry a letter-prefixed 3-digit sequence (naming rule in [openspec/config.yaml](../../openspec/config.yaml), amended 2026-07-15 — the OpenSpec CLI requires letter-first ids), e.g. `c001-add-shared-kernel`.

**Sequencing note (2026-07-14):** the domain-layer foundations run ahead of the M0 walking skeleton: the domain-layer and testing-organisation design interviews ([conventions/domain-rules.md](../conventions/domain-rules.md), [conventions/testing.md](../conventions/testing.md)) → a SharedKernel + test-infrastructure change (pulling those pieces forward from M0 scope) → the first domain module as its validation slice. The domain layer is framework-free (pure C# + TUnit), so nothing in M0 gates it; the consciously accepted trade-off is that this code lands on main before the pipe (CI gates, deploy) is proven end-to-end.

## M0 — Walking skeleton on Azure

**Goal:** a skeleton app with one real use case, live on Azure — the entire pipe (code → gates → prod) proven by the **Membership** module serving its first vertical slice, *get account detail*.

Scope is decomposed into GitHub issues — the [M0 milestone](https://github.com/jakubbohm/DoseUp/milestone/1) is the authoritative work breakdown. Strategically: the Aspire-wired app skeleton (API + React PWA + Postgres), the Membership module with its *get account detail* slice — the account table lands inside its rightful module from day 1, also serving the `ActiveAccount` edge policy and removing the `Platform/Persistence` bootstrap placeholder from c001 — Entra sign-in end-to-end, the contract pipeline, the full CI/CD gate set with hand-authored Bicep deployment stacks, and the Wolverine go/no-go spikes. `c001-add-shared-kernel` already landed the SharedKernel seed, the three test projects with the arch-test catalog, and a minimal PR-gate `ci.yml` (the FastEndpoints-on-preview spike de-facto passed there).

**Done when:** Jakub signs in on his phone against the live prod URL and sees his own account detail served by the real Membership slice; a deliberately broken PR is demonstrably blocked by each gate class.

Delivers: FR-1 (partial), FR-15 (shell) · foundations of NFR-4/7/8 · validates OQ-1 (first bill)

- [ ] change(s) proposed
- [ ] archived

## M1 — Core loop: log & see

**Goal:** DoseUp becomes Jakub's daily dose logger.

Scope: profiles (FR-3), substances CRUD with archive semantics (FR-4, FR-5), ≤ 2-interaction logging (FR-6, FR-7, FR-8), history timeline (FR-9). The **Membership** module skeleton and the per-module Postgres schema/migration baseline arrive with M0; M1 adds the remaining profile/substance/logging slices on top of it plus **Scheduling** — completing the initial module set fixed 2026-07-15 (capability-renamed 2026-07-16; a floor, not a ceiling; [ADR-0002-architecture-style](../adr/0002-architecture-style.md)); first Claude Design mockup + handoff for the logging UI.

**Done when:** Jakub logs every real dose through the app for a week without friction notes.

Candidate changes: `add-profiles`, `add-substances`, `add-dose-logging`, `add-history-view`

- [ ] changes proposed
- [ ] archived

## M2 — Schedules & reminders

**Goal:** G1 — DoseUp replaces memory.

Scope: schedule model + DST-safe recurrence engine (FR-10, FR-11 — heavy domain unit-testing), server-side reminder triggers that work while the app is scaled down (NFR-3 — storage-queue visibility alarms per [ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md); fine-grained design in the change), web push subscribe/delivery with taken/skip/snooze (FR-12), due/overdue view (FR-13). Adherence (FR-14) if it fits.

**Done when:** four consecutive weeks of G1 behavior (reminder-triggered logging).

Candidate changes: `add-schedules`, `add-reminder-engine`, `add-web-push`, `add-due-view`

- [ ] changes proposed
- [ ] archived

## M3 — Circle release

**Goal:** first external users (G2).

Scope: account admin — revoke plus the OQ-5 admission decision (FR-2), onboarding + PWA install UX polish (FR-15 finished, OQ-3 validated, incl. polishing the complete-signup flow — FR-1), adherence view (FR-14) if not in M2, authZ hardening tests (NFR-4 — the authorization matrix completed per [conventions/testing.md § 4](../conventions/testing.md): kind classification, completeness gate, payload-embedded foreign-id probes), backup restore verified (OQ-4), cost check (OQ-1), "not medical advice" note (NFR-5), language decision (OQ-2).

**Done when:** ≥ 2 non-Jakub users active for 2+ consecutive weeks without support.

Candidate changes: `add-account-admin`, `harden-authz`, `polish-onboarding`

- [ ] changes proposed
- [ ] archived

## Backlog (unscheduled)

FR-16 stock tracking · FR-17 effects journal · FR-18 export · FR-19 shared catalog · FR-20 offline (won't, revisit) · FR-21 caregiver sharing (won't, revisit) · Czech i18n (OQ-2) · Stryker mutation testing (spike deferred out of c002, the first domain-module change — re-slotted into a later domain-heavy change, M2's recurrence engine at the latest; [conventions/testing.md § 7](../conventions/testing.md); if viable → nightly, never a PR gate) · the Claude Design workflow maturing (`/design-sync` push-up + handoff-back — [conventions/design.md](../conventions/design.md))

## Standing activities (no milestone)

- **Preview riding:** each monthly .NET 11 preview and Aspire release gets a dedicated, CI-gated upgrade PR promptly ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md))
- **Dependency hygiene:** bot-driven update PRs once M0 wiring exists
