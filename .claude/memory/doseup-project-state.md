---
name: doseup-project-state
description: Founding interview done 2026-07-13; PRE-7 (domain layer) resolved 2026-07-14; docs are authoritative — sequencing runs domain-first ahead of M0
metadata: 
  node_type: memory
  type: project
  originSessionId: 66159c46-e4d3-45a8-86cc-321fe9041b01
---

The founding product + engineering interview finished 2026-07-13. Everything it decided is recorded in the repo — treat these as authoritative, not memory: `docs/product/` (vision, requirements, roadmap), `docs/adr/0001-0004`, `docs/conventions/`, `CLAUDE.md` (working rules incl. [[user-gates-stage-progression]]), `openspec/config.yaml` (context + artifact rules).

PRE-7 (domain layer) resolved 2026-07-14 via per-topic interview (SmartEnum → base types/typed ids → domain rules → events → SharedKernel discipline); flagship output `docs/conventions/domain-rules.md`. It also *refined PRE-4's recording*: thin endpoints + framework-free feature handlers replaced the "endpoint = handler" shorthand — when an old summary conflicts with ADR-0002/conventions, the PRE-7 state wins. Jakub floated turning domain-rules.md into a skill later.

Agreed sequencing (roadmap intro note): domain-first ahead of the M0 walking skeleton — next is the PRE-8 interview (testing organisation), then one OpenSpec change for SharedKernel + test infrastructure, then the first domain module as validation slice; harvest the factory artifact only after that slice proves the primitives.

`docs/software-factory.md` is special to Jakub: a reusable decision catalog seeding a future project-setup ("software factory") repo he plans to build — the top-priority deliverable of this phase. Keep depositing (F-1..F-65 so far) when foundational decisions are made or reversed — reversals are explicitly wanted as training data.

Process rhythm that works: each resolved PRE item lands docs + factory deposits + its own commit ("docs: resolve PRE-x - ..."). Tooling files (.editorconfig, CI workflows, `.claude/rules/`) still don't exist — they land with the shared-kernel/M0 changes.
