# DoseUp — Design workflow (Claude Design ↔ repo)

**Status:** decided 2026-07-18 ([#81](https://github.com/jakubbohm/DoseUp/issues/81); capability facts live-verified that day) · part of [conventions](README.md) — docs-first source of truth

How DoseUp turns UI intent into shipped interfaces with Claude Design (Anthropic's design tool). The mandate itself — every UI-heavy change gets a mockup + handoff before implementation — is delivery process ([ADR-0004-delivery-and-process § OpenSpec workflow rules](../adr/0004-delivery-and-process.md)); this doc is the how. Claude Design is a research preview and moves fast: capability statements below carry their verification date and are **re-verified on first use after any product update** (the [software-factory F-39](../software-factory.md) discipline).

## 1. Terms — what Claude Design's words mean (verified 2026-07-18)

- **Project** — one design workspace (chat + canvas + design files). Each screen iteration lives in a regular project.
- **Design system** — Claude Design's name for the reusable look-and-feel package: a "UI kit" of colors, typography, components, and layout patterns that new projects inherit. **Mechanically a tokens + components library — not corporate branding.** For DoseUp it holds exactly the token set and components the app builds on; ignore the brand-compliance marketing framing. An account can hold several; one is the **default** new projects pick up — DoseUp's must be that default once it has content.
- **Design-system project** — the dedicated project type (`PROJECT_TYPE_DESIGN_SYSTEM`) holding that package. Ours is **"DoseUp"** (created 2026-07-18, id `6ada98c2-bda4-4278-9995-50cee0d099db`). The empty "Design System" project left over from onboarding is never used.
- **Handoff (bundle)** — the app's "Hand off to Claude Code" export: the project's design files + the design chat + a README on interpreting them, delivered as a pasteable prompt carrying the bundle URL. This is the **only** channel design work travels back to us — Claude Design cannot write to the repo (the claude.ai GitHub integration is read-only, file contents on one branch).
- "Template" is not a first-class concept in the current product; this workflow doesn't use the word.

## 2. Source of truth & direction of flow

**The repo is the single source of truth for tokens and components.** The DoseUp design-system project is a derived, pushed copy — never hand-edited in the app. Remix explorations on the canvas are fine, but their outcome returns through a handoff like any other design work.

- **Up (repo → Claude Design):** Claude Code's `/design-sync` (beta) converts the client's component library into the uploaded design-system bundle, so the design agent builds with our real parts. It is **push-only** — it never pulls design content into the repo; public "two-way sync" wording means push-up + handoff-back, not bidirectional file sync. It requires a *buildable* library (component source + tokens/CSS + compiled output, or a Storybook) and pins the target project in a committed `.design-sync/config.json`. Re-push after a merged change to tokens/components.
- **Down (Claude Design → repo):** the handoff bundle only, consumed by an OpenSpec change. **Bundle content is input, never code:** the implementing change translates it into our conventions; nothing from a bundle is committed verbatim; generated agent-config files (a `CLAUDE.md`, rules, skills), lockfiles, or scaffolding inside a bundle are discarded on sight.

## 3. The loop (every UI-heavy change)

1. **Brief** — a versioned prompt doc in `docs/design/` (first: [claude-design-prompt-first-screens.md](../design/claude-design-prompt-first-screens.md)). Briefs state the implementation stack — Tailwind CSS v4 ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md)) — as deliberate prompt steering; the *documented* fidelity mechanism is a linked codebase / the pushed design system (steps 2 and 5), so brief steering carries the greenfield runs and re-pushing carries everything after. Component-behavior direction (e.g. Radix primitives) stays brief-level guidance while the React stack decision ([#26](https://github.com/jakubbohm/DoseUp/issues/26)) is open.
2. **Design** — Jakub runs the brief in a regular Claude Design project and iterates on the canvas. Manual by nature; the only other manual app-side acts are clicking the handoff and, optionally: linking the GitHub repo read-only for context (the documented fidelity mechanism once code exists) and publishing / setting DoseUp as the account's default design system.
3. **Hand off** — "Hand off to Claude Code"; the bundle URL enters the implementing session. The bundle's README / decisions record is archived under `docs/design/handoffs/<iteration>/` (bundle binaries stay out of git; [project-management.md § 12](project-management.md) attachments rule).
4. **Implement** — an OpenSpec change lands the translated tokens/components/screens in the client; all other conventions apply unchanged.
5. **Re-sync** — if tokens or components changed, the change's tasks include an explicit `/design-sync` push step (mirror of the TS-client-regen rule), so the next iteration designs with current parts.

## 4. Where design lands in the repo

- **Tokens: one file** in the web client (`styles/tokens.css`; exact path fixed by the scaffold change — [#43](https://github.com/jakubbohm/DoseUp/issues/43)): CSS custom properties with semantic names. Tailwind v4 consumes them CSS-first via `@theme` — static tokens become utilities; runtime-varying tokens (the ambient daypart hue) stay plain custom properties that utilities reference.
- **Components:** co-located in the client per the scaffold's layout; every reusable element is a named component (the brief's component list is the seed).
- **`.design-sync/config.json`:** committed once the first push runs (pins the DoseUp project).
- **`docs/design/`:** briefs + handoff records.

## 5. Bootstrap order (M0/M1 — greenfield exception)

Until the client exists there is nothing to push, so iteration 1 runs the loop with step 5 last-not-first: first-screens run (the trial [#58](https://github.com/jakubbohm/DoseUp/issues/58) executes) → handoff → the scaffold change lands tokens + base components → **first `/design-sync` push**. From then on the steady-state loop above. #58 is done when the DoseUp project holds the pushed tokens and the shell references them.

## 6. Re-verify on first use (beta facts, verified 2026-07-18)

`/design-sync` availability in the local Claude Code build · exact handoff-bundle contents (including that no agent-config files ride along — official docs confirm by omission only, and "by default" wording hints contents may vary) · whether a pushed design-system project still needs an app-side publish/set-default step before new projects inherit it · how far brief-declared stack steering carries the greenfield run (documented fidelity comes only from a linked codebase / pushed design system) · repo-linking scope. Sources: [help center — get started](https://support.claude.com/en/articles/14604416-get-started-with-claude-design) · [set up your design system](https://support.claude.com/en/articles/14604397-set-up-your-design-system-in-claude-design) · [GitHub integration](https://support.claude.com/en/articles/10167454-use-the-github-integration).
