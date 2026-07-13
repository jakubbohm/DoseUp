# DoseUp — Vision

**Owner:** Jakub Bohm · **Status:** living document · **Last updated:** 2026-07-13

## Problem

People who take medications or supplements regularly keep the schedule in their heads or in notes: *Did I take the morning dose? When did I last take it? How long until I run out?* The result is missed doses, accidental double doses, and refill surprises. Existing tracker apps tend to be ad-funded, subscription-gated, built around US drug databases, or cavalier with what is ultimately health data.

## Product

DoseUp is a **mobile-first, installable web app (PWA)** for logging doses and getting reminded — hosted privately by Jakub for an **invite-only circle** of family and friends. Its promise: logging a routine dose takes at most two interactions, reminders arrive reliably as push notifications, and the person running the service is someone you personally trust.

## Users

- **Jakub** — primary user, admin, and host.
- **The circle** — household, family, friends invited onto the same instance. No public signup.
- One **account** can hold multiple **profiles** — e.g. a parent tracking their own and a child's doses. All data belongs to a profile.

## Goals

| ID | Goal | Target |
|----|------|--------|
| G1 | **Daily driver** — DoseUp replaces memory and notes for Jakub's own regimen: every regular dose is logged or consciously skipped, reminders are trusted | end of M2 |
| G2 | **Circle adoption** — invited users onboard without hand-holding and keep using it | M3 onward |
| G3 | **Engineering showcase** — the repo demonstrates exemplary AI-assisted, spec-driven development: OpenSpec workflow, modular-monolith discipline, full test pyramid, trunk-based CD. Presentable as a reference | continuous |
| G4 | **Learning** — hands-on depth in .NET 11 previews (C# 15 unions), Aspire, Neon Postgres, Entra External ID, Web Push, TUnit, and the Claude Design workflow | continuous |

## Non-goals

- **Not a medical device.** No dosing advice, no interaction checking, no clinical claims. The app shows a "not medical advice" note.
- No public SaaS, open registration, or multi-tenant operations.
- No offline-first sync (online-only; the due list is readable, logging needs connectivity).
- No native app-store clients.
- No cross-account caregiver sharing in v1 — dependents are modeled as profiles under one account (revisit later, see FR-21).
- No monetization.

## Success criteria

- **G1:** four consecutive weeks in which every scheduled dose of Jakub's regimen is logged or explicitly skipped through the app, with reminders as the trigger.
- **G2:** at least two non-Jakub users active for two or more consecutive weeks without support requests.
- **G3:** any shipped behavior can be traced requirement → spec → change → code → test; CI has stayed the merge authority (no bypassed gates).
- **G4:** each learning-goal technology has shipped inside a real feature slice, not just a spike.

## Open questions

| ID | Question | Decide by |
|----|----------|-----------|
| OQ-1 | Monthly Azure cost ceiling — working target **≤ €20/month** at circle scale; is that realistic with always-available reminders? | M0 deploy (first real bill) |
| OQ-2 | UI language: English-only, or Czech localization? Strings are externalized either way (NFR-9) | M3 |
| OQ-3 | iOS push requires installing the PWA to the home screen — acceptable onboarding friction for the circle? | first iOS circle member (M3) |
| OQ-4 | Backup/restore: how is restore verified, and how often? | M3 hardening |

## Related documents

- [Requirements](requirements.md) — what DoseUp must do (FR/NFR with priorities)
- [Roadmap](roadmap.md) — milestones and their openspec changes
- [ADRs](../adr/) — the technology and architecture decisions behind it
- `openspec/specs/` — precise behavioral truth, once capabilities ship
