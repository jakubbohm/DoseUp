---
name: az-account
description: Always `az account set` to this repo's subscription before any az command; value lives in a gitignored file
metadata:
  type: feedback
---

Before running **any** `az` command in this project, first select the correct subscription: read the gitignored file `.claude/az-account.local` and run `az account set --subscription <the subscription value in that file>`. Do this once per session before the first `az` call.

**Why:** Jakub has several Azure accounts/subscriptions cached in the az CLI, and the default active one is not reliably DoseUp's. Running `az` against the wrong subscription could create/read/delete resources in an unrelated account. The subscription/tenant GUIDs (and the personal account) are treated as **not-for-public-repo** (this is a public showcase repo), so the real value lives only in the gitignored `.claude/az-account.local` — never in a committed file. [[copy-paste-commands-in-powershell]]

**How to apply:**
- Start of any Azure work: read `.claude/az-account.local`, extract the `subscription = <guid>` line, run `az account set --subscription <guid>`, then proceed.
- **If `.claude/az-account.local` is missing:** surface a single, **non-blocking** sentence to Jakub — the machine/session isn't set up for Azure yet (likely a fresh clone) — then carry on. Do **not** stop or fail the session over it: cloud/headless Claude sessions have no az at all and must keep working; they simply can't run az commands.
- Never print the subscription/tenant GUID into committed files, PRs, or artifacts.
