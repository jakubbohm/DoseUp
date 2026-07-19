# c002-add-membership-domain — Design

## Context

c001 left the repo with a proven pipeline and an empty middle: SharedKernel primitives (`Result`, `RuleCheck`/`RuleSet`, `Entity`/`AggregateRoot`, `ITypedId` + `TypedIdConverter`, the domain-event dispatcher and its `SaveChanges` interceptor) all exist and are unit-tested, but no module uses them; persistence is the bootstrap-placeholder `DoseUpDbContext` mapping nothing. This change births **Membership** — the Account aggregate and the `membership` schema — and retires the placeholder ([proposal](proposal.md); ADR-0002 module tree). Downstream consumers this design must serve: `ActiveAccount` resolution (#47) looks an Entra `oid` up in the account table on every authenticated request; *get account detail* (#54/#42) reads it; the complete-signup slice (future change) calls the aggregate's factory.

Constraints: [conventions/domain-rules.md](../../../docs/conventions/domain-rules.md) (two-layer rule checking, static-first affordances, stable violation codes) · [conventions/README.md § Domain model base types / § Persistence](../../../docs/conventions/README.md) (typed ids, v7 client ids, SmartEnum, snake_case F-88) · [ADR-0002 § Persistence is module property](../../../docs/adr/0002-architecture-style.md) (module schema, own history table, boundary rules 17–19) · spike #93 GO + its three authoring rules (`spikes/snake-case-naming`).

## Goals / Non-Goals

**Goals:**

- The Account aggregate as pure, framework-free, unit-tested domain code — the first real subject of the domain conventions, written to be the template future aggregates copy.
- The domain layer's own result vocabulary: `DomainResult` in SharedKernel, the edge union renamed `ApiResult` (#98), and an architecture test banning `ApiResult` from Domain namespaces (#97 — together, the domain half of #38).
- The `membership` schema as the first module-owned migration, authored per the spike-proven snake_case rules, applied by the migration runner locally and in the harness.
- The placeholder context, its migration, and its factory gone (#55) with the arch/discovery tests updated.
- The decided-2026-07-19 doc batch landed: `CheckCanXxx` naming examples, admin-trace cleanup, caller-class `revoked` → `disabled`, spike-#93 outcome + expiry recorded, Stryker deferral recorded.

**Non-Goals:** endpoints, handlers, or validators (first slice is #54's change) · auth wiring (#47) · profiles (M1) · admin/permission model (future change; M3 consumes it) · OQ-5 admission gating (stays open; `SignUp` lands `Active` as the recorded default) · domain/integration events · generalized per-module registration (d18, #39) · Stryker (deferred).

## Decisions

### D1 — Aggregate shape: two orthogonal dimensions, no admin anything

`Account : AggregateRoot<AccountId>` carries `EntraObjectId` (immutable), `DisplayName`, `Email` (both mandatory strings), `Status` (`AccountStatus` SmartEnum: `Active = 1`, `Disabled = 2`, append-only ints), `CreatedAt` (`DateTimeOffset`, factory-supplied `now`). Status is one dimension; authorization *capability* is deliberately absent — a permission model is a future change, so the entity carries **no admin flag** (decided 2026-07-19, reversing ADR-0002's earlier "admin flag is a DB column" wording — see D7). *Alternative rejected:* one merged state enum (`Active/Admin/Disabled`) — cannot express a disabled admin, conflates lifecycle with capability; the issue-#53 phrasing that suggested it is corrected by this change.

`DisplayName`/`Email` stay plain strings: shape validation (length, format) is the signup slice's contract validation (taxonomy class 4); the domain guards only against bug-class nulls/whitespace (`ArgumentException.ThrowIfNullOrWhiteSpace`). Email is **not unique** — `EntraObjectId` is the identity key; email is contact data and Entra owns identity-level questions. No value objects: neither value has behavior (conventions: records are the VO mechanism *when one is warranted*).

### D2 — State machine and rules: `CheckCanXxx`, static-first

```
Account.SignUp(oid, displayName, email, now) ──▶ Active ──Disable()──▶ Disabled
                                                   ▲                      │
                                                   └────Reactivate()──────┘
```

- `SignUp` mints `AccountId` via `AccountId.Create()` (`Guid.CreateVersion7()` — conventions § Domain model base types) so the aggregate is born with its identity; lands `Active` (recorded OQ-5 default, not a closure of OQ-5).
- Transitions follow domain-rules.md §3 two-layer form: static-first affordances `CheckCanDisable(AccountStatus)` / `CheckCanReactivate(AccountStatus)` (+ instance conveniences), and `Disable()`/`Reactivate()` re-assert them, returning **`DomainResult`** (D8 — never the edge `Result` union).
- Violation codes (contract, stable forever): `account.not-active` — "Only an active account can be disabled." · `account.not-disabled` — "Only a disabled account can be reactivated."
- Transitions take no `now` — nothing timestamps them yet; audit columns are the M3 admin change's call.
- **No domain events raised.** Explicit handler orchestration is the default (ADR-0002 § Events); nothing reacts to signup or disabling yet, and the future "signup creates a default profile" is same-module, same-UoW handler orchestration anyway.
- Naming decided 2026-07-19: rule checks are **`CheckCanXxx`** (`CanXxx` in the conventions was directional; member names are declared tunable in the domain-rules.md header). This change updates the domain-rules.md examples (§4/§5/§8: `CheckCanEdit`, `CheckCanChangeTiming`) — zero code impact, no `CanXxx` exists anywhere. Wire-projected affordance flags stay `canEdit`-shaped (DTO naming, not rule naming).

### D3 — `EntraObjectId`: a typed external-identity reference riding `ITypedId`

`readonly record struct EntraObjectId(Guid Value) : ITypedId<EntraObjectId>` in Membership Domain. It is not an aggregate id, but implementing `ITypedId` buys exactly what typed ids exist for — `Where(a => a.EntraObjectId == someAccountId)` must not compile (the ring-1 lookup is the hottest query in the system) — plus a free ride on the one generic converter. It defines `From` but **no `Create()`** — DoseUp never mints one; Entra does. *Alternatives rejected:* plain `Guid` (swap-typo compiles — the exact failure mode conventions bought typed ids to kill); plain record struct without the interface (hand-registered converter, no gain).

### D4 — Persistence mechanics: the spike's proven spellings

`Modules/Membership/Infrastructure/Persistence/`: `MembershipDbContext` (+ `AccountConfiguration : IEntityTypeConfiguration<Account>`, design-time factory, `Migrations/`). Directional shape, lifted from the spike:

- `public const string Schema = "membership"` — **authored lowercase** (spike rule 1: explicit names pass through verbatim); `modelBuilder.HasDefaultSchema(Schema)`.
- `UseNpgsql(..., npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", Schema))` — spike rule 3: EF names its history table explicitly, so the package would leave it quoted-PascalCase with snake_case columns inside; rename by hand, into the module's schema (ADR-0002: each context keeps its own history).
- `.UseSnakeCaseNamingConvention()` on every path that builds options — runtime (`Program.cs`), design-time factory, and the migration runner all reach the same model. The design-time factory mirrors c001's: hard-coded never-opened connection string, no host.
- `DbSet<Account>` only (`IAggregateRoot`-keyed arch rule). PK `ValueGeneratedNever` (client v7 ids). **Unique index on `entra_object_id`** — the ring-1 lookup path and the set-rule backstop for "one account per identity"; the generated name (`ix_accounts_entra_object_id`) is deterministic and snake_case, so the future signup slice can map the `DbUpdateException` by constraint name per domain-rules.md §6. No check constraints in this schema yet; the spike's rule 2 (hand-authored constraint names *and expressions* are snake_case) is recorded in conventions for when one appears.
- `AccountStatus` persists as its int `Value` via `Ardalis.SmartEnum.EFCore`; strings as `text` (validator-bounded, no arbitrary varchar); `CreatedAt` as `timestamptz`.

### D5 — Converter registration: the Platform model-builder convention lands now

`ITypedId`'s own doc promises "one generic value converter and one Platform model-builder convention cover every id" — the converter exists, the convention doesn't. It lands here as a Platform helper the module context calls from `ConfigureConventions`: scan the API assembly for `ITypedId<>` implementors, register `TypedIdConverter<T>` for each via `Properties(type).HaveConversion(...)`. SmartEnum conversion registers alongside (mechanism tuned in implementation — per-type in `ConfigureConventions` or per-property in the entity configuration, whichever the EFCore package's converter shape favors). *Alternative rejected:* per-property converter registration in each entity configuration — a per-aggregate ritual that will silently drift the day someone forgets it; the whole point of the generic converter is that ids are handled once.

### D6 — Composition: hard-wired, d18 stays honestly open

`Program.cs` swaps `AddDbContext<DoseUpDbContext>` for `AddDbContext<MembershipDbContext>` (same interceptor wiring, same `EnrichNpgsqlDbContext` with health checks off, same `doseupdb` connection string — **no AppHost graph change**). `MigrationWorker` migrates `MembershipDbContext`. With one module there is nothing to generalize — the per-module registration shape (d18, #39) stays open, and both call sites keep their "generalizes in d18" comment. The module's **declared grade** (ADR-0002 § Per-module rigor) lands as `Modules/Membership/README.md` — "full domain discipline, small surface" — because `MembershipModule.cs`'s shape *is* the open d18 decision; the README moves into it when d18 resolves. No new capability spec for the runner switch: behavior (migrations apply before the API serves) is unchanged.

### D7 — The doc batch: amendments, mirrors, cleanup

One pass, docs-first, all decided 2026-07-19 in the c002 interview:

1. **ADR-0002 § Authorization ring 1 amendment:** `CallerContext` carries `AccountId` only; "account status is a database column" (drop "and the admin flag"); the admin gate's backing store is explicitly the future permission-model change; surviving principle: lifecycle state lives in-app, never in Entra app roles. `AdminOnly`-group mechanics and "Admin ≠ data access" stand. Amendment note added to the ADR header.
2. **Caller-class rename** `revoked` → `disabled` in ADR-0002 § Testing and [testing.md](../../../docs/conventions/testing.md) § 4 (vocabulary alignment with `AccountStatus.Disabled`; FR-2's product-level "revoke" verb stays).
3. **Mirrors:** conventions/README.md § Authorization; `openspec/config.yaml` context blurb (same stale "admin flag" phrasing).
4. **domain-rules.md** example rename to `CheckCanXxx` (D2) + the §3/§8 `DomainResult` updates (D8), and the **#98 rename sweep**: every edge-union `Result` mention in conventions docs, CLAUDE.md, and the config.yaml blurb becomes `ApiResult` (ADR mentions stay historical).
5. **conventions/README.md § Persistence:** record the spike-#93 GO, the three authoring rules, and the version-range expiry (currently the paragraph still says the spike is pending and points the block at #42).
6. **testing.md § 7 / roadmap backlog:** Stryker spike recorded as deferred out of this change.
7. **Issue #53 body** "(active, admin)" → "(active, disabled)" — tracker mutation, executed on plan sign-off per project-management.md.

No new ADR: every cross-cutting decision here amends ADR-0002 or the conventions, which are the recording surface for exactly this (config design rule satisfied by reference).

### D8 — `DomainResult`: the domain layer's result union (steering 2026-07-19, the domain half of #38)

Domain methods do not return the SharedKernel `Result`: its taxonomy is the edge's (`NotFound`, `Forbidden`, `Validation`, `Unexpected` are request/resource concepts an aggregate cannot meaningfully produce), and #38's parked objection lands exactly here now that the first real domain call sites exist. New SharedKernel union:

```csharp
public union DomainResult {
  case Success;
  case RuleViolations(/* the same violation payload RuleCheck.Fail carries */);
}
```

- **Only two things can happen** in a rule-guarded domain operation: it happened, or rules were violated. The union says precisely that — bugs still throw (guards), and everything edge-shaped stays the handler's vocabulary.
- **Conversions, both SharedKernel:** `RuleCheck.Fail` → `DomainResult` (the aggregate's re-assert: check fails ⇒ return violations); `DomainResult` → `ApiResult` (the handler's last step maps `RuleViolations` → `ApiResult.RuleViolations` → 409; the wire contract is untouched).
- **Name:** `DomainResult` — names the owning layer, which self-polices (the type appearing past a handler reads wrong on sight); keeps the `Result` family word; case names mirror the edge union's so the mapping is mentally 1:1. *Rejected:* `CheckedResult` (says nothing about the layer), `DomainMethodResult` ("Method" adds length, not meaning), `Outcome` (layer-neutral hedge for a future handler share — but handlers produce `NotFound`, not a domain concept, so a future share *should* hurt and be decided consciously, not eased by a vague name).
- **The edge union renames to `ApiResult` in the same change (#98):** once two layers own result types, the bare `Result` no longer says whose it is — `ApiResult` ↔ `DomainResult` are distinguishable on sight, each naming its owner. Pure rename (type, file, `ResultProblemDetailsMapper` + tests, main-spec and conventions-doc mentions incl. CLAUDE.md and the config.yaml blurb; ADR mentions stay historical per #98's scope); behavior and the HTTP contract are untouched, and #38's *shape* question stays open.
- **Shape discipline:** both forms ship in c002 — the non-generic for void-shaped mutations (`Disable`/`Reactivate`) and `DomainResult<T>` whose `Success` carries the operation's value. The generic has **no consumer here** (`SignUp` stays rule-free by design: mandatory-data blanks are bug-class guards, and identity uniqueness is a set rule owned by handler + constraint, never the factory) — shipping it anyway is a **recorded, Jakub-directed exception to the SharedKernel growth rule** (2026-07-19: the pair is complete the day the pattern is taught, nothing to remember later; first consumer expected M1, e.g. a rule-guarded `AddProfile`). Its `RuleViolations` side converts to `ApiResult` exactly like the non-generic's; the `Success` value comes out by pattern matching, not conversion (the edge `ApiResult.Success` carries no payload — how handlers return *data* is #38's parked half). No `Task` variants at this layer ever: domain methods are sync by convention (pure checks, no I/O); asynchrony is a handler concern and belongs to #38's open half.
- **Enforcement (#97):** new arch-catalog rule — Domain namespaces never reference the edge union (`ApiResult`; `RuleCheck`/`RuleViolation`/`DomainResult` stay allowed); testing.md catalog gains the rule at the next free number, `DomainDisciplineTests` carries it, and a deliberate violation is demonstrated failing before the rule is trusted (#97's done-criterion). Docs-first ripple: domain-rules.md §3 ("fails fast … as `Result`" → `DomainResult`) and the §8 handler walkthrough (step 4 maps the domain result at the end); conventions/README.md § SharedKernel discipline seed list gains the type.
- **#38 stays open:** a comment (on plan sign-off) records this as the domain-half resolution and names the suspicion that module handlers want the same revisit — whether they keep `Result`, share `DomainResult`, or get their own type is deliberately left to the parked handler/endpoint half, judged against #54's real call sites.

### D9 — Testing: unit layer only, existing gates do the rest

Per testing.md placement: pure domain → `tests/DoseUp.UnitTests/Modules/Membership/Domain/AccountTests.cs` (behavior-sentence names; covers: signup lands active with identity/name/email/`CreatedAt = now`; check-affordances are pure functions of status; transitions re-assert and return the stable codes; guard behavior on null/whitespace). `DomainResult` (both forms) and its conversions get SharedKernel unit tests beside the existing suites, which follow the `ApiResult` rename mechanically (#98) — the unit layer is the generic form's only exerciser until its first domain consumer arrives. Arch tests gain exactly **one new catalog rule** — the Domain ban on `ApiResult` (D8, #97, deliberate-violation-proven) — while 1–7, the SmartEnum ban, and 17–19 acquire their first real subject; `DbContextDiscovery` and any placeholder-aware tests are updated by the #55 deletion. The harness proves "migrates cleanly" implicitly (AppHost gates the API on the migration runner) — no new integration tests. Slice tests arrive with the first slice (#54's change), as the testing decision tree dictates.

## Risks / Trade-offs

- **[NamingConventions GO expires at EF 11 stable]** — range `[10.0.1, 11.0.0)` satisfied only by prerelease ordering; EF 11.0.0 stable → `NU1608` → hard restore failure under `TreatWarningsAsErrors`. Loud, and on a bump we control → owned by the standing preview-bump activity; recorded in conventions (D7.5); named fallback: hand-rolled ~20-line `IModelFinalizingConvention` (mechanism swap, decision intact). Follow-up issue = Jakub's call (proposal).
- **[Silent naming-rule violations in future authoring]** — explicit names and raw SQL bypass the package; two of three failure modes are silent. → The three rules live in conventions § Persistence (D7.5), and this change's migration is reviewed against them; the check-constraint case additionally fails loudly on apply (harness would catch it).
- **[Stray placeholder artifacts in existing local volumes]** — deleting the placeholder migration orphans its `__EFMigrationsHistory` in any existing local dev volume; no code references it. → Local volumes are disposable (recreate via `aspire`); no deployed database exists yet (M0's Azure legs land later), so prod is untouched. Accepted.
- **[`SignUp` → `Active` pre-empts OQ-5 in code]** — a later admission decision may insert a state before `Active`. → SmartEnum ints are append-only (a new state is additive), the default is recorded as such in code comment + spec, and OQ-5 stays formally open (vision).
- **[README-declared grade instead of `MembershipModule.cs`]** — a nonstandard home relative to ADR-0002's letter. → Deliberate: creating `MembershipModule.cs` now would concretize the d18 registration shape ahead of its decision; the README notes it migrates there. Revisited when d18 resolves (#39).

## Migration Plan

No deployed environment exists yet — this is a local/harness-only schema change. Order inside the change: domain (+ unit tests green) → persistence + first migration (spike rules applied) → composition switch + placeholder deletion (build + full suite green) → docs batch. Rollback = revert the PR; no data motion anywhere.

## Open Questions

- **None blocking implementation.** Parked, with owners: OQ-5 admission gating (vision, before M3) · permission model shape (future change; ADR-0002 amendment marks it) · d18 module registration (#39, when a second module or the first slice forces it) · NamingConventions-expiry follow-up issue (Jakub's call, proposal § GitHub) · how Jakub's own account first becomes admin (the permission-model change; nothing in M0 needs it) · #38's handler/endpoint half — whether handlers keep `Result`, share `DomainResult`, or get their own type (parked; judged against #54's real call sites).
