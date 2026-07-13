# Software Factory — Decision Catalog (seed)

**Status:** knowledge deposit, not project documentation · **Snapshot:** 2026-07-13 · **Source:** DoseUp founding interview

This document is *not* used by DoseUp directly. It records every setup decision made for DoseUp in a **reusable question → options → choice → rationale** form, as seed material for a future "software factory" repo: a tool that interviews the owner of a new project (dozens of questions, top-level architecture down to how an endpoint is implemented, across stacks) and **recommends answers based on accumulated knowledge** like this file.

> ⚠️ **Ecosystem facts rot.** Every claim tagged with a date reflects 2026-07. A factory must re-verify current versions, licenses, and compatibility at setup time (see F-39) instead of trusting this snapshot.

Entry format: **Question** → options with trade-offs → the DoseUp answer → when it generalizes.

---

## A. Product discovery

These condition everything downstream; ask them first.

**F-1 · What is the product, in one sentence?**
Open question. DoseUp: *medication & supplement dose tracker for an invite-only circle.*
*Generalizes:* the sentence names the domain nouns — they become module candidates later.

**F-2 · Who uses it?**
Options: just me · me + household · invite-only circle · public someday.
DoseUp: **invite-only circle** → real auth and per-account isolation, but no public-signup/multi-tenant machinery.
*Generalizes:* this single answer sizes auth, tenancy, ops, and privacy work more than any other.

**F-3 · Primary form factor?**
Options: mobile-first PWA · native app · desktop-first web · both equally.
DoseUp: **mobile-first PWA** (quick logging on the go; no app stores).
*Generalizes:* PWA choice immediately raises the push-notification question (F-8) and iOS home-screen caveats.

**F-4 · Why is it being built?** *(multi-select; weights all later trade-offs)*
Options: I need the tool · learning playground · portfolio/showcase · future product.
DoseUp: **all of: need + learning + showcase** (incl. showcasing AI-assisted workflow itself).
*Generalizes:* "learning + showcase" justifies deliberate over-engineering (modular discipline, preview runtimes) that "I need the tool" alone would forbid. Record the weighting — it settles later arguments.

**F-5 · Must-have capability set for v1?**
Ask as forced choice over candidate capabilities, not open brainstorming.
DoseUp: logging + history and schedules + reminders; stock tracking and journaling explicitly deferred.
*Generalizes:* everything not chosen goes to a Could/Won't table with ids — deferral must be visible, not silent.

**F-6 · Offline requirement?**
Options: offline-first (sync engine — big) · graceful degradation · online-only.
DoseUp: **online-only** — connectivity assumed at logging time.
*Generalizes:* offline-first is an architecture, not a feature; never let it in implicitly.

**F-7 · Data sensitivity?**
Options: sensitive-by-design (export/delete, minimal logging, documented stance) · reasonable defaults (TLS, auth, encryption at rest, backups) · don't care.
DoseUp: **reasonable defaults** + never log payload contents + in-app "not medical advice" note.
*Generalizes:* health/finance data at public scale forces the first option; small trusted circles can rationally pick the middle.

**F-8 · Notification channel?**
Options: web push · in-app only · email · messaging bot.
DoseUp: **web push (VAPID)** — time-sensitive reminders are the product's core promise.
*Generalizes:* pick by time-sensitivity; web push on iOS requires home-screen install (onboarding friction to validate).

**F-9 · Account/profile shape?**
Options: 1 account = 1 person · profiles under an account · cross-account sharing.
DoseUp: **profiles under an account** (parent + child), cross-account sharing deferred.
*Generalizes:* decide before the first schema — retrofitting profile scoping is painful.

**F-10 · Cost ceiling?**
Open, with a number. DoseUp: **≤ €20/month** working target, validated at first deploy.
*Generalizes:* a number turns hosting/topology debates into arithmetic.

## B. Platform & language

**F-11 · Runtime channel: LTS · current stable · preview?**
DoseUp (2026-07): **.NET 11 previews** for services (motivated by C# 15 `union` types), AppHost pinned to net10 (Aspire constraint).
*Generalizes:* preview runtimes only when (a) a specific feature motivates it, (b) learning value is an explicit goal (F-4), and (c) CI is strong enough to absorb churn. Verify orchestrator/tooling constraints — often *part* of the solution must stay on stable.

**F-12 · Error-handling model: exceptions vs Result?**
DoseUp: **Result end-to-end** — expected failures (validation/not-found/conflict) travel as a hand-rolled C# 15 `union Result` from domain to the HTTP edge, mapped to ProblemDetails; exceptions reserved for bugs/infrastructure.
*Generalizes:* Result-everywhere needs language-level support (unions) or a library, plus a written case→status-code matrix. Half-adopted Result (some layers throw, some return) is worse than either pure approach.

**F-13 · Result implementation: library vs hand-rolled?**
DoseUp: **hand-rolled** in SharedKernel (zero deps, full control, showcases the language feature).
*Generalizes:* hand-roll only with native unions; otherwise a maintained library (check license!) beats a homegrown monad.

**F-14 · Dependency/runtime upgrade cadence?**
Options: eager dedicated PRs · pin + upgrade per milestone · bot-driven continuous.
DoseUp: **eager dedicated PRs** for runtime/orchestrator (+ bots for routine deps once wired).
*Generalizes:* eagerness must match CI strength; an upgrade PR gated by a full pyramid is cheap insurance either way.

## C. Architecture

**F-15 · Architecture style ladder**
Plain vertical slices → **hybrid modular monolith** (bounded-context modules, clean domain core + ports per module, vertical slices as application layer) → multi-project clean architecture → microservices.
DoseUp: **hybrid modular monolith in a single project** — folders + namespaces policed by architecture tests, not csproj references.
*Generalizes:* pick by team size × domain complexity × goals. Solo + small domain + showcase = the hybrid sweet spot; the namespace discipline maps 1:1 to projects (or services) if scale demands later. Never microservices for a solo hobby app.

**F-16 · Physical granularity: one project · few layers · project-per-module?**
DoseUp: **single API project.**
*Generalizes:* boundaries-by-test beat boundaries-by-csproj until team size or build times say otherwise.

**F-17 · Rigor uniformity across modules?**
DoseUp: **sliding scale, declared** — rich modules get full DDD treatment; trivial modules may be honest CRUD, but each module *declares its grade*.
*Generalizes:* the declaration is the trick — it converts inconsistency from drift into decision.

**F-18 · Eventing maturity: none · domain events · + integration events?**
DoseUp: **both kinds from day one** — domain events sync in-module/in-UoW; integration events async post-commit via outbox (Cosmos change feed as pump; in-process transport first, broker-shaped seam).
*Generalizes:* the two-kind vocabulary is reusable everywhere; the outbox mechanism is per-database (change feed on Cosmos, outbox table + poller on SQL). Skip integration events entirely while there's only one module — unless establishing the pattern is itself a goal.

**F-19 · Validation layering?**
DoseUp: **two layers, two channels** — request shape at the edge (FluentValidation → 400 ProblemDetails), business invariants in the domain (→ Result → ProblemDetails).
*Generalizes:* near-universally correct; the alternatives (all-at-edge, all-in-domain) each leak one concern into the other's home.

## D. API & contract

**F-20 · Endpoint framework style?**
DoseUp (.NET): **FastEndpoints** (REPR pattern — one endpoint class per use case, natural slice fit).
*Generalizes:* per-stack question; the invariant is *one use case = one addressable unit*, whatever the framework.

**F-21 · Error contract?**
DoseUp: **ProblemDetails (RFC 9457) for every non-2xx**, from validation and Result mapping alike.
*Generalizes:* adopt the platform's standard problem format everywhere; a single documented case→status matrix, no ad-hoc error JSON.

**F-22 · Frontend/backend contract sync?**
Options: OpenAPI → generated client (committed) · TypeSpec contract-first · docs only.
DoseUp: **OpenAPI → generated TS types (openapi-typescript candidate), committed.** Regeneration is a mandatory task inside any spec change that touches the API; **CI only verifies** (regen + diff = drift gate). PR diffs thus show contract evolution.
*Generalizes:* "change process triggers regen, CI gates drift" beats watcher-magic and beats CI-side generation — the artifact stays reviewable and the duty stays visible in the task list.

## E. Data & auth

**F-23 · Database selection?**
DoseUp: **Azure Cosmos DB serverless** — Azure-native, ~free at circle scale, document shape fits per-profile freeform data, change feed powers the outbox; accepted lock-in as showcase.
*Generalizes:* default relational (Postgres) unless: document-shaped data + cloud-native goals + serverless economics + a feature like change feed pulls the other way. No relational migrations on document DBs — demand versioning/expand-contract discipline instead. Partition-key design is an early, hard-to-reverse decision.

**F-24 · Identity: external IdP · self-hosted · third-party?**
DoseUp: **Microsoft Entra External ID** (cloud-native CIAM, free tier, invite-only friendly).
*Generalizes:* never self-host credentials for hobby scale; pick the IdP native to the target cloud unless portability is a stated goal.

## F. Testing

**F-25 · Test framework + assertions?**
DoseUp (2026-07): **TUnit** (MTP-native, source-generated; accepted weekly churn) + **Shouldly** (BSD, no license traps).
*Generalizes:* **always run a licensing check on test deps** (FluentAssertions went commercial in v8 — the lesson behind this question) and verify mutation-testing/tooling compatibility *before* committing to a framework. xUnit v3 is the boring-mature default; TUnit the bleeding-edge pick.

**F-26 · Integration-test harness altitude?**
Options: in-proc factory (WebApplicationFactory) · containers per dependency (Testcontainers) · full-orchestrator harness (Aspire testing).
DoseUp: **Aspire harness only** (`DistributedApplicationTestingBuilder` + Cosmos vNext emulator) — one harness, real graph; accepted cost: full startup even for persistence-level tests (revisit if painful).
*Generalizes:* the trade is fidelity vs loop speed; most teams layer two altitudes — going single-altitude is valid if declared and revisited.

**F-27 · E2E runner & placement?**
DoseUp: **@playwright/test (TypeScript)** beside the frontend; **smoke subset on PR, full suite nightly**.
*Generalizes:* E2E in the frontend's language gets the flagship tooling (HTML report, UI mode, sharding — 2026-07). "Smoke gates PR / full runs nightly" is the near-universal cost balance.

**F-28 · Architecture tests?**
DoseUp: **ArchUnitNET** (TUnit adapter) enforcing written dependency rules (ADR-0002 rules 1–5), plus analyzers for must-never-happen rules.
*Generalizes:* arch tests are only as good as the *written* rules they encode — write the rules first (ADR), then the tests quote them. (2026-07: NetArchTest is unmaintained; don't pick it.)

**F-29 · Mutation testing?**
DoseUp: **spike first** (Stryker × TUnit unverified; Stryker × xUnit v3 broken as of 2026-07); if viable → nightly with dashboard baseline, never a PR gate.
*Generalizes:* mutation testing is a nightly telescope, not a PR microscope; its tooling always trails framework churn — verify before promising it.

## G. Delivery

**F-30 · PR gate composition?**
DoseUp (all blocking): build + analyzers-as-errors · unit/integration/arch tests · formatter checks (both stacks) · typecheck · contract drift · E2E smoke · CodeQL + dependency review. Coverage/mutation deliberately *not* gates (nightly signal instead).
*Generalizes:* gates must be fast enough to keep PRs sub-~15 min; anything slower moves to nightly. Every gate is either blocking or deleted — advisory gates rot.

**F-31 · Environment topology + deployment gates?**
Options: single prod · staging → manual approval · staging → E2E auto-promote.
DoseUp: **single prod, deploy on merge to main** — PR gates + feature flags + instant revert *are* the deployment gate. Consequence: migrations must be forward-safe (expand/contract).
*Generalizes:* staging earns its cost only when (a) real users would feel breakage and (b) something can't be verified pre-merge. A hobby circle with strong gates doesn't clear that bar.

**F-32 · Commit & release conventions?**
DoseUp: **trunk-based, PRs always, squash merge, Conventional-Commit PR titles (linted), automated changelog/semver releases.**
*Generalizes:* squash + conventional titles is the lowest-discipline path to automated releases; "PRs always, even solo" keeps gates authoritative and history uniform.

**F-33 · Feature flags?**
DoseUp: **Microsoft.FeatureManagement + Azure App Configuration**; every flag ships with a removal task.
*Generalizes:* trunk→prod *requires* flags or dark-launching; flag-removal-as-task is the anti-rot mechanism regardless of provider.

## H. Formatting & static quality

**F-34 · Formatter: opinionated printer vs rule-based?**
DoseUp: **CSharpier** owns C# layout (config via .editorconfig; also formats csproj/XML) + **Prettier/ESLint** for TS; enforcement on-save + on-build + CI (no pre-commit hooks).
*Generalizes:* layout questions ("when do params wrap?") should be *removed* by an opinionated printer, not documented as prose or AI-rules — rule-based formatters can't express them, and prose drifts. Semantic style (naming, var, patterns) stays with analyzers/lint.

**F-35 · Analyzer strictness?**
DoseUp: **warnings-as-errors, latest-all, curated third-party packs**, every suppression justified in-place.
*Generalizes:* strictness is nearly free on greenfield and expensive to retrofit — start maximal, tune down with recorded reasons.

**F-36 · Where do conventions live?**
Options: enforced-only ("not enforced = not a convention") · **docs-first + enforcement mirror** · AI-rules-first.
DoseUp: **docs-first** — `docs/conventions/` is authoritative; tooling and `.claude/rules/` mirror it; convention changes touch doc + enforcement together.
*Generalizes:* docs-first preserves rationale (the *why* tools can't hold); the mirror discipline is what keeps it honest.

## I. AI-assisted workflow (the factory's own process)

**F-37 · Spec-driven development?**
DoseUp: **OpenSpec** — behavior changes go through proposal/specs/design/tasks artifacts; proposals must cite requirement ids + milestone; archiving syncs specs and updates roadmap/requirement statuses.
*Generalizes:* the traceability chain (requirement id → milestone → change → spec → code → test) is the product of this choice; any spec workflow that maintains it qualifies.

**F-38 · Who gates stage progression?**
DoseUp: **the human, explicitly.** The AI proposes, summarizes state after each stage/round, and waits; it never declares an interview or phase complete on its own.
*Generalizes:* encode this in the project's AI instructions (CLAUDE.md); it's the single highest-leverage trust rule for AI-assisted work — and the factory interview itself must obey it.

**F-39 · Research-before-recommend.**
DoseUp practice: every ecosystem-sensitive recommendation (runtime features, formatter, testing stack, licensing, product capabilities) was **verified against current sources during the interview**, not answered from model memory — which is how the FluentAssertions license, NetArchTest abandonment, Stryker×xUnit-v3 breakage, Cosmos vNext GA-but-experimental-in-Aspire, and Claude Design's June-2026 integration were caught.
*Generalizes:* the factory must treat its own catalog (this file) as hypotheses to re-verify at setup time, with dates on every claim.

**F-40 · AI workspace setup?**
DoseUp: auto-memory redirected **into the repo** (`autoMemoryDirectory` in gitignored `settings.local.json` → committed `.claude/memory/`), thin CLAUDE.md pointing at docs (no bulk imports), path-scoped `.claude/rules/` mirroring conventions, docs layout `docs/product` + `docs/adr` + `docs/conventions` distinct from spec truth (`openspec/specs`).
*Generalizes:* memory-in-repo makes AI project knowledge versioned and portable; the docs/spec split ("product docs say why, specs say precisely what, conventions say how") prevents duplication rot.

**F-41 · Design workflow with AI?**
DoseUp: **Claude Design** (Anthropic Labs, research preview) — UI-heavy changes get a mockup + **handoff bundle** before implementation; the component library syncs up via DesignSync so designs use real tokens/components (two-way loop since the 2026-06 integration).
*Generalizes:* the durable pattern is *design-artifact-before-UI-code with real design-system context*; the specific product is preview-grade — re-verify (F-39).

**F-42 · Architecture bar & decision ownership?**
DoseUp (PRE-1): **architectural quality is the project's highest priority**; the AI always reasons as a very senior software architect — not only what works, but what is architecturally correct — and the human is always the decision maker: no architectural decision is adopted until he understands and agrees. Encoded as the first working rule in CLAUDE.md.
*Generalizes:* "works" vs "correct" is the default failure line of AI-assisted architecture — models converge on the first unless the instructions demand the second. The understand-and-agree rule is what keeps the human a real architect rather than a rubber stamp; encode both in the project's AI instructions, next to F-38.

---

*Next deposits:* endpoint implementation micro-conventions (after M0/M1 set them), Cosmos partition/versioning rules (M1), outbox-via-change-feed design (first integration event), reminder-scheduling topology (M2), and any decision this catalog's recommendations get *wrong* — recording reversals is the factory's best training data.
