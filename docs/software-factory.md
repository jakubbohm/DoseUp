# Software Factory — Decision Catalog (seed)

**Status:** knowledge deposit, not project documentation · **Snapshot:** 2026-07-14 · **Source:** DoseUp founding interview

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
DoseUp: **both kinds from day one** — domain events sync in-module/in-UoW; integration events async post-commit via outbox (outbox row in the same DB transaction + background dispatcher — mechanism revised by the PRE-2 reversal, originally Cosmos change feed; in-process transport first, broker-shaped seam).
*Generalizes:* the two-kind vocabulary is reusable everywhere; the outbox mechanism is per-database (change feed on Cosmos, outbox table + poller on SQL). Skip integration events entirely while there's only one module — unless establishing the pattern is itself a goal.

**F-19 · Validation layering?**
DoseUp: **two layers, two channels** — request shape at the edge (FluentValidation → 400 ProblemDetails), business invariants in the domain (→ Result → ProblemDetails).
*Generalizes:* near-universally correct; the alternatives (all-at-edge, all-in-domain) each leak one concern into the other's home.

**F-43 · In-process dispatch: mediator or direct calls?**
DoseUp (PRE-4): **direct** — the REPR endpoint *is* the handler; no mediator of any kind. Pipeline concerns live in the endpoint framework (validation, processors) and middleware (OTel); dispatch indirection would blind architecture tests and IDE/AI navigation. Context: MediatR v13 went commercial (2025, RPL/commercial dual license).
*Generalizes:* AI-assisted coding flips the old trade — the boilerplate a mediator amortized is now free, while its opacity still costs at review, navigation, and architecture-test time. With vertical slices + a REPR framework there is nothing left for `IMediator.Send()` to do. License-check messaging libraries like test libraries (F-25).

**F-44 · Asynchronous messaging: hand-roll, or which framework?**
DoseUp (PRE-4; transport revised by PRE-3 the same day): **Wolverine** (MIT, open-core JasperFx), confined to the async seam; **Azure Service Bus Basic** as transport (~$0.05/M ops, managed identity — reversed from the hours-earlier CloudAMQP pick: same ~€0, one vendor and one secret fewer, gated on the M0 queue-only spike); KEDA queue-depth scaler wakes ACA from zero. Rejected: hand-rolling (distributed reliability — retries, poison handling, idempotency, crash recovery — is bought, not built), MassTransit (v9 commercial Q1 2026, $400/month floor; v8 security-only through 2026), NServiceBus (always commercial). HTTP stays with the dedicated endpoint framework: Wolverine.HTTP's OpenAPI/versioning gaps (2026-07) lose to FastEndpoints where the contract pipeline is load-bearing.
*Generalizes:* the outbox semantics to demand from any candidate: envelope written in the business transaction, immediate post-commit dispatch (no polling latency), startup recovery + relaxed sweep as backstop, at-least-once ⇒ idempotent consumers. Confine framework conventions to the async seam; keep the sync path explicit. And scheduled *messages* over editable domain state need two guards — fire-time validation against current state, and eager re-arm on change (cancellation then becomes mere noise reduction); on brokers that can't carry them (RabbitMQ's delayed-exchange plugin: "not a longer-term scheduling solution"), a storage-queue visibility timeout is the free stand-in (F-47).

**F-47 · Time-based triggers on scale-to-zero infrastructure?**
DoseUp (PRE-3): **Azure Storage Queue "visibility-timeout alarms"** — one in-flight *next occurrence* message per schedule, enqueued invisible (≤ 7 days per hop), visible exactly at due time; KEDA's azure-queue scaler with `queueLengthStrategy: visibleonly` (the default counts invisible messages and would nullify scale-to-zero) wakes the app; the fire handler validates against the aggregate (stale → discard), sends the push, re-arms the next occurrence through the outbox; `popReceipt` update/delete revokes early as an optimization. Rejected: minute-cron sweep (keeps the serverless DB awake ≈ €11/month; *the* right answer at large scale where batching wins — crossover recorded), ASB Standard scheduled messages (identical semantics, ~€9/month base — the paid upgrade path behind the `IReminderAlarm` port), Functions timer (free compute, same DB-wake), Durable Functions timers (sound, but a second runtime for one alarm clock).
*Generalizes:* on scale-to-zero stacks the scheduler cost is never the tick's compute — it's *what the tick touches*: a per-minute probe of a scale-to-zero database silently converts it to always-on. The robust shape regardless of backend: queue = dumb alarm, domain state = truth, fire-time validation = correctness, revocation = optimization. Free-tier feature matrices decide hobby-scale architectures — check them before committing to the pattern, not after (F-39).

**F-52 · Public scale-to-zero ingress: can authentication gate the wake?**
DoseUp (PRE-10): **No — nothing auth-shaped prevents bot wakes** (verified 2026-07): ACA's built-in auth runs as a sidecar *per replica* — at zero replicas no auth layer exists to reject anything; the HTTP scale rule counts requests at ingress before any authentication; IdP-side gates ("require user assignment") stop token *issuance*, and bots never ask for tokens. The only pre-replica filter is ingress IP restriction — useless for roaming phone users. Posture: price the wake instead of preventing it (~75 vCPU-s per wake against a 180k vCPU-s monthly free grant = cents; first bill watches it), and **keep the expensive resource out of the unauthenticated path**: token validation local (cached OIDC metadata), DB-backed account gate only after authentication, **health probes DB-free** (orchestrator defaults love DB health checks — each wake would drag the scale-to-zero database up with it). Custom domains land in Certificate Transparency logs and scanners find them in hours — obscurity is not a plan. Contingency: free-tier CDN/WAF front, origin ingress IP-restricted to its ranges.
*Generalizes:* on scale-to-zero infrastructure the wake decision happens **below** every authentication layer you control. Same lesson-shape as F-47: it's never the wake itself, it's what the wake touches — audit the unauthenticated path (including probes) for touches on pay-per-wake resources.

## D. API & contract

**F-20 · Endpoint framework style?**
DoseUp (.NET): **FastEndpoints** (REPR pattern — one endpoint class per use case, natural slice fit).
*Generalizes:* per-stack question; the invariant is *one use case = one addressable unit*, whatever the framework.

**F-21 · Error contract?**
DoseUp: **ProblemDetails (RFC 9457) for every non-2xx**, from validation and Result mapping alike.
*Generalizes:* adopt the platform's standard problem format everywhere; a single documented case→status matrix, no ad-hoc error JSON.

**F-22 · Frontend/backend contract sync?**
Options: OpenAPI → generated client (committed) · TypeSpec contract-first · docs only.
DoseUp (firmed by PRE-6): **OpenAPI → generated TS types, committed** — FastEndpoints `--exportswaggerjson` → committed `openapi.json` → **openapi-typescript** types file, consumed via **openapi-fetch** (`{ data, error }` = the Result union continued into TS). Regeneration is a mandatory task inside any spec change that touches the API; **CI only verifies** (regen + diff = drift gate over both artifacts). PR diffs thus show contract evolution twice — in OpenAPI terms and TS terms. Runner-up: hey-api (equal type fidelity, named/class SDK ergonomics, TanStack plugin; pre-1.0 churn, bundles its client runtime as generated source, broke on TypeScript 7 during evaluation). Rejected on generated-output evidence: Kiota / FastEndpoints.ClientGen.Kiota — models erase `required` by design (Graph-style permissive shapes, wrong trade for a first-party gated client), `@ts-ignore` on every generated function, throws where the edge speaks Result, TS pipeline officially preview (2026-07); NSwag TS (dated class/exception patterns).
*Generalizes:* "change process triggers regen, CI gates drift" beats watcher-magic and beats CI-side generation — the artifact stays reviewable and the duty stays visible in the task list. Prefer the generator whose committed artifact is smallest (types-only): the PR diff *is* the contract delta. And judge generators empirically — generate candidates against a sample spec and read the output; "SDK-shaped" ≠ type-faithful (F-48).

**F-45 · Endpoint payload vs. handler payload — one type or two?**
DoseUp (PRE-4): **one** — the slice's request/response DTO is simultaneously the wire contract and the handler input; a second, internal type appears only when the public contract must stay stable across an internal change. DTOs stop at the domain boundary (aggregates take values through methods). No anti-corruption layer until a foreign model is consumed.
*Generalizes:* anticipatory mapping layers are the most common ceremony in layered codebases — split on divergence, not on principle. What makes one-type safe is that the contract is gated elsewhere (generated client + CI drift gate, F-22).

**F-48 · Who maintains machine-derivable artifacts in an AI-assisted repo?**
DoseUp (PRE-6): **deterministic tools generate; the AI runs them and reads the diff.** The AI hand-maintains only layers whose mistakes are compile-loud — e.g. a thin ergonomic facade over the generated contract types, where wrong wiring cannot compile — never the contract artifact itself, whose transcription errors are silent until runtime. Considered and rejected: Claude hand-maintaining the TS client. The CI drift gate ("regenerate, fail on diff") is only expressible over byte-reproducible generation; verifying a hand-written client would take another LLM pass per PR — slow, probabilistic in both directions, and paid forever.
*Generalizes:* generator = truth, AI = ergonomics, type-checker = the bridge gate between them. The intuition "the AI edits just the small delta, cheaper than regenerating everything" inverts in practice: after regen the AI reads only `git diff` (exactly the minimal delta), while hand-maintenance pays reasoning tokens on every change *and* forfeits mechanical verifiability. After a contract change, the compile errors across facade/components *are* the migration to-do list. Never let an AI be a slow, fallible generator.

**F-50 · Denial semantics: 403 or 404?**
DoseUp (PRE-10): **404 for everything the caller can't see** — cross-account access to owned resources returns NotFound, deliberately indistinguishable from nonexistence (anti-enumeration; RFC 9110 explicitly permits 404 to conceal existence), including foreign ids referenced in payloads; falls out of ownership-scoped queries for free. **403 only for request-class denials** whose target isn't secret (revoked account, non-admin on admin endpoints). The Result union's `Forbidden` case is narrowed accordingly and stays mostly dormant until per-resource permission levels exist ("visible but not permitted" — e.g. a view-only caregiver trying to log a dose).
*Generalizes:* draw the 403/404 line *once, as a written convention per resource class* — ad-hoc per-endpoint choices leak existence through inconsistency (a 403 here, a 404 there is itself an oracle). Scoped-query ownership makes the safe answer the cheap one: the query returns nothing, the handler says NotFound, no dedicated check exists to get wrong.

## E. Data & auth

**F-23 · Database selection?**
DoseUp: **Neon serverless Postgres** — *a recorded reversal:* the founding interview chose **Azure Cosmos DB serverless** (Azure-native, document shape, change-feed outbox, ~free), and Jakub reversed it one day later at PRE-2, before any code. What won: relational modeling with real migrations, Neon branching for dev/CI, standard-Postgres portability — still serverless/~free at circle scale. (PRE-9 later refined the branching claim: dev/CI runs local containers; branching's real value is pre-deploy restore points — F-56.) Fact-check during the reversal (2026-07): Neon's Azure Native Integration is **retired** and its Azure regions deprecated (new projects AWS-only) — so the DB is a deliberate cross-cloud exception to the all-Azure stance.
*Generalizes:* the default ("relational Postgres unless something strong pulls away") reasserted itself — treat exotic-DB picks made mid-interview as provisional until a cooling-off re-check; recording the reversal is the factory's best training data. If a document DB does win: no relational migrations — demand versioning/expand-contract discipline; partition-key design is early and hard to reverse. And third-party "Azure-native" claims rot fast (F-39) — re-verify the integration exists at setup time.

**F-24 · Identity: external IdP · self-hosted · third-party?**
DoseUp: **Microsoft Entra External ID** (cloud-native CIAM, free tier, invite-only friendly).
*Generalizes:* never self-host credentials for hobby scale; pick the IdP native to the target cloud unless portability is a stated goal.

**F-46 · ORM & unit-of-work shape?**
DoseUp (PRE-4): **EF Core 11 previews + Npgsql previews** (fallback pin: EF Core 10 GA) — the `DbContext` *is* the unit of work: a `SaveChanges` interceptor drains aggregates' domain events and dispatches them synchronously inside the transaction; integration events become outbox envelopes in the same transaction; no `IUnitOfWork` wrapper.
*Generalizes:* don't wrap the framework's UoW in a homegrown one. The change tracker is what makes aggregate + event + outbox atomicity cheap — a micro-ORM reintroduces that bookkeeping by hand; choose one only where you aren't doing aggregate-style domain modeling (or for hot read paths beside the ORM).

**F-49 · Authorization: engine, framework, or plain code?**
DoseUp (PRE-10): **engine-free, three rings** — (1) authenticate (IdP JWT; endpoints secure-by-default, anonymous = explicit arch-tested allowlist), (2) request-class gates as ASP.NET named policies declared via FastEndpoints (`ActiveAccount` resolving the IdP subject to a DB-backed request-scoped `CallerContext`; one `AdminOnly` group), (3) ownership **by construction** — every profile-scoped query is account-scoped, misses = NotFound; no ownership check exists to forget. casbin.net rejected **on fit, not health** (v2.21.2 2026-06, Apache-2.0, active; ran on net11 preview 5 — verified by a 20-minute spike): the complete two-rule system came out as ~30 lines of runtime-interpreted DSL (opaque to compiler, architecture tests, and IDE — the F-43 mediator objection, worse) vs two C# boolean expressions, and the ownership branch still needed the resource row loaded from the DB first. Also rejected: FastEndpoints `Permissions()`/`AccessControl` codegen (permission-code machinery for two eternal roles), resource-based `IAuthorizationService` for ownership (imperative anyway, answers bool not Result, load-then-check leans 403/leaky where scoped queries yield 404 free).
*Generalizes:* inventory the *needs* before naming tools — count the roles, ask who edits policy and whether it changes at runtime, and classify the dominant check as **policy** (attributes an engine can own) or **data** (rows in your DB). Ownership/tenancy is data: an engine can only compare values handed to it after the load, and it can never scope a query — *authorization is data, not policy, until proven otherwise*. Engine-justification checklist: many/changing roles · non-developer policy authors · runtime policy changes · policy audit/simulation · policy shared across services — all "no" → write the booleans. If relationship sharing ever gets rich, the honest engines are ReBAC (OpenFGA/SpiceDB), not RBAC-file engines — and below thousands of users a `grant(subject, resource, level)` table still beats operating one.

**F-51 · App roles & account status: IdP claims or database?**
DoseUp (PRE-10): **database columns** (active + admin flag on the account row), resolved per request into `CallerContext`; the token stays a pure authentication artifact carrying the stable subject key (Entra's `oid` — `sub` is pairwise per app, a migration trap). Entra External ID *does* support app roles in external tenants (verified 2026-07), and "require user assignment" is enabled as an IdP-door duplicate of the invite-only rule — but the enforcement of record is in-app.
*Generalizes:* tokens outlive administrative changes — anything that must take effect *now* (revocation, demotion) cannot live only in claims; it needs a per-request check against current state. When the account lifecycle (invite/revoke/promote) is administered inside the app, the DB is the single source of truth and claims duplication is drift risk; put roles in tokens only when the IdP is where they're administered. Testability bonus: DB-backed roles need no IdP round-trip in tests.

**F-56 · Backups on a branch-capable serverless Postgres?**
DoseUp (PRE-9): **a named branch per deploy** (`pre-deploy-<run>`, instant copy-on-write, pruned to the last 3) as the rollback point, **plus scheduled `pg_dump` to Azure Blob** as vendor-independent DR (restore drill = M3/OQ-4). Why PITR alone doesn't cut it: Neon's Free-plan instant-restore window is 6 hours (2026-07) — a bad migration found next morning is already outside it, while a named branch persists indefinitely (10-branch plan limit ⇒ prune). Dev/CI branching rejected: local containers are faster, free, and isolated (refines F-23's founding expectation).
*Generalizes:* on copy-on-write databases the branch primitive doubles as the restore primitive — a pre-deploy branch is a free, instant backup that classic Postgres needs dump-time for; wire it into CD, not into human memory. But same-vendor snapshots are not DR: keep one dump stream on independent storage regardless of what the provider promises. And read the restore window *per plan tier* — free tiers quietly shrink it.

**F-57 · EF Core migration application & data seeding in CD?**
DoseUp (PRE-9, verified against EF docs 2026-07): migrations applied by a **self-contained migration bundle** run from CD before rollout — never `Database.Migrate()` at app startup (concurrency hazards; would hand the app DDL rights). Seeding split by *who may change the data afterwards*: **static catalogs** → `HasData` ("model managed data" — versioned with the model, explicit PKs, identical per version everywhere) · **seed-once-then-mutable** → hand-written data motion inside a migration (`InsertData`/`Sql` — applied exactly once per DB via the history table, later drift untouched) · **dev/test data** → `UseSeeding`/`UseAsyncSeeding` (EF 9+) registered only in the dev/test composition root (fires on `Migrate` even with nothing pending; implement both sync and async — tooling calls the sync one).
*Generalizes:* classify seed data by post-insert ownership — the three EF mechanisms map exactly onto that axis, and mis-mapping fails silently (`HasData` reverts admin edits on the next migration; insert-if-missing seeding resurrects deliberate deletions). The bundle-not-startup rule doubles as least privilege: the app role needs DML only; the migration path owns DDL.

## F. Testing

**F-25 · Test framework + assertions?**
DoseUp (2026-07): **TUnit** (MTP-native, source-generated; accepted weekly churn) + **Shouldly** (BSD, no license traps).
*Generalizes:* **always run a licensing check on test deps** (FluentAssertions went commercial in v8 — the lesson behind this question) and verify mutation-testing/tooling compatibility *before* committing to a framework. xUnit v3 is the boring-mature default; TUnit the bleeding-edge pick.

**F-26 · Integration-test harness altitude?**
Options: in-proc factory (WebApplicationFactory) · containers per dependency (Testcontainers) · full-orchestrator harness (Aspire testing).
DoseUp: **Aspire harness only** (`DistributedApplicationTestingBuilder` + Postgres container) — one harness, real graph; accepted cost: full startup even for persistence-level tests (revisit if painful).
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
DoseUp: **single prod, deploy on merge to main** — PR gates + feature flags + instant revert *are* the deployment gate. The original consequence — migrations must be forward-safe (expand/contract) — was renegotiated at PRE-9: a maintenance-window recreate on schema-changing deploys buys naive migrations instead (F-55).
*Generalizes:* staging earns its cost only when (a) real users would feel breakage and (b) something can't be verified pre-merge. A hobby circle with strong gates doesn't clear that bar.

**F-32 · Commit & release conventions?**
DoseUp: **trunk-based, PRs always, squash merge, Conventional-Commit PR titles (linted), automated changelog/semver releases.**
*Generalizes:* squash + conventional titles is the lowest-discipline path to automated releases; "PRs always, even solo" keeps gates authoritative and history uniform.

**F-33 · Feature flags?**
DoseUp: **Microsoft.FeatureManagement + Azure App Configuration**; every flag ships with a removal task.
*Generalizes:* trunk→prod *requires* flags or dark-launching; flag-removal-as-task is the anti-rot mechanism regardless of provider.

**F-53 · Infrastructure-as-code authorship: generated from the app model, or hand-authored?**
DoseUp (PRE-9): **hand-authored Bicep + Azure Deployment Stacks** (`--action-on-unmanage deleteResources` — git deletions delete in Azure; per-environment `.bicepparam`; GitHub→Azure via OIDC federated credentials; managed identities + minimal RBAC assigned in Bicep; no portal/CLI mutations ever). The generated path (aspire publish / azd) rejected after verification (2026-07): customization exists but lives in C# `ConfigureInfrastructure`/`PublishAsAzureContainerApp` lambdas (infra through escape hatches — the F-43 opacity objection again), no first-class per-environment parameterization, azd's stacks support is alpha, and regeneration overwrites hand-customization. The tempting middle — commit generated Bicep and drift-gate it (F-22's pattern) — dies on the same fact: the gate is only expressible over byte-reproducible generation, and customized output isn't. Sync is procedural instead: the AppHost models local orchestration only; a change touching its resource graph carries an explicit "update infra Bicep" task; the deploy smoke test backstops.
*Generalizes:* the F-22/F-48 drift-gate pattern has a stated precondition — **regeneration must be customization-free**; the moment humans edit generated output, process rules + runtime smoke replace mechanical gates. "The app model generates the infra" is attractive until multi-environment parameters and day-2 tuning arrive; purpose-built IaC (param files, what-if, linting) is the honest tool then. Tracked deletion (deployment stacks; Terraform/Pulumi state elsewhere) is the feature that makes IaC-only discipline enforceable rather than aspirational.

**F-54 · Where are deployable artifacts born?**
DoseUp (PRE-9): **built once per merge commit on main** (`release.yml`): images tagged with the git SHA + the migration bundle; identical artifacts promoted to every environment, with GitHub Environments holding per-env params/secrets and the required-reviewers approval gate. PR CI is gates-only and publishes nothing. Why not promote PR-built artifacts: squash merge makes the PR-head SHA unreachable from main; merge skew means the merged combination may never have been built or tested; and PR runners must not hold publish credentials.
*Generalizes:* "build once, promote everywhere" is right — but anchor the build on the *post-merge* commit unless the merge strategy preserves the tested SHA (fast-forward + merge queue). Squash-based flows always rebuild on main. GitHub Environments' required reviewers are the manual promotion button; no extra machinery needed.

**F-55 · Deployment downtime vs migration discipline?**
DoseUp (PRE-9, superseding the founding expand/contract mandate): **conditional recreate** — a deploy carrying migrations stops the app, runs the bundle, rolls images, starts (schema and code switch together ⇒ migrations may be destructive); migration-free deploys use the platform's zero-downtime replace. Cost accepted with eyes open: instant revert weakens on schema-changing deploys (the old image may not run on the new schema) — recovery is restore-the-pre-deploy-branch, losing minutes of writes, or roll forward.
*Generalizes:* zero-downtime is not free — its price is backward-compatible migrations (expand/contract) on every destructive schema change, forever. Small or internal user bases should usually buy naive migrations with a minute of downtime instead. The three-way constraint to put to the owner: {no staging · app-only instant revert · naive migrations} — pick two. Queue-buffered async work (F-47 alarms) tolerates the window by design; audit what else fires during it.

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
DoseUp: **OpenSpec** — behavior changes go through proposal/specs/design/tasks artifacts; proposals must cite requirement ids + milestone; archiving syncs specs and updates roadmap/requirement statuses. Change ids carry a 3-digit sequential prefix (`001-…`, PRE-12) so ordering and cross-references stay stable.
*Generalizes:* the traceability chain (requirement id → milestone → change → spec → code → test) is the product of this choice; any spec workflow that maintains it qualifies.

**F-38 · Who gates stage progression?**
DoseUp: **the human, explicitly.** The AI proposes, summarizes state after each stage/round, and waits; it never declares an interview or phase complete on its own.
*Generalizes:* encode this in the project's AI instructions (CLAUDE.md); it's the single highest-leverage trust rule for AI-assisted work — and the factory interview itself must obey it.

**F-39 · Research-before-recommend.**
DoseUp practice: every ecosystem-sensitive recommendation (runtime features, formatter, testing stack, licensing, product capabilities) was **verified against current sources during the interview**, not answered from model memory — which is how the FluentAssertions license, NetArchTest abandonment, Stryker×xUnit-v3 breakage, Cosmos vNext GA-but-experimental-in-Aspire, and Claude Design's June-2026 integration were caught. (PRE-2 added another catch: Neon's Azure Native Integration retirement + Azure-region deprecation, found while recording the database reversal.)
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

*Next deposits:* endpoint implementation micro-conventions (after M0/M1 set them), Postgres schema/migration rules (M1), Wolverine adoption conventions (first integration event), reminder-alarm fine-grained design (M2), and any decision this catalog's recommendations get *wrong* — recording reversals is the factory's best training data (first one landed: F-23).
