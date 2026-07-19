# Spike — snake_case naming on the EF 11 previews ([#93](https://github.com/jakubbohm/DoseUp/issues/93))

**Verdict: GO — take `EFCore.NamingConventions` as-is, with three authoring rules and one dated follow-up.**

The snake_case decision ([conventions/README.md § Persistence — Postgres](../../docs/conventions/README.md), [software-factory.md F-88](../../docs/software-factory.md)) stands either way; this spike only asked whether the *mechanism* holds. It does: `EFCore.NamingConventions` 10.0.1 — built and tested against EF Core 10 — loads, runs, and renames correctly on `Microsoft.EntityFrameworkCore` **11.0.0-preview.5** with `Npgsql.EntityFrameworkCore.PostgreSQL` **11.0.0-preview.5**, the exact versions the repo root pins today.

| Question | Answer | Evidence |
| --- | --- | --- |
| Does the package even load on EF 11 previews? | Yes — no binding error, no convention-pipeline break | [`05-dependency-resolution.txt`](evidence/05-dependency-resolution.txt) |
| Do tables, columns, keys, indexes come out snake_case? | Yes — all of them, plus owned types and acronyms | [`03-postgres-introspection.txt`](evidence/03-postgres-introspection.txt) |
| Does a real Postgres accept the generated DDL? | Yes, on `postgres:17` | [`02-corrected-authoring-applies.txt`](evidence/02-corrected-authoring-applies.txt) |
| Do LINQ queries also address snake_case names? | Yes — runtime model agrees with the design-time model | [`04-test-run.txt`](evidence/04-test-run.txt) |
| Is anything left needing quotes in hand-written SQL? | No, once the three rules below are followed | [`03-postgres-introspection.txt`](evidence/03-postgres-introspection.txt) |
| Does this stay true forever? | **No — it expires when EF 11.0.0 *stable* ships** | [`06-version-range-expiry.txt`](evidence/06-version-range-expiry.txt) |

## What runs, and what it proves

`tests/SnakeCaseNaming.Tests` — 11 TUnit tests, all green, no database required.

| Test | Proves |
| --- | --- |
| `ModelBuildsAtAllOnEfCore11` | The headline risk: the package plugs into EF's convention infrastructure and that infrastructure did not break across the major |
| `EveryTableNameIsSnakeCase` / `…ColumnName…` / `…KeyName…` / `…ForeignKeyConstraintName…` / `…IndexName…` | Each identifier class the issue named, matched against `^[a-z][a-z0-9]*(_[a-z0-9]+)*$` |
| `OwnedTypeColumnsAreFlattenedAndRenamed` | Owned value objects — `PrimaryContact.EmailAddress` → `primary_contact_email_address` |
| `ConsecutiveCapitalsInAcronymsAreHandled` | The classic edge case: `IANATimeZoneId` → `iana_time_zone_id`, not `i_a_n_a_…` |
| `QueryTranslationAlsoEmitsSnakeCase` | Migrations read the **design-time** model, LINQ reads the **runtime** model — different pipelines. A convention landing in only one would ship a schema no query could address |
| `ExplicitCheckConstraintNamesAndSqlPassThroughVerbatim` / `ExplicitSchemaNamesPassThroughVerbatim` | Pins the two known limitations, so a future package version that changes them fails loudly rather than silently |

The model in `src/SnakeCaseNaming.Module` is deliberately module-shaped — own schema, three aggregates, an owned type, an alternate key, a filtered composite unique index, a cascade FK, and a check constraint — so every identifier class appears at least once. Every property name is multi-word PascalCase on purpose: a missed rename shows up rather than hiding behind an already-lowercase name.

## Findings — the three rules the first real migration must follow

The package renames the names it **generates**. It never touches a name you set **explicitly**, and it cannot rewrite **raw SQL**. Three places in normal EF usage hit that boundary, and all three are silent — the C#-idiomatic spelling compiles, generates, and only fails later.

1. **Author module schema names lowercase.** `HasDefaultSchema("Membership")` survives verbatim and yields a quoted `"Membership".…` in every statement — the exact tax the decision exists to avoid. Write `"membership"`.
2. **Author check constraints snake_case on both halves — name *and* expression.** This is the sharp one. The idiomatic `HasCheckConstraint("CK_MedicationProfile_DailyDoseLimit_Positive", "\"DailyDoseLimit\" > 0")` produces DDL that Postgres **rejects outright** (`column "DailyDoseLimit" does not exist`), because the column is now `daily_dose_limit` — see [`01-naive-authoring-fails.txt`](evidence/01-naive-authoring-fails.txt). This matters beyond aesthetics: [domain-rules.md](../../docs/conventions/domain-rules.md) maps set-rule violations *by constraint name*.
3. **Rename EF's own migrations-history table.** EF names it explicitly, so the package renames its *columns* but not the *table* — leaving a quoted `"__EFMigrationsHistory"` with snake_case columns inside. `.MigrationsHistoryTable("__ef_migrations_history", Schema)` on the Npgsql options fixes it, and also parks it in the module's schema rather than `public` — which the module-per-schema layout wants anyway.

All three are one-line authoring choices, not defects, and all three are cheapest to fix now: casing is reversible only while zero tables exist ([F-88](../../docs/software-factory.md)).

## The dated follow-up — this GO expires

The issue assumed a dependency **floor** (`>= 10.0.0`). It is actually a **bounded range**: `[10.0.1, 11.0.0)`. Our restore is clean today only because SemVer sorts prereleases below their release — `11.0.0-preview.5 < 11.0.0` — so the previews slip under the ceiling. **EF 11.0.0 stable will not.** It raises `NU1608`, and because the repo root sets `TreatWarningsAsErrors=true`, that arrives as a **hard restore failure in `src/`**, not a warning. Proven with the same range shape one major earlier in [`06-version-range-expiry.txt`](evidence/06-version-range-expiry.txt).

That is a good failure mode: loud, on a bump we control and schedule, never in production. But it needs an owner — the EF-stable bump is already a standing upgrade activity (the EF block is held at preview.5 waiting on Npgsql), and this rides along with it.

## Recommendation

Take the dependency. When the EF family bumps to 11.0.0 stable, check whether `EFCore.NamingConventions` has shipped an 11.x: if yes, bump together; if it is merely late and the release notes show no relevant breaking change, `NoWarn NU1608` as a documented stopgap; if it is abandoned, spend the ~20 lines on the hand-rolled `IModelFinalizingConvention` fallback. The naming decision is unaffected in all three branches — only the mechanism moves, which is exactly the shape F-88 predicted.

## Run it yourself

```pwsh
cd spikes/snake-case-naming

# The naming assertions — no database needed
dotnet build tests/SnakeCaseNaming.Tests/SnakeCaseNaming.Tests.csproj
./tests/SnakeCaseNaming.Tests/bin/Debug/net11.0/SnakeCaseNaming.Tests.exe

# Regenerate the migration and the DDL from scratch
dotnet tool restore
cd src/SnakeCaseNaming.Module
dotnet ef migrations script --context MembershipDbContext --idempotent --output ../../evidence/schema.sql
```

To re-prove it against a real Postgres:

```pwsh
docker run -d --name doseup-snake-spike -e POSTGRES_PASSWORD=spike -e POSTGRES_USER=spike -e POSTGRES_DB=doseup_spike -p 55437:5432 postgres:17
docker exec -i doseup-snake-spike psql -U spike -d doseup_spike -v ON_ERROR_STOP=1 < evidence/schema.sql
docker exec doseup-snake-spike psql -U spike -d doseup_spike -c "\d membership.medication_profiles"
docker rm -f doseup-snake-spike
```

Run the test project's executable directly rather than `dotnet test`: invoked from inside this spike folder, `dotnet test` hits a Microsoft.Testing.Platform handshake failure and reports "Zero tests ran" while the same assembly runs all 11 green. A spike-local quirk, unrelated to the naming question — production test projects are unaffected.

## Recorded fallbacks

- Package abandoned or breaking on EF 11 stable → hand-rolled `IModelFinalizingConvention` (~20 lines), per [F-88](../../docs/software-factory.md).
- Package merely late for EF 11 stable → `NoWarn NU1608` as a documented, time-boxed stopgap, only after reading the release notes.

> These are spike outputs (throwaway PoCs), **not** production code and **not** OpenSpec changes. Everything here is isolated: its own `Directory.Build.props` / `Directory.Packages.props` mean nothing under `spikes/` is part of `DoseUp.slnx`, the production package set, or any gate. Delete the folder and the repo is unchanged.
