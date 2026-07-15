# ADR-0004: Delivery pipeline and development process

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-13) · **Amended:** 2026-07-14 — infra-as-code model, release pipeline, migration & backup strategy; expand/contract mandate dropped (PRE-9) · 2026-07-15 — C# format gate folded into the build: CSharpier dropped for `.editorconfig`/IDE0055 (conventions § Formatting)

## Context

Full CI/CD from the walking skeleton onward; solo developer + Claude; trunk-to-production with no staging environment — which makes PR gates, feature flags, and pre-deploy restore points the entire safety story.

## Decisions

### Git

- **Trunk-based, PRs always** — short-lived branches (typically one per openspec change or task); every merge goes through a PR so all gates always run, even for solo/trivial edits.
- **Every openspec change starts on its own branch off the freshest main**, created at change creation — automated by the `openspec/config.yaml` proposal rule; naming `<type>/<change-id>` (conventions).
- **Squash merge** with the PR title enforced as a **Conventional Commit** (title lint in CI); re-examined and confirmed at PRE-9: one change = one gate-verified, machine-readable, singly-revertable commit on main, with the WIP history still browsable in the PR. Corollary: the PR-head SHA never lands on main, so deployable artifacts are built from the merge commit (see CD).
- **Automated releases:** changelog + semver tags + GitHub Releases maintained automatically (release-please-style tooling; exact tool picked in the M0 change).

### Infrastructure as code (PRE-9)

- **Hand-authored Bicep** under `infra/` is the only definition of Azure. No portal or ad-hoc CLI mutations, ever — drift is corrected by redeploying the stack (`--deny-settings-mode denyDelete` is the recorded escalation if out-of-band changes ever become a problem).
- Applied as an **Azure Deployment Stack** (`az stack group create --action-on-unmanage deleteResources`): a resource removed from git is deleted from Azure on the next deploy.
- **Per-environment `.bicepparam` files** carry every environment-specific value (tiers, scale bounds, names). Today prod only; a staging environment would be one more param file + GitHub Environment, no redesign.
- **RBAC-minimal auth everywhere:** GitHub Actions → Azure via OIDC federated credentials (no stored service-principal secret); service-to-service via managed identities holding the narrowest built-in data-plane roles, assigned in Bicep. Recorded non-RBAC exceptions (NFR-4): the Neon connection string and VAPID keys.
- **The Aspire AppHost models local orchestration only.** AppHost ↔ Bicep sync is procedural, mirroring the PRE-6 contract rule: a change touching the AppHost resource graph includes an explicit "update infra Bicep + `.bicepparam`" task (openspec config rule); the post-deploy smoke test backstops wiring drift.
- **Neon sits outside the IaC** (not an Azure resource): project setup is manual and documented in the runbook that lands with M0.

### CI — PR gates (all blocking)

1. Build (dotnet + npm), analyzers as errors
2. TUnit suites: unit + integration (Aspire harness) + architecture tests
3. ESLint + Prettier check + `tsc` *(C# formatting needs no separate gate since 2026-07-15: `.editorconfig` layout rides gate 1 as IDE0055 build errors — CSharpier dropped, conventions § Formatting)*
4. TS-client drift gate: regenerate OpenAPI types, fail on diff
5. Playwright E2E smoke subset against the Aspire-orchestrated app
6. CodeQL + dependency review
7. Bicep compiles and lints (`az bicep build`)

Additionally: a PR that adds migrations gets the idempotent SQL script (`dotnet ef migrations script --idempotent`) generated and attached as a review artifact. **PR CI publishes nothing** — no images, no deployable artifacts, no cloud credentials on PR runners (squash + merge skew make PR-built artifacts undeployable anyway).

### CD — release pipeline (PRE-9)

`release.yml` on push to main:

1. **Build once from the merge commit** — container images tagged with the git SHA + the EF Core **migration bundle** (self-contained linux-x64). These identical artifacts are what every environment receives; only `.bicepparam` values and GitHub Environment secrets differ per environment (Environments also carry the required-reviewers approval gate if a pre-prod environment ever exists).
2. **Infra:** `az stack group create` applies the committed Bicep (idempotent).
3. **Restore point:** create Neon branch `pre-deploy-<run>` (instant copy-on-write; prune to keep the last 3). The Free plan's instant-restore window is only 6 h (2026-07), so the named branch *is* the rollback insurance; the plan allows 10 branches.
4. **Migrate + roll out:** if the deploy range contains new migrations → **maintenance-window recreate**: stop the API app, run the bundle, update images, start — schema and code switch together, so migrations may be destructive. Otherwise → plain zero-downtime revision replace. Never `Database.Migrate()` at app startup in production.
5. **Smoke probe** against the live URL.

- **Vendor-independent DR:** scheduled `pg_dump` to Azure Blob (standard Postgres stays portable — ADR-0001); restore drill in M3 (OQ-4).
- **Nightly quality job:** full Playwright suite, coverage report, Stryker mutation run (only if the TUnit spike succeeds, ADR-0003).

### Feature flags

**Microsoft.FeatureManagement + Azure App Configuration** — runtime toggles without redeploys. Convention: incomplete features merge dark behind a flag; every flag introduction includes a removal task (tracked via the openspec change's tasks) so flags don't rot.

### OpenSpec workflow rules (operative, mirrored in `openspec/config.yaml`)

- Behavior changes go through OpenSpec; proposals cite FR/NFR ids + roadmap milestone.
- **Every change branches off the freshest main at creation** (`<type>/<change-id>`; PRE-9).
- **A change whose design touches the API contract includes an explicit "regenerate TS client" task** — regeneration is triggered by the change process, CI only verifies (drift gate).
- **A change touching the AppHost resource graph includes an explicit "update infra Bicep + `.bicepparam`" task** (PRE-9 — infra is hand-authored; the AppHost is local-only).
- UI-heavy changes include a **Claude Design mockup + handoff** step before implementation; the component library syncs to the Claude Design project via DesignSync so designs use real tokens/components.
- On archive: tick the roadmap, update requirement statuses.
- Stage progression across the workflow is **Jakub's explicit call** — propose, then wait.

## Alternatives considered

- Staging + manual prod approval, or staging + E2E auto-promote — rejected for cost/complexity at hobby scale; revisit if a prod incident shows the gates aren't enough. The pipeline is deliberately environment-parameterized so adding staging later is additive, not a redesign.
- Merge commits / rebase merging — rejected: squash + conventional titles is the cleanest fit for release automation; rebase taxes every intermediate commit with gate/convention discipline and still rewrites SHAs.
- **Generated IaC** (`aspire publish`/azd-generated Bicep, committed or not — PRE-9, verified 2026-07): rejected. Customization happens through C# `ConfigureInfrastructure`/`PublishAsAzureContainerApp` lambdas rather than IaC; there is no first-class per-environment parameterization; azd's deployment-stacks support is alpha; and regeneration overwrites hand-customization — which also kills any generate-and-diff drift gate (no reliable "logical" Bicep comparison exists, and the generated shape churns with each Aspire release). Process rule + smoke test replace the mechanical gate.
- **Zero-downtime-always + expand/contract migrations** (the original decision here) — superseded at PRE-9: the old-code-on-new-schema coexistence window is the only thing that forced backward-compatible migrations; a short maintenance window on schema-changing deploys buys naive/destructive migrations forever. Trade recorded in consequences.
- Promoting PR-built artifacts to environments — rejected: squash makes the PR-head SHA unreachable from main, merge skew means the merged combination may never have been built or tested, and PR runners must not hold publish/deploy credentials.
- **Neon branches for dev/CI isolation** — rejected: local Aspire Postgres containers (dev) and the Aspire harness (CI — ADR-0003) are faster, free, and isolated; branching's role is the pre-deploy restore point instead.

## Consequences

- The PR gate suite is the **only** thing between a merge and production — gates are never skipped or made optional "just this once"; a red gate blocks, full stop.
- E2E smoke on every PR + Aspire integration tests make CI minutes the main cost — worth watching, tune the smoke subset if PRs get slow.
- **Migrations are free to be destructive (expand/contract dropped, PRE-9).** The price: a minute-scale maintenance window on deploys that carry migrations (reminder alarms buffer in the queue meanwhile — at-least-once semantics already assume delay), and **instant revert weakens for those deploys** — the previous image may not run on the new schema, so recovery is restore-the-pre-deploy-branch (losing the minutes of writes since deploy) or roll forward. Accepted at circle scale; revisit when real external users would feel it.
- Hand-authored Bicep means the env-var/connection-string contract between AppHost and Azure is kept in sync by process (task rule + smoke test), not by generation — the accepted cost of owning the IaC.
