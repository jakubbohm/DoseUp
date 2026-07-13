# ADR-0001: Platform and stack

**Status:** Accepted · **Date:** 2026-07-13 · **Decided by:** Jakub (interview 2026-07-12/13)

## Context

Greenfield hobby project, solo developer working with Claude. Azure is both the deployment target and part of the learning/showcase goal ([vision](../product/vision.md) G3/G4). Scale is an invite-only circle (≤ 50 accounts), which frees the stack to prioritize learning value and showcase quality over conservatism.

## Decisions

| Area | Choice | Rationale | Alternatives considered |
|------|--------|-----------|------------------------|
| Runtime | **.NET 11 previews** for API/domain/test projects (`net11.0` + `LangVersion preview`); **AppHost stays `net10.0`** (Aspire 13.x requires the .NET 10 SDK for C# AppHosts) | C# 15 `union` types are the motivating feature; preview churn is accepted as a learning goal | All-net10 until GA (safer, no unions); rejected — the result pattern is wanted from day one |
| Result pattern | **Hand-rolled `union`-based `Result`** in SharedKernel, used **end-to-end**: expected failures (validation, not-found, conflict) flow as Result from domain to the HTTP edge, where cases map to **ProblemDetails**. Exceptions only for bugs/infrastructure | Zero dependencies, pure C# 15 showcase, fully ours to shape | ErrorOr / FluentResults (battle-tested ergonomics, but external and pre-union); exceptions-as-control-flow |
| Web framework | **FastEndpoints** on ASP.NET Core | REPR model fits vertical slices; built-in FluentValidation path | Minimal APIs (less structure), MVC (more ceremony) |
| Frontend | **React + Vite + TypeScript PWA** | Jakub's preference; largest PWA/push ecosystem; demonstrates Aspire polyglot orchestration | Blazor (single-language appeal — declined), Next.js (SSR unneeded) |
| Database | **Azure Cosmos DB (serverless)** | Azure-native, near-zero cost at circle scale, document model fits per-profile freeform data, change feed enables the outbox (ADR-0002); learning goal | PostgreSQL (portable, relational — not chosen: Azure-native + serverless preferred), SQLite, SQL Server |
| Auth | **Microsoft Entra External ID** | Azure-native CIAM, free tier covers the circle many times over, invite-only fits | ASP.NET Identity (own the password risk; Cosmos stores are custom work), Auth0/Clerk (non-Azure dependency) |
| Hosting | **Azure Container Apps**, single production environment, deployed from main | aspire/azd-native path; scale-to-zero economics | Home server/VPS (cheaper, less showcase), App Service |
| Feature flags | **Microsoft.FeatureManagement + Azure App Configuration** | Runtime toggles without redeploy — required by trunk→prod (ADR-0004); Azure-native | Homegrown config flags; dark-launching only |
| Push | **Web Push (VAPID) directly from the API** *(default, confirm in M2 design)* | Standard, no extra service | Azure Notification Hubs (overkill for PWA-only) |
| Upgrade policy | **Ride previews eagerly** — each monthly .NET 11 preview / Aspire release becomes a dedicated PR gated by full CI | Maximum learning; CI is the safety net | Pin-and-upgrade-per-milestone; bot-only cadence |

## Consequences

- **Preview risk is structural:** tooling (IDE, analyzers, CSharpier, TUnit, FastEndpoints) may lag or break on a given preview. Mitigations: upgrades are isolated PRs (trivially revertable); M0 includes a FastEndpoints-on-net11 spike; fallback is pinning the last working preview.
- **Union caveats (as of 2026-07):** values stored as `object?` (value types box — fine at our scale, watch hot paths); "member providers" not yet implemented; early previews required a `UnionAttribute`/`IUnion` polyfill — check whether the current preview still does.
- Cosmos: no relational migrations — document versioning discipline instead; **partition-key design happens early** (M1 design); local dev/CI via the vNext emulator (ADR-0003).
- Scale-to-zero vs NFR-3 (reminders must fire regardless): reminder computation needs an always-runnable trigger (e.g. scheduled Container Apps job) — designed in M2.
- Vendor lock-in to Azure is accepted deliberately (it *is* the showcase).
- Polyglot toolchain (dotnet + npm) in every pipeline.
