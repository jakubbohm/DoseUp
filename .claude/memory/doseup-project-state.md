---
name: doseup-project-state
description: Founding interview 2026-07-13; PRE-7 (domain) + PRE-8 (testing) resolved — next is the SharedKernel + test-infra openspec change, then the first domain module
metadata: 
  node_type: memory
  type: project
  originSessionId: 66159c46-e4d3-45a8-86cc-321fe9041b01
---

The founding product + engineering interview finished 2026-07-13. Everything it decided is recorded in the repo — treat these as authoritative, not memory: `docs/product/` (vision, requirements, roadmap), `docs/adr/0001-0004`, `docs/conventions/`, `CLAUDE.md` (working rules incl. [[user-gates-stage-progression]]), `openspec/config.yaml` (context + artifact rules).

PRE-7 (domain layer) resolved 2026-07-14; flagship `docs/conventions/domain-rules.md`. It *refined PRE-4*: thin endpoints + framework-free feature handlers replaced the "endpoint = handler" shorthand — when an old summary conflicts with ADR-0002/conventions, the newest PRE state wins. Jakub floated turning domain-rules.md into a skill later.

PRE-8 (testing organisation) resolved 2026-07-15; flagship `docs/conventions/testing.md` (ADR-0003 = stack, testing.md = organisation). Headlines: slice tests over HTTP = the handler test (no repositories ⇒ EF InMemory/SQLite/DbContext-mocks banned); isolation by construction (each test its own account, no cleanup); real JWT pipeline + test trust anchor; ADR-0002 gained dependency rule 7 (slice independence); Shouldly re-confirmed against TUnit.Assertions; zero-retry flake policy. It *refined PRE-6*: contract modifications update every TS consumer in the same change (CI only compares, never commits); the TS facade/wrapper question stays open in PRE-5. testing.md §9 is the M0 verification checklist the shared-kernel change must walk.

Agreed sequencing (roadmap intro note): next is **one OpenSpec change for SharedKernel + test infrastructure** (implements PRE-7 primitives + PRE-8 layout/arch-test catalog, ships rules vacuously green), then **the first domain module as validation slice** (carries the Stryker×TUnit spike). Harvest factory artifacts (possible skills from domain-rules.md / testing.md) only after that slice proves the primitives.

`docs/software-factory.md` is special to Jakub: a reusable decision catalog seeding a future project-setup ("software factory") repo — the top-priority deliverable of this phase. Keep depositing (F-1..F-73 so far) when foundational decisions are made or reversed — reversals are explicitly wanted as training data.

Process rhythm that works: per-topic interview (propose → Jakub verdicts → land docs → next topic; flag when a "verdict" has no real fork — he dislikes empty questions); each resolved PRE item lands docs + factory deposits + its own commit ("docs: resolve PRE-x - ..."). Tooling files (.editorconfig, CI workflows, `.claude/rules/`) still don't exist — they land with the shared-kernel/M0 changes.
