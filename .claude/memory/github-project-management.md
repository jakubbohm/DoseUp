---
name: github-project-management
description: DoseUp's human-first GitHub project-management process — issues/milestones/project as the only work-item home, gh CLI as the only channel, decision issues replacing the PRE checklist
metadata:
  node_type: memory
  type: project
---

DoseUp's work-item process is authoritative in `docs/conventions/project-management.md` (adopted 2026-07-17) — read it before touching the backlog. Summary of the load-bearing rules:

- **Source-of-truth split.** GitHub issues + milestones are the *only* home of work items (what's planned, status, ordering); repo docs are the *only* home of decisions/knowledge (ADRs, conventions, specs, requirements, vision); OpenSpec changes are the execution layer, created at implementation time. Nothing is duplicated — links bridge. Never build a parallel work-item structure in the repo.
- **gh CLI is the single GitHub channel.** No GitHub MCP server, never the web UI for state. Sub-issues and blocked-by dependencies have no native gh flags → `gh api graphql` (`parent`/`subIssues`/`blockedBy`/`blocking`). Issue types are unavailable on personal accounts → labels carry the type. Attachments have no API → commit visual artifacts to the repo and link them.
- **Read is free; mutations wait for sign-off.** `gh issue/label/project view/list`, `gh api` GET, read-only graphql — fine anytime. Any create/edit/close/delete on GitHub runs only after Jakub signs off a plan, batched as one scripted pass (the F-38 human gate applied to the tracker).
- **Issue register.** Titles = plain outcome language, no internal codenames; body top = 2–6 sentences Jakub understands cold; dense pointers (FR/NFR ids, ADR/convention links) go in a collapsed "Context for Claude" block. Sub-issues only for chunks worth seeing on the board, never a mirror of tasks.md. Labels: types `uc`/`enhancement`/`bug`/`spike`/`task`/`decision` + `opsx` marker; five muted area labels (`fe`/`be`/`db`/`infra`/`ci-cd`).
- **Decision records replace the PRE checklist.** Every open design decision = a `decision`-labeled issue under the evergreen **"Design decisions"** parent (stays open forever). Jakub kicks each off himself — Claude never starts one. The original raw note is quoted verbatim (never reworded); resolution writes the outcome authoritatively into the right doc type, then the issue closes with a short pointer comment on its own thread.
- **OpenSpec linkage (also in `openspec/config.yaml`).** Proposal records `Tracks: #N (closes on merge)` / `Refs: #N`; the implementing PR body carries `Closes #N` for each completed issue **plus the parent when the change completes the whole UC** — GitHub never auto-closes a parent when its children close. opsx:verify checks the PR body against the Tracks line.
- **Referencing style.** Cite durable artifacts by full descriptive slug (`ADR-0001-platform-and-stack`, `conventions/testing.md § 4`) so the reader knows what it is without searching. Opaque item ids like `PRE-x` are abolished repo-wide; FR-x/NFR-x/OQ-x/G-x, software-factory F-x, and change ids `cNNN-` stay.
- **Planning ritual.** At each milestone start: Jakub briefs → Claude interviews → Claude drafts the full issue plan as reviewable markdown → Jakub red-pens → after explicit sign-off Claude creates everything on GitHub in one pass.

Factory deposits F-77..F-80 generalize this. Related: [[doseup-project-state]], [[user-gates-stage-progression]].
