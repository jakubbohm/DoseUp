# c002-add-membership-domain — Tasks

Order is dependency-driven; every task ends with `dotnet build DoseUp.slnx` and `dotnet test DoseUp.slnx` green. No API contract, AppHost-graph, settings, or feature-flag work in this change — the corresponding standing tasks (TS regen, Bicep, appsettings, flag removal) are deliberately absent.

## 1. SharedKernel result vocabulary (#98, #38 domain half)

- [x] 1.1 Rename `Result` → `ApiResult` (#98): type + file, `ResultProblemDetailsMapper` and its tests follow (`ApiResultProblemDetailsMapper`), every code usage updated — pure rename, no behavior change; build + tests green.
- [x] 1.2 Add `DomainResult` (non-generic) and `DomainResult<T>` (design D8): `Success | RuleViolations`, conversions `RuleCheck.Fail` → `DomainResult` and `DomainResult` → `ApiResult` (failure-side lossless; non-generic `Success` → `ApiResult.Success`); SharedKernel unit tests cover both forms and all conversion scenarios from the spec delta.

## 2. Membership Domain (#53)

- [x] 2.1 Add `Ardalis.SmartEnum` (+ `Ardalis.SmartEnum.EFCore`) to central package management — verify current versions against the .NET 11 preview stack (preview-riding rule) before pinning.
- [x] 2.2 Create `Modules/Membership/Domain`: `AccountId` (v7 `Create()`), `EntraObjectId` (`ITypedId`, `From` only — never minted here), `AccountStatus` SmartEnum (`Active = 1`, `Disabled = 2`).
- [x] 2.3 `Account` aggregate: `SignUp(oid, displayName, email, now)` (guards on blank name/email, lands `Active` with a comment recording the OQ-5 default), `CheckCanDisable`/`Disable`, `CheckCanReactivate`/`Reactivate` returning `DomainResult` with codes `account.not-active` / `account.not-disabled`; no domain events, no admin state.
- [x] 2.4 Unit tests `tests/DoseUp.UnitTests/Modules/Membership/Domain/AccountTests.cs` covering every `membership-accounts` domain scenario (signup values + v7 identity, guard throws, both transitions incl. refusal codes and pure static affordances); add `Modules/Membership/README.md` declaring the module grade (full domain discipline, small surface; notes it migrates into `MembershipModule.cs` when d18/#39 resolves).

## 3. Architecture rule (#97)

- [x] 3.1 Add the Domain-`ApiResult` ban to `DomainDisciplineTests` (Domain namespaces must not reference `ApiResult`; `RuleCheck`/`RuleViolation`/`DomainResult` allowed) and register it in the testing.md catalog at the next free rule number; prove it by a deliberate violation failing (evidence in the PR description), then remove the violation.

## 4. Membership persistence (#53)

- [x] 4.1 Add `EFCore.NamingConventions` 10.0.1 (spike-#93 GO); create `Modules/Membership/Infrastructure/Persistence`: `MembershipDbContext` (lowercase `Schema = "membership"` const, `HasDefaultSchema`, `DbSet<Account>` only), `AccountConfiguration` (PK `ValueGeneratedNever`, unique index on the identity link, SmartEnum-int status, mandatory text columns), design-time factory; every options path (runtime, factory, migration runner) applies `UseSnakeCaseNamingConvention()` + `MigrationsHistoryTable("__ef_migrations_history", Schema)`; land the Platform typed-id model-builder convention (D5) and wire the domain-event interceptor.
- [x] 4.2 Generate the initial Membership migration and review it against the three spike authoring rules (lowercase schema, no hand-authored non-snake_case identifiers, renamed history table) and the expected `ix_accounts_entra_object_id` unique index; commit migration + snapshot.

## 5. Composition switch — placeholder retired (#55)

- [x] 5.1 Switch `Program.cs` and `MigrationWorker` to `MembershipDbContext` (keep the d18/#39 "generalizes later" comments); delete `Platform/Persistence/{DoseUpDbContext, DoseUpDbContextFactory, Migrations/*}`; update `DbContextDiscovery` and any placeholder-aware arch/hygiene tests.
- [x] 5.2 Full suite green including the Aspire harness (integration tests boot the migration runner against the new schema — this is #53's "migrates cleanly" and #55's "migrations run green from Membership alone" proven together).

## 6. Docs & config sweep (all decided 2026-07-19 — design D7/D8)

- [x] 6.1 ADR-0002: § Authorization ring-1 amendment (`CallerContext` carries `AccountId` only; account **status** is a DB column; admin gate's backing store = future permission-model change; lifecycle-in-app principle survives), caller-class `revoked` → `disabled` in § Testing, dated amendment note in the header.
- [x] 6.2 conventions/README.md (§ Authorization mirror; § Persistence: spike-#93 GO + three authoring rules + version-range expiry, replacing the pending-spike clause), testing.md (caller class, new catalog rule, Stryker §7 deferral note), domain-rules.md (`CheckCanXxx` examples in §4/§5/§8; §3/§8 domain methods return `DomainResult`; §1 `ApiResult`), roadmap backlog Stryker line.
- [x] 6.3 #98 rename sweep of the remaining mentions: CLAUDE.md, `openspec/config.yaml` context blurb (also drop its "admin flag" phrasing — D7.3); ADR mentions stay historical.
- [x] 6.4 Update `.claude/memory/doseup-project-state.md` with the c002 state (memory edits ride this PR — memory-changes-ride-the-pr rule).

## 7. GitHub pass (mutations on plan sign-off; PR-body duty)

- [x] 7.1 Edit issue #53 body: "(active, admin)" → "(active, disabled)".
- [x] 7.2 Comment on #38: domain-half resolved (`DomainResult` both forms + `ApiResult` rename, generic shipped by explicit growth-rule exception); handler/endpoint half stays parked for #54's call sites; issue remains open.
- [x] 7.3 Set #98's milestone to M0 (it closes with this change).
- [x] 7.4 PR body carries `Closes #53`, `Closes #55`, `Closes #97`, `Closes #98`, `Closes #99` (matches the proposal Tracks line — opsx:verify checks this) **and the rule-21 deliberate-violation evidence** (#97 done-criterion; proof preserved in checkpoint commit `f52ad4a`'s message and session scratchpad `rule21-proof.md`; the generalized rule re-proven 2026-07-20 on `SharedKernel.Domain.DomainResult` itself). Done: PR #103.

## 8. Archive-time note (for opsx:sync/archive, not implementation)

- [ ] 8.1 When syncing deltas into main specs, also update the **Purpose** paragraphs of `shared-kernel-primitives` and `error-contract` (they mention `Result`; delta operations cover requirements only) so #98's "every mention in the specs" done-criterion holds.

## 9. Review response (2026-07-20 — code-review findings + #99 folded in)

- [x] 9.1 Inference-first entity configuration: no-op `IsRequired()` calls removed (the load-bearing `Property(CreatedAt)` mapping stays — setterless properties are convention-invisible); recorded in conventions/README.md § Persistence + software-factory F-89.
- [x] 9.2 Failure payloads guarded and snapshotted: shared `RuleViolationCarrier` base for every violations-carrying case; `ApiResult.Validation` construction-guarded; null-status guards on the static `CheckCanXxx` affordances; spec delta updated accordingly (no longer "pure rename").
- [x] 9.3 `default(DomainResult)` crash reaffirmed as designed (c001 — no catch-all arm ever) and pinned by a unit test.
- [x] 9.4 CA1508 exemption kept module-wide with an explanatory comment; removal tracked in #101 under the #100 ".NET 11 GA" umbrella; NamingConventions expiry tracked as #102.
- [x] 9.5 Placeholder-context doc stragglers fixed (ADR-0002, conventions/README.md — past tense, c002 reference).
- [x] 9.6 #99: `DomainResult` relocated to `SharedKernel/Domain`; conversions inverted onto the edge (`ApiResult.From(...)`; domain-side files mention the edge nowhere, comments included); rule 21 generalized to the direction rule and deliberate-violation-proven; typed-id registration module-scoped (`(assembly, namespacePrefix)`, null = deliberate whole-assembly hatch); bullet 4 decided 2026-07-20 — the whole `Rules` vocabulary merged into `SharedKernel/Domain` (`RuleSet` async nuance recorded in D8).
- [x] 9.7 Verification pass (16-agent adversarial workflow, 7 distinct confirmed findings, all fixed): `Validation` deep snapshot + per-field guards with tests; rule 21 re-anchored on `typeof` (vacuous-pass rot); missing pins added (generic-form snapshot, both `ApiResult.From` null guards); membership-accounts delta gained the null-status-guard requirement; design.md risk/open-question stragglers aligned with #102.
- [x] 9.8 Review finding 10: `DisplayName`/`Email` become get-only (dead `private set` removed — a future rename/change-email feature adds its guarded method and restores the setter consciously); explicit `Property()` mappings added for both (setterless ⇒ convention-invisible, F-89).
