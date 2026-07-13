# ADR-0004: Delivery pipeline and development process

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-13)

## Context

Full CI/CD from the walking skeleton onward; solo developer + Claude; trunk-to-production with no staging environment — which makes PR gates and feature flags the entire safety story.

## Decisions

### Git

- **Trunk-based, PRs always** — short-lived branches (typically one per openspec change or task); every merge goes through a PR so all gates always run, even for solo/trivial edits.
- **Squash merge** with the PR title enforced as a **Conventional Commit** (title lint in CI); linear history feeds release automation.
- **Automated releases:** changelog + semver tags + GitHub Releases maintained automatically (release-please-style tooling; exact tool picked in the M0 change).

### CI — PR gates (all blocking)

1. Build (dotnet + npm), analyzers as errors
2. TUnit suites: unit + integration (Aspire harness) + architecture tests
3. CSharpier check; ESLint + Prettier check + `tsc`
4. TS-client drift gate: regenerate OpenAPI types, fail on diff
5. Playwright E2E smoke subset against the Aspire-orchestrated app
6. CodeQL + dependency review

### CD

- Merge to main → deploy the **single production environment** (Azure Container Apps, azd/aspire path). No staging: accepted risk, mitigated by the gate suite + feature flags + instant revert (small deploys).
- **Nightly quality job:** full Playwright suite, coverage report, Stryker mutation run (only if the TUnit spike succeeds, ADR-0003).

### Feature flags

**Microsoft.FeatureManagement + Azure App Configuration** — runtime toggles without redeploys. Convention: incomplete features merge dark behind a flag; every flag introduction includes a removal task (tracked via the openspec change's tasks) so flags don't rot.

### OpenSpec workflow rules (operative, mirrored in `openspec/config.yaml`)

- Behavior changes go through OpenSpec; proposals cite FR/NFR ids + roadmap milestone.
- **A change whose design touches the API contract includes an explicit "regenerate TS client" task** — regeneration is triggered by the change process, CI only verifies (drift gate).
- UI-heavy changes include a **Claude Design mockup + handoff** step before implementation; the component library syncs to the Claude Design project via DesignSync so designs use real tokens/components.
- On archive: tick the roadmap, update requirement statuses.
- Stage progression across the workflow is **Jakub's explicit call** — propose, then wait.

## Alternatives considered

- Staging + manual prod approval, or staging + E2E auto-promote — rejected for cost/complexity at hobby scale; revisit if a prod incident shows the gates aren't enough.
- Merge commits / rebase merging — rejected: squash + conventional titles is the cleanest fit for release automation.
- Free-form commits — rejected: the history is part of the showcase.

## Consequences

- The PR gate suite is the **only** thing between a merge and production — gates are never skipped or made optional "just this once"; a red gate blocks, full stop.
- E2E smoke on every PR + Aspire integration tests make CI minutes the main cost — worth watching, tune the smoke subset if PRs get slow.
- No staging means schema/data migrations must be forward-safe from day one (expand/contract discipline with Cosmos document versioning).
