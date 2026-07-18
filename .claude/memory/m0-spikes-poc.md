---
name: m0-spikes-poc
description: M0 spike PoCs (#50/#51/#62) built overnight 2026-07-18 — verdicts + what's left for Jakub
metadata:
  type: project
---

Overnight 2026-07-18 I built throwaway PoCs under `spikes/` (fully isolated: own Directory.Build/Packages.props, not in DoseUp.slnx, main build unaffected) answering the three M0 `spike` issues. Verdicts:

- **#50 (Wolverine on net11 preview + ASB Basic, per-module outbox in own schema): GO — now confirmed against a REAL ASB Basic namespace (2026-07-18), not just the emulator.** Proven end-to-end locally — 4/4 integration tests green against the ASB emulator — AND smoked against a real Basic namespace (`doseup-spike-sb-basic`, rg `doseup-spikes`, Jakub's personal sub): unchanged PoC (AutoProvision off, SystemQueuesAreEnabled(false), queue-only) did a clean send→receive round-trip in ~2s, zero topic/session/system-queue/MessagingEntityNotFound errors; Wolverine keeps its reply endpoint in-memory (`stub://replies/`) so it needs no ASB system queue (why Basic suffices). Secret-free proof at `spikes/messaging-poc/evidence/asb-basic-real-namespace.txt`. Each module keeps its **own outbox AND inbox in its own Postgres schema** (`MessageStoreRole.Ancillary` + `Enroll<TDbContext>()`), single Main store in a `wolverine` schema; envelope joins the module transaction; EF Core 11 preview.5 runtime compat confirmed.
- **#51 (Wolverine × ASB emulator in Aspire harness): GO.** `RunAsEmulator()` + AutoProvision-off round-trip works; stub-transport fallback not needed.
- **#62 (Entra app registration from pipeline): CONDITIONAL GO.** GA Microsoft Graph Bicep extension; `az bicep build` compiles the tenant-scope Graph template offline. Conditional only on CIAM specifics (tenant-scope deploy, one-time admin consent + ARM elevation, unconfirmed GitHub-OIDC-into-CIAM).

Two Wolverine wiring findings the real M0 change must bake in: (1) 6.20 removed the runtime Roslyn compiler from core — need `WolverineFx.RuntimeCompilation` OR static codegen (ADR-0002 already wants static codegen); (2) `UseEntityFrameworkCoreTransactions()` is NOT enough — must also call `Policies.AutoApplyTransactions()` or handlers never save and events bypass the outbox.

Left for Jakub: **#50 ASB Basic smoke is DONE** (2026-07-18) — only housekeeping is deleting the `doseup-spike-sb-basic` namespace on his sub. Still pending: deploy #62 to the real External ID tenant (manual step B). **GitHub issues #50/#51/#62 left untouched** — recording go/no-go + closing them is his call (see [[github-project-management]]). These are spike PoCs, not production code and not OpenSpec changes; adopting into src/ is a separate Jakub-gated decision (see [[user-gates-stage-progression]]).
