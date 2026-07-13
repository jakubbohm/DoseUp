---
name: doseup-project-state
description: Founding interview complete (2026-07-13); all foundational decisions live in repo docs — read those, don't rely on memory for them
metadata:
  type: project
---

The founding product + engineering interview finished 2026-07-13. Everything it decided is recorded in the repo — treat these as authoritative, not memory: `docs/product/` (vision, requirements, roadmap), `docs/adr/0001-0004`, `docs/conventions/`, `CLAUDE.md` (working rules incl. [[user-gates-stage-progression]]), `openspec/config.yaml` (context + artifact rules).

`docs/software-factory.md` is special to Jakub: a reusable decision catalog seeding a future project-setup ("software factory") repo he plans to build. Keep depositing into it when foundational decisions are made or reversed — reversals are explicitly wanted as training data.

State as of writing: no git commit made for the docs batch yet (Jakub hasn't asked); no openspec change exists; tooling files (.editorconfig, CSharpier config, CI workflows, `.claude/rules/`) intentionally don't exist yet — they land via the M0 walking-skeleton change, which awaits Jakub's go.
