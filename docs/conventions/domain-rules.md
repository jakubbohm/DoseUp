# Domain rules & the error model

**Status:** decided 2026-07-14 · part of [conventions](README.md) — docs-first source of truth

The conventions here are **binding**; the C# signatures are **directional** — the shared-kernel change implementing them may tune ergonomics (member names, exact generic shapes), never the rules. Written for every future coder, human or Claude: read this before writing an endpoint, a feature handler, or a rule check.

## 1. The error taxonomy — every way a request leaves the happy path

Every non-2xx response is ProblemDetails (RFC 9457). Each error class has exactly one owner and one shape — never improvise a second mechanism for a class that already has one.

| # | Class | Example | Checked by | ApiResult case | HTTP |
|---|-------|---------|------------|-------------|------|
| 1 | Authentication | missing/expired token | Platform (JWT bearer, local validation) | — | 401 |
| 2 | Request-class authorization | disabled account; non-admin on admin group | Platform policies (`ActiveAccount`, `AdminOnly`) | — | 403 |
| 3 | Routing | unknown path/method | ASP.NET | — | 404/405 |
| 4 | Contract validation | missing field, bad range/format | FluentValidation — **first step of the feature handler** | `Validation` | 400 |
| 5 | Resource miss | id nonexistent **or not the caller's** — indistinguishable by design (anti-enumeration — [ADR-0002-architecture-style § Authorization](../adr/0002-architecture-style.md)) | account-scoped queries in the handler | `NotFound` | 404 |
| 6 | **Domain rule violation** | "only an active schedule can be edited" | aggregate self-checks + handler `RuleSet` (this document) | `RuleViolations` | 409 |
| 7 | Concurrency | optimistic-concurrency clash (reserved until needed) | persistence | `Conflict` | 409 (distinct PD `type`) |
| 8 | Unexpected | bugs, infrastructure failures | global exception middleware | `Unexpected` | 500 — never leaks internals |

Exceptions belong to class 8 only: a violated `Guard` (null argument, impossible state) *is* a bug — throw. Everything expected travels as the `ApiResult` union (ADR-0001) and maps to ProblemDetails in the **single Platform mapper** — endpoints never hand-craft error responses.

## 2. Vocabulary

- **Domain rule** — a named business precondition on an operation ("schedule must be active to edit"). Deliberately *not* called an invariant: invariants are always-true internal consistency, protected by constructors/methods and guarded by exceptions; rules are per-operation and **expected to fail in normal use**.
- **Affordance rule** — a rule the UI needs in advance to enable/disable actions (`CheckCanEdit`, `CheckCanArchive`). **Affordance rules are pure** — functions of already-loaded aggregate state only (hard rule, §4).
- **Set rule** — a rule about membership in a set ("name unique within profile", "max N schedules per profile"). Needs the database, therefore **write-time only** — a set rule is never an affordance (you cannot precompute `CheckCanRenameTo(x)` for every x; the client learns by trying).
- `RuleViolation(Code, Message)` — one broken rule.
- `RuleCheck` — the outcome of checking: `union RuleCheck(RuleCheck.Pass, RuleCheck.Fail)`, the case types nested in the union body (a single failed rule is a one-element `Fail`). Construction is `new RuleCheck.Pass()` / `new RuleCheck.Fail(code, message)` — C# forbids a static member sharing a nested case type's name, so this document's value shorthand (`RuleCheck.Pass`, `ApiResult.NotFound`) compiles with `new` (c001).
- `RuleSet` — the handler-side composer that evaluates checks and aggregates violations (§5).
- `ApiResult.RuleViolations` — the union case carrying violations to the edge → 409.

**Codes are contract:** `<aggregate>.<rule>` in kebab-case (`schedule.not-active`), stable forever, declared in the endpoint's OpenAPI 409 response so they reach the generated TS types ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md)). **Messages are static rule texts:** developer-English, never interpolating user or dose data (NFR-5-safe; OQ-2 i18n will key on the code, not the message). Telemetry logs codes only.

## 3. Two layers, two jobs

Rule checking deliberately happens **twice** on the write path:

- **The aggregate is the guarantee.** Every mutating method re-asserts its own pure rules and fails fast (first violation, as `DomainResult` — the domain layer's own union, c002; the edge union never enters Domain, arch-catalog rule 21). No caller — endpoint, Wolverine consumer, test, future code — can push an aggregate into a state its rules forbid. This is also the stale-UI answer: `canEdit` was true when the page rendered, the state changed since, the write still refuses.
- **The handler is the courtesy.** Before touching the domain, the feature handler composes *all* rules for the operation — the aggregate's pure checks plus async set checks — in one `RuleSet`, so the user gets **every** violation in a single 409 instead of fixing them one round-trip at a time.

The double evaluation of pure checks is intentional and costs nanoseconds. Never "optimize" it away — removing the aggregate-side check trades a correctness guarantee for nothing.

## 4. Affordances are pure — and static-first for projections

Affordance rules serve two callers: read endpoints (projecting `canEdit` flags into DTOs) and the aggregate's own write methods. Both demand purity — a list endpoint evaluating affordances per row cannot afford I/O, and an aggregate method must not query.

**Static-first form** — define the rule as a static function of the minimal state it reads; the instance method delegates. The static *is* the rule (single source of truth); projections calling it cannot drift from the domain:

```csharp
public sealed class Schedule : AggregateRoot<ScheduleId>
{
    // The rule itself — callable without materializing an aggregate
    public static RuleCheck CheckCanEdit(ScheduleStatus status) =>
        status == ScheduleStatus.Active
            ? RuleCheck.Pass
            : RuleCheck.Fail("schedule.not-active", "Only an active schedule can be edited.");

    // Instance convenience for write methods and single-item reads
    public RuleCheck CheckCanEdit() => CheckCanEdit(Status);
}
```

```csharp
// Read endpoint: list projections never materialize aggregates
.Select(s => new ScheduleListItem
{
    Id = s.Id,
    Name = s.Name,
    CanEdit = Schedule.CheckCanEdit(s.Status) is RuleCheck.Pass,  // client-evaluated in the final Select
})
```

EF Core caveat: this works **only in the final `Select`** (EF client-evaluates the top-level projection). Never use a rule in `Where`/`OrderBy` — it cannot translate to SQL; if filtering by an affordance is ever needed, express the underlying predicate in queryable terms.

Skip the static form only when a rule reads complex internal state that no projection will ever need — instance-only is then fine. If an affordance seems to need I/O, the design is wrong: either the aggregate is missing state it should carry, or the rule is really a set rule and belongs write-time-only.

## 5. `RuleSet` — composition semantics

```csharp
var rules = await RuleSet
    .Add(schedule.CheckCanEdit())                       // pure — already-evaluated value
    .Add(Schedule.CheckCanChangeTiming(req.Timing))     // pure — same stage: failures aggregate
    .Then(() => NameIsUniqueAsync(req, ct))        // async — next stage
    .CheckAsync();
if (rules is RuleCheck.Fail f) return f.ToApiResult();   // → ApiResult.RuleViolations → 409
```

- **Deferred:** nothing async executes until `CheckAsync()`. Pure checks arrive as already-evaluated `RuleCheck` values (they are cheap by §4); async checks arrive as `Func<Task<RuleCheck>>` lambdas the set controls.
- **Sequential, never parallel:** async checks are awaited in order. This is a hard constraint, not a style choice — `DbContext` is not thread-safe, and with no repository layer the context *is* the query API. `Task.WhenAll` over checks sharing a context throws `InvalidOperationException`. (At circle scale the forgone parallelism is unmeasurable.)
- **Stages:** `.Then(...)` starts a new stage. Violations *within* a stage aggregate; a later stage runs only if all previous stages passed. Use stages for genuine dependency (check C is meaningless when A failed) and for cost ordering (pure checks first, queries later).

## 6. Set rules get a constraint backstop

A query-based set check is **advisory** — a race can invalidate it between check and commit. The database constraint is the guarantee:

- every set rule is backed by a unique index / check constraint in the schema, and
- the `DbUpdateException` from that constraint maps to the **same violation code** as the advisory check, so the client sees one truth regardless of which layer caught it.

## 7. The 409 contract

One ProblemDetails object per response (RFC 9457 — multiple occurrences ride an extension member, the same way ASP.NET's `ValidationProblemDetails` carries `errors` for 400s):

```json
{
  "type": "https://doseup.app/problems/rule-violation",
  "title": "One or more domain rules were violated.",
  "status": 409,
  "violations": [
    { "code": "schedule.not-active", "message": "Only an active schedule can be edited." }
  ]
}
```

**Why 409:** RFC 9110 defines it as a conflict with "the current state of the target resource" — precisely what a domain-rule denial is. The client-facing distinction stays crisp: **400 = the request is malformed** (shape) · **409 = the request is fine, the state says no**. 422 was considered and rejected — a third category between those two that gives the client no additional action. `Conflict → 409` (class 7, concurrency) shares the status but not the `type` URI — the matrix maps union cases to statuses, not bijectively.

Endpoints declare the 409 response in their OpenAPI spec so `violations` and its codes reach the generated TS types.

## 8. The write path, end to end

The endpoint in front of this is a thin adapter — route + OpenAPI spec + auth policies + `ApiResult` → ProblemDetails via the Platform mapper (ADR-0002 § Slices). A Wolverine consumer invoking the same use case is the same shape: inbox/idempotency concerns + the handler call. The handler is transport-independent and framework-free. The `db` it commits is the **module's** `DbContext` — the transactional boundary is the bounded context, not the single aggregate, a conscious DDD divergence recorded in [ADR-0002 § Unit of work & side effects](../adr/0002-architecture-style.md#unit-of-work--side-effects-pre-4).

```csharp
// Feature handler — plain class, no FastEndpoints/HTTP references
public async Task<ApiResult> Handle(EditScheduleRequest req, CancellationToken ct)
{
    // 1. Contract validation — shape only (class 4)
    if (await validator.ValidateAsync(req, ct) is { IsValid: false } v)
        return v.ToApiResult();                                            // → 400

    // 2. Load, account-scoped — nonexistent and foreign are the same 404 (class 5 — ADR-0002 § Authorization)
    var schedule = await db.Schedules
        .SingleOrDefaultAsync(s => s.ProfileId == caller.ProfileId && s.Id == req.Id, ct);
    if (schedule is null) 
        return ApiResult.NotFound;

    // 3. All rules, aggregated for the user (class 6, §5)
    var rules = await RuleSet
        .Add(schedule.CheckCanEdit())
        .Then(() => NameIsUniqueAsync(req, ct))
        .CheckAsync();
    if (rules is RuleCheck.Fail f) 
        return f.ToApiResult();                 // → 409, all violations

    // 4. Domain — the aggregate re-asserts its own rules (§3, in domain vocabulary) and raises events
    DomainResult result = schedule.Edit(Map(req));
    if (result is not DomainResult.Success) 
        return result.ToApiResult();

    // 5. Commit — domain events drain in the SaveChanges interceptor;
    //    outbox envelopes join the same transaction (ADR-0002)
    await db.SaveChangesAsync(ct);
    return ApiResult.Success;
}
```

## 9. Where things live

| Thing | Home |
|---|---|
| `RuleCheck`, `RuleViolation`, `RuleSet`, `DomainResult` (+ `DomainResult<T>`), `ApiResult.RuleViolations` | SharedKernel |
| Rules — static-first affordances + write-method self-checks | the aggregate |
| Async set checks | private methods of the feature handler (module-local) |
| `ApiResult` → ProblemDetails mapping | Platform — one mapper, no per-endpoint error crafting |
| Violation codes | the API contract (OpenAPI 409 declarations → TS types) |
