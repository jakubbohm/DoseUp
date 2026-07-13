# ADR-0001: Platform and stack

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-12/13) · **Amended:** 2026-07-13 — database: Cosmos DB → Neon Postgres (PRE-2)

## Context

Greenfield hobby project, solo developer working with Claude. Azure is both the deployment target and part of the learning/showcase goal ([vision](../product/vision.md) G3/G4). Scale is an invite-only circle (≤ 50 accounts), which frees the stack to prioritize learning value and showcase quality over conservatism.

## Decisions

| Area | Choice | Rationale | Alternatives considered |
|------|--------|-----------|------------------------|
| Runtime | **.NET 11 previews** for API/domain/test projects (`net11.0` + `LangVersion preview`); **AppHost stays `net10.0`** (Aspire 13.x requires the .NET 10 SDK for C# AppHosts) | C# 15 `union` types are the motivating feature; preview churn is accepted as a learning goal | All-net10 until GA (safer, no unions); rejected — the result pattern is wanted from day one |
| Result pattern | **Hand-rolled `union`-based `Result`** in SharedKernel, used **end-to-end**: expected failures (validation, not-found, conflict) flow as Result from domain to the HTTP edge, where cases map to **ProblemDetails**. Exceptions only for bugs/infrastructure | Zero dependencies, pure C# 15 showcase, fully ours to shape | ErrorOr / FluentResults (battle-tested ergonomics, but external and pre-union); exceptions-as-control-flow |
| Web framework | **FastEndpoints** on ASP.NET Core | REPR model fits vertical slices; built-in FluentValidation path | Minimal APIs (less structure), MVC (more ceremony) |
| Frontend | **React + Vite + TypeScript PWA** | Jakub's preference; largest PWA/push ecosystem; demonstrates Aspire polyglot orchestration | Blazor (single-language appeal — declined), Next.js (SSR unneeded) |
| Database | **Neon serverless Postgres** *(PRE-2; replaces the founding Cosmos DB choice)* | Serverless economics stay (scale-to-zero; free plan covers circle scale as of 2026-07), relational modeling with real migrations, database branching for dev/CI workflows (PRE-9), standard Postgres = portable + huge ecosystem; learning goal | Azure Cosmos DB serverless (founding choice — Azure-native, change-feed outbox; reversed by PRE-2: document/no-migrations discipline and heavier lock-in outweighed it, reversal recorded in software-factory F-23), Azure Database for PostgreSQL Flexible Server (Azure-native but no scale-to-zero/branching; idle cost), SQLite, SQL Server |
| Auth | **Microsoft Entra External ID** | Azure-native CIAM, free tier covers the circle many times over, invite-only fits | ASP.NET Identity (own the password risk; identity store is custom work), Auth0/Clerk (non-Azure dependency) |
| Hosting | **Azure Container Apps**, single production environment, deployed from main | aspire/azd-native path; scale-to-zero economics | Home server/VPS (cheaper, less showcase), App Service |
| Feature flags | **Microsoft.FeatureManagement + Azure App Configuration** | Runtime toggles without redeploy — required by trunk→prod (ADR-0004); Azure-native | Homegrown config flags; dark-launching only |
| Push | **Web Push (VAPID) directly from the API** *(default, confirm in M2 design)* | Standard, no extra service | Azure Notification Hubs (overkill for PWA-only) |
| Upgrade policy | **Ride previews eagerly** — each monthly .NET 11 preview / Aspire release becomes a dedicated PR gated by full CI | Maximum learning; CI is the safety net | Pin-and-upgrade-per-milestone; bot-only cadence |

## Consequences

- **Preview risk is structural:** tooling (IDE, analyzers, CSharpier, TUnit, FastEndpoints) may lag or break on a given preview. Mitigations: upgrades are isolated PRs (trivially revertable); M0 includes a FastEndpoints-on-net11 spike; fallback is pinning the last working preview.
- **Union caveats (as of 2026-07):** values stored as `object?` (value types box — fine at our scale, watch hot paths); "member providers" not yet implemented; early previews required a `UnionAttribute`/`IUnion` polyfill — check whether the current preview still does.
- Postgres: real relational **migrations, forward-safe from day one** (expand/contract, ADR-0004); the schema + migration baseline lands in M1 design and data-access/UoW tooling is settled in PRE-4; local dev/CI runs a plain Postgres container via Aspire's Postgres integration (ADR-0003); the Neon branching strategy is explored in PRE-9.
- **Neon is a non-Azure dependency (facts as of 2026-07):** the Azure Native Neon integration is retired and Neon's Azure regions are deprecated — new projects are AWS-only (nearest: Frankfurt/London). DoseUp therefore runs cross-cloud: ACA (Azure) ↔ Neon (AWS EU). Accepted deliberately; the NFR-2 latency budget is validated in M0, and the connection string is a real secret with no managed-identity path — held in ACA secrets/Key Vault (NFR-4).
- Scale-to-zero vs NFR-3 (reminders must fire regardless): reminder computation needs an always-runnable trigger (e.g. scheduled Container Apps job) — designed in M2.
- Vendor lock-in to Azure is accepted deliberately for hosting/auth (it *is* the showcase); the database is the deliberate exception — standard Postgres on Neon stays portable (any Postgres host can take a dump/restore).
- Polyglot toolchain (dotnet + npm) in every pipeline.
