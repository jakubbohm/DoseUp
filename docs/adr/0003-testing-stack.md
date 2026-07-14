# ADR-0003: Testing stack

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-13, based on ecosystem research of the same date) · **Amended:** 2026-07-13 — integration-test database: Cosmos emulator → Postgres container (PRE-2) · 2026-07-15 — test *organisation* settled in [conventions/testing.md](../conventions/testing.md); assertions re-examined against TUnit-native `TUnit.Assertions` and confirmed Shouldly (PRE-8)

## Context

Quality bar is showcase-grade (vision G3): full test pyramid, CI-gated. Ecosystem facts as of 2026-07: FluentAssertions v8+ is commercial; NetArchTest is unmaintained (since 2021); Stryker.NET's xUnit-v3 support is broken (open bug) and its Microsoft.Testing.Platform runner is preview-grade. (The Cosmos vNext-emulator caveat that originally lived here disappeared with PRE-2's switch to Postgres — the integration database is now a plain, stable container.)

## Decisions

| Layer | Choice | Rationale | Alternatives considered |
|-------|--------|-----------|------------------------|
| Test framework | **TUnit** (source-generated, Microsoft.Testing.Platform-native) | Modern, AOT-capable, matches the bleeding-edge identity of the project; official ArchUnitNET adapter exists | xUnit v3 (the mature default — rejected in favor of learning value; also currently broken with Stryker), NUnit (adapter-based MTP) |
| Assertions | **Shouldly** | BSD-3, no licensing asterisks, pleasant API; sync where our domain tests are sync; framework-agnostic hedge on the TUnit bet | AwesomeAssertions (FA-v7 fork; richer equivalency but fork-dependency), FluentAssertions v8 (commercial), built-ins, **TUnit.Assertions** (weighed PRE-8: async-first `await Assert.That(…)` puts await-ceremony + async signatures on every sync domain test, pre-1.0 churn, welds assertions to the framework — rejected; revisit trigger in [conventions/testing.md §6.6](../conventions/testing.md)) |
| Unit tests | Domain logic (recurrence/DST engine, Result invariants) tested exhaustively — the pyramid's base | — | — |
| Integration tests | **Aspire harness only**: `DistributedApplicationTestingBuilder` runs the real AppHost graph with a **Postgres container** via Aspire's Postgres integration (stable — no emulator caveats) | One harness, tests the real orchestration | Testcontainers for narrow per-dependency loops (explicitly not adopted — accepted cost below), WebApplicationFactory-only |
| Architecture tests | **ArchUnitNET** (TUnit adapter) enforcing the ADR-0002 dependency rules, plus Roslyn analyzers/BannedApiAnalyzers for must-never-happen rules | Only maintained option; compile-time + test-time coverage | NetArchTest (dead) |
| E2E | **`@playwright/test` (TypeScript)** beside the frontend; smoke subset on PR, full suite nightly | Flagship runner: HTML report, traces, UI mode, sharding | Microsoft.Playwright .NET binding (no HTML reporter/UI mode, constrained parallelism) |
| Mutation testing | **Pending a spike**: Stryker.NET × TUnit compatibility is unverified. If viable → nightly diff/dashboard job, never a PR gate | Honest — the chosen framework is young | Skipping entirely |
| Coverage | Reported in the nightly quality job; **no PR threshold gate** | Signal without fighting the gate | Changed-lines threshold on PR |

## Consequences

- **Persistence-level tests pay full-graph startup cost** (Aspire-only harness). Accepted; revisit (Testcontainers or WebApplicationFactory tier) if the loop gets painful — record the reversal here.
- CI runs integration tests on **ubuntu runners** with **health-probe waits + explicit timeouts** (known Aspire-in-CI hang risk).
- TUnit's weekly release cadence folds into the eager-upgrade policy (ADR-0001).
- ~~Test-naming/AAA conventions land in `docs/conventions/` during M0, once real tests exist.~~ Superseded: PRE-8 pulled the organisation and style conventions forward into [conventions/testing.md](../conventions/testing.md) (2026-07-15); M0 only calibrates them against the first real tests.
