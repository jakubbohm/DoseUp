# DoseUp — Project management & work items

**Status:** decided 2026-07-17 (project-management interview) · part of [conventions](README.md) — docs-first source of truth

How DoseUp plans and tracks work. The register rule above all others: **every issue is written for Jakub cold** — plain words, outcome-first, no internal codenames. Read this before creating, editing, or closing anything on GitHub, and before planning a milestone.

## 1. Source of truth — three homes, nothing duplicated

- **GitHub issues + milestones + project board are the only home of work items** — what's planned, its status, its ordering, its relations. Nothing in the repo mirrors them.
- **Repo docs are the only home of decisions and knowledge** — [ADRs](../adr/), [conventions](README.md), `openspec/specs/`, [requirements](../product/requirements.md), [vision](../product/vision.md), [roadmap](../product/roadmap.md). An issue never *contains* a decision; it links to where the decision landed.
- **OpenSpec changes are the execution layer** for behavior-changing code — precise delta specs + fine-grained `tasks.md`, created at implementation time (§10), never at planning time.

The tracker records only that work happened: if an issue vanished we'd lose history but zero knowledge. Links bridge the homes; content never crosses.

## 2. Work-item types

Exactly one type label per issue (GitHub issue *types* aren't available on personal accounts, so labels carry type):

| Label | Means | Notes |
|---|---|---|
| `uc` | new user-facing functionality across layers | typically one openspec change; typically has sub-issues |
| `enhancement` | improvement or extension of existing functionality | |
| `bug` | something shipped is wrong | |
| `spike` | time-boxed go/no-go proof | yields a decision, not a change |
| `task` | non-code / manual / setup work | buy a domain, create the Entra tenant, Claude Design work, portal-less Azure setup steps |
| `decision` | tracked design decision | §8 — successor of the retired pre-implementation checklist |

**Ideas are not issues.** They live as project **drafts** (§6) — Jakub's notebook on the board. Drafts can't carry labels; a draft gets its type label when promoted to a real issue.

## 3. Issue anatomy & register

- **Title = plain outcome language.** No codenames, change ids, or shorthand.
  - Bad (real, retired): *API Entra authority + ActiveAccount + account table*
  - Good: *API recognizes who's calling — Entra sign-in validated, account looked up in the DB*
- **Body top = 2–6 sentences Jakub understands cold:** what, why, what "done" looks like, known constraints/ideas. Where a technical term is genuinely the subject (Bicep, Entra, Wolverine, OTel) it may appear, but the sentence around it says what it's for in plain language.
- **Dense pointers go at the bottom**, optionally, in one collapsed block — FR/NFR ids, ADR/convention links, related change ids:

  ```markdown
  <details><summary>Context for Claude</summary>

  FR-3 · [ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md) · relates to c001-add-shared-kernel
  </details>
  ```

- **Sub-issues = only chunks worth seeing on the board** — spikes, manual/Azure/design tasks, deliberately split code chunks. Never a mirror of an openspec `tasks.md`. Rule of thumb: sub-issue if Jakub would want it on the board; `tasks.md` line if only the implementation session cares.
- **No custom numbering.** GitHub #ids + parent links are the identity — nothing like "M0-3", ever.

## 4. Labels

12 labels total; everything else (GitHub defaults, the old `type:`/`area:` sets) is pruned — GitHub's native duplicate-of and "not planned" close reasons replace `duplicate`/`wontfix`/`invalid`.

- **Types** (§2): `uc` · `enhancement` · `bug` · `spike` · `task` · `decision` — vivid, distinct hues.
- **Areas** — one muted color family; apply every one that fits:
  - `fe` — React PWA / web
  - `be` — API, modules, messaging (no separate `api` label; the TS-client regen duty is an openspec task rule — [conventions/README.md § API conventions](README.md) — not a planning label)
  - `db` — schema, migrations, data motion
  - `infra` — Bicep, Azure, and Neon *operational* work
  - `ci-cd` — GitHub Actions gates & release pipeline
- **Marker:** `opsx` — will be delivered through an OpenSpec change (§10).

## 5. Milestones

The milestones (M0–M3) mirror the [roadmap](../product/roadmap.md)'s narrative. A milestone description is 2–4 human sentences — goal + done-when — plus one link to its roadmap section. Never a copy-pasted scope list: **the milestone's issues are the decomposition**; the roadmap keeps the strategic why.

## 6. Board & ideas-as-drafts

User project **DoseUp** (project 6). Fields: **Status** (Todo / In progress / Done) and **Priority** (P0 / P1 / P2) — nothing else; Team/Size/Estimate/Iteration were deliberately removed. Built-in workflows do the routine motion: new repo issues auto-add to the board, closed issues move to Done (workflow *configuration* is UI-only — the GraphQL schema exposes no mutation to set it, so any workflow change is a manual UI step). Ideas live as drafts (§2); promoting one to an issue is a deliberate act in a planning conversation.

## 7. Relationships

- **Parent ↔ sub-issue** and **blocked-by** use GitHub's native mechanisms (GraphQL-only — recipes in §13), never body-text conventions.
- Blocked-by is declared by the blocked issue against its blocker — including UCs blocked by open `decision` issues (§8).
- **GitHub never auto-closes a parent when all its children close** — §10 says who closes the parent.

## 8. Decision records (`decision` label)

The successor of the retired pre-implementation checklist. Knowledge lives only in authoritative docs; the issue tracks that a decision is open, then that it happened.

- **One evergreen parent — "Design decisions"** — holds every decision issue as a sub-issue, past and future; it stays open indefinitely. Outcome comments go on each item's own issue, never the parent thread.
- **Anatomy:** human title ("Choose the React data & state stack", never an id); body = short human framing + the **original raw note quoted verbatim** (never reworded — Jakub's rule) + for migrated items, the old PRE-x id mentioned once as a searchable historical alias.
- **Jakub kicks each decision off himself; Claude never starts one on its own initiative.**
- **Resolution:** the outcome is written authoritatively into the right doc type — ADR for architecturally significant, conventions for how-we-work, requirements/roadmap for scope — then the issue closes with a 3–6-line pointer comment: the decision in one line + links to the landing docs + [software-factory](../software-factory.md) F-ids.
- Decision issues get **no milestone**; dependent UCs declare native blocked-by against them (§7).

## 9. Referencing style

- **Full descriptive slugs, always:** `[ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md)`, `[conventions/testing.md § 4](testing.md)` — the link text alone tells Jakub what it is without opening it. Never bare "ADR-0001"; retired PRE-x ids never appear (except as the historical-alias line inside migrated decision issues, §8).
- **Ids that stay:** FR-x / NFR-x / OQ-x / G-x (defined in [requirements](../product/requirements.md) / [vision](../product/vision.md)), F-x (defined in [software-factory.md](../software-factory.md)), change ids `cNNN-…`, GitHub `#N`.
- **Open decisions** are cited by their issue URL; a doc spot written before the issue exists says "(open design decision — tracked in the *Design decisions* issues)" until the URL is patched in.

## 10. OpenSpec linkage & auto-close mechanics

Lifecycle of a `uc` (or any `opsx`-marked issue):

1. Issue created with human description + known board-worthy sub-issues; labeled `opsx` when it will be spec-driven.
2. Work starts: `opsx:explore` → `opsx:propose` creates change `cNNN-…`; the proposal records `Tracks: #21, #22 (closes on merge)` — or `Refs: #N` for partial delivery.
3. The implementing PR body carries `Closes #N` for every issue the change completes (PRs are created via `gh pr create --body`, so this is deterministic). `opsx:verify` checks the PR body against the proposal's Tracks line.
4. Squash-merge to `main` auto-closes those issues; the board flips them to Done; archiving the change ticks the roadmap as today.

Mechanics worth knowing:

- Auto-close fires when a PR whose **body** carries `Closes #N` / `Fixes #N` / `Resolves #N` merges into the default branch — one keyword per issue (`Closes #21, closes #22`). `Refs #N` associates without closing — deliberate for partial delivery. Squash-merge preserves all of this because the keywords live in the PR body, and the link shows in the issue's Development sidebar as soon as the PR exists.
- **Parent-close gotcha:** GitHub never auto-closes a parent when all children close. Convention: the change that completes the whole UC also carries `Closes` for the parent; otherwise the parent is closed explicitly when its last child lands.

These rules are mirrored in [`openspec/config.yaml`](../../openspec/config.yaml) so every change session carries them.

## 11. Planning ritual

At each milestone start:

1. Jakub briefs; Claude interviews about scope.
2. Claude drafts the **full issue plan as reviewable markdown** — every title, body, label, relation — with zero GitHub writes.
3. Jakub red-pens; after his **explicit sign-off**, Claude creates everything on GitHub in one scripted pass.

Mid-milestone, new work enters as individual issues with the same anatomy (§3); ideas as project drafts (§6). Issue templates/forms: deliberately skipped for now — revisit if Jakub files many raw issues from his phone.

## 12. Attachments

No API can upload files to issues (web-UI only). Convention: Claude-produced visual artifacts (mockups, diagrams, screenshots) are **committed to the repo** and linked from the issue.

## 13. Tooling — gh CLI is the single GitHub channel

`gh` (2.92.0+, token with the `project` scope) is the only way Claude touches GitHub — no GitHub MCP server, no portal automation. Etiquette:

- **Reads are free:** `gh issue view/list`, `gh label list`, `gh api` GET, read-only GraphQL, `gh project view/field-list/item-list`.
- **Mutations happen only inside a signed-off plan's scripted pass** (§11) — never ad-hoc, never "while I'm here".
- **CI never touches the tracker** — no auto-generated issues; the only tracker automation is GitHub's own (auto-close keywords, board workflows).

Recipes (verified against gh 2.92.0 + GraphQL introspection, 2026-07-17):

```sh
# Create / close / comment / edit an issue (--milestone takes the TITLE, not the number)
gh issue create -R jakubbohm/DoseUp --title "TITLE" --body-file body.md \
  --label uc --label be --milestone "M0 — Walking skeleton on Azure"
gh issue close 21 -R jakubbohm/DoseUp --reason "not planned" --comment "…"
gh issue comment 21 -R jakubbohm/DoseUp --body-file comment.md
gh issue edit 21 -R jakubbohm/DoseUp --add-label opsx --body-file body.md

# Labels (color = hex without #; --force updates an existing label)
gh label create uc -R jakubbohm/DoseUp --color 1D76DB --description "…"
gh label delete "type: change" -R jakubbohm/DoseUp --yes

# Milestone descriptions (no gh subcommand — REST PATCH by milestone NUMBER)
gh api -X PATCH repos/jakubbohm/DoseUp/milestones/1 -f description="…"

# Board (item-add returns the item id with --format json; item-edit sets single-selects)
gh project item-add 6 --owner jakubbohm --url https://github.com/jakubbohm/DoseUp/issues/21 --format json
gh project item-edit --id <ITEM_ID> --project-id <PROJECT_ID> --field-id <FIELD_ID> --single-select-option-id <OPTION_ID>

# Node id for GraphQL (issue relations are GraphQL-only — no native gh flags)
gh issue view 21 -R jakubbohm/DoseUp --json id
```

Sub-issue, dependency, and deletion mutations — GraphQL only:

```sh
# Make #child a sub-issue of #parent
gh api graphql -f query='
mutation($parent: ID!, $child: ID!) {
  addSubIssue(input: {issueId: $parent, subIssueId: $child}) {
    issue { number } subIssue { number }
  }
}' -f parent="<PARENT_NODE_ID>" -f child="<CHILD_NODE_ID>"

# Declare a dependency (issueId = the blocked issue)
gh api graphql -f query='
mutation($blocked: ID!, $blocker: ID!) {
  addBlockedBy(input: {issueId: $blocked, blockingIssueId: $blocker}) {
    issue { number }
  }
}' -f blocked="<BLOCKED_NODE_ID>" -f blocker="<BLOCKER_NODE_ID>"

# Delete an issue — PERMANENT, no undo; only ever inside a signed-off plan
gh api graphql -f query='
mutation($id: ID!) {
  deleteIssue(input: {issueId: $id}) { repository { nameWithOwner } }
}' -f id="<ISSUE_NODE_ID>"
```

`removeSubIssue` / `removeBlockedBy` take the same input shapes; `reprioritizeSubIssue` (`afterId`/`beforeId`) orders children under a parent. Read side: the `Issue.blockedBy` / `Issue.blocking` connections (the field is `blockedBy`, not `blockedByIssues`).
