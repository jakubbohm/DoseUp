# M0 spikes — proof-of-concept results

Throwaway PoCs answering the M0 `spike`-labelled GitHub issues with **evidence**, built so the
only thing left is the handful of steps that genuinely need a human with a tenant/subscription.
Everything here is isolated: its own `Directory.Build.props` /
`Directory.Packages.props` mean nothing under `spikes/` is part of `DoseUp.slnx`, the production
package set, or any gate. Delete the folder and the repo is unchanged.

## Verdicts at a glance

| Issue | Spike | Verdict | Proven offline tonight | Needs you tomorrow |
|---|---|---|---|---|
| [#50](https://github.com/jakubbohm/DoseUp/issues/50) | Wolverine on .NET 11 preview + ASB **Basic**, per-module outbox in own schema | **GO** | Full round-trip green on the exact preview pins; per-module outbox **and** inbox in each module's schema; envelope joins the module transaction; runtime EF Core 11 preview.5 compat | ✅ **done 2026-07-18** — confirmed on a real ASB **Basic** namespace (clean send+receive, no topic/session/system-queue errors); see [`evidence/asb-basic-real-namespace.txt`](messaging-poc/evidence/asb-basic-real-namespace.txt) |
| [#51](https://github.com/jakubbohm/DoseUp/issues/51) | Wolverine × ASB **emulator** in the Aspire harness | **GO** | 4/4 tests green against `servicebus-emulator:2.0.0`, no cloud | nothing |
| [#62](https://github.com/jakubbohm/DoseUp/issues/62) | Entra app registration from the pipeline | **CONDITIONAL GO** | `az bicep build` compiles the tenant-scope Graph template; app + SP + FIC bind offline | ➡️ **implementation tracked in [#75](https://github.com/jakubbohm/DoseUp/issues/75)** (not finished live here); two open risks since resolved in our favour — see the entra-pipeline README |
| [#93](https://github.com/jakubbohm/DoseUp/issues/93) | `EFCore.NamingConventions` (built for EF 10) on our EF **11 previews** | **GO** | 11/11 tests green on the exact preview pins; schema applied to a real `postgres:17` with every identifier unquoted — tables, columns, owned types, PK/AK/FK, indexes, and query translation | nothing now — but the GO **expires**: the package's dependency ceiling is `< 11.0.0`, so bumping EF to 11.0.0 **stable** breaks restore until it ships an 11.x |

Detail lives with each PoC:
- **[`messaging-poc/README.md`](messaging-poc/README.md)** — spikes #50 + #51, with the topology and round-trip evidence, and **four design findings** worth folding into M0.
- **[`entra-pipeline/README.md`](entra-pipeline/README.md)** — spike #62, with the proven-vs-needs-tenant split and the CIAM runbook.
- **[`snake-case-naming/README.md`](snake-case-naming/README.md)** — spike #93, with **three authoring rules** the first real migration ([#42](https://github.com/jakubbohm/DoseUp/issues/42)) must follow and one dated follow-up.

> These are spike outputs (throwaway PoCs), **not** production code and **not** OpenSpec changes.
> Adopting any of it into `src/` is a separate, Jakub-gated decision. I have **not** touched the
> GitHub issues — recording go/no-go outcomes on #50/#51/#62 is yours to do.

---

## What each spike taught us (the decisions)

**#50 — Wolverine stays; the async design holds.** Wolverine 6.20 runs on the net11 preview + EF
Core 11 preview.5 runtime, and keeps a **separate transactional outbox _and_ inbox per module, each
in the module's own Postgres schema** (`MessageStoreRole.Ancillary` + `Enroll<TDbContext>()`), with a
single Main control store in a `wolverine` schema. Envelope writes join the module's `DbContext`
transaction (a failed unit of work relays nothing). Two wiring facts the real change must bake in:
`AutoApplyTransactions()` is mandatory (not just `UseEntityFrameworkCoreTransactions()`), and Wolverine
6.20 needs **static codegen** or the `WolverineFx.RuntimeCompilation` package. Fallbacks (EF 10 GA pin;
Standard tier) remain valid and unused.

**#51 — messaging is locally testable.** The Aspire harness runs `RunAsEmulator()` and Wolverine does
a real AMQP round-trip against it with `AutoProvision` off (queue pre-seeded) — so async slices are
testable with no cloud connection, exactly as [testing.md §3d](../docs/conventions/testing.md) hoped.
The recorded stub-transport fallback is **not** needed.

**#62 — sign-in setup can be code.** The GA Microsoft Graph Bicep extension declares the app
registration + service principal + federated credential and compiles offline. It's *conditional* only
because DoseUp's identity tenant is Entra External ID (CIAM): no subscription → tenant-scope,
Graph-only deployment, plus a one-time human bootstrap (consent + ARM elevation). An imperative
`az ad` fallback avoids the ARM elevation entirely.

**#93 — snake_case rides the previews; the mechanism is what has an expiry date.**
`EFCore.NamingConventions` 10.0.1 renames everything it should on EF 11 preview.5, and a real
`postgres:17` accepts the result with no identifier needing quotes — which was the whole point of
[F-88](../docs/software-factory.md). The value is in the three boundaries it does **not** cross,
each of which fails silently in normal EF usage: an explicit schema name, an explicit check
constraint (name *and* raw SQL — the naive spelling emits DDL Postgres rejects outright), and EF's
own migrations-history table. All three are one-line authoring choices in [#42](https://github.com/jakubbohm/DoseUp/issues/42),
and all three are cheap only while zero tables exist. Separately, the package's dependency ceiling
is `< 11.0.0`: today's previews slip under it because SemVer sorts prereleases low, but EF 11.0.0
**stable** will not — a loud, scheduled break on a bump we control, with the hand-rolled
`IModelFinalizingConvention` fallback still standing behind it.

---

## Tomorrow's manual steps (ordered, ~30 min total)

### A. Messaging — smoke against a real ASB **Basic** namespace (#50) — ✅ DONE 2026-07-18
> **Result: GO confirmed.** Jakub created the `doseup-spike-sb-basic` Basic namespace (rg
> `doseup-spikes`); the unchanged PoC (AutoProvision off, system queues off, queue-only) did a clean
> send→receive round-trip against it in ~2s, with no topic/subscription/session/system-queue errors.
> Proof: [`messaging-poc/evidence/asb-basic-real-namespace.txt`](messaging-poc/evidence/asb-basic-real-namespace.txt).
> Only remaining housekeeping: **delete the `doseup-spike-sb-basic` namespace** (Jakub's subscription).
> The original steps are kept below for the record / to reproduce.

The only thing the emulator can't prove is Basic-tier queue-only behavior. Bicep is ready:

1. **Create the namespace** (hand-authored Bicep, honoring ADR-0004):
   ```bash
   az deployment group create \
     --resource-group <rg> \
     --template-file spikes/messaging-poc/infra/servicebus-basic.bicep \
     --parameters spikes/messaging-poc/infra/servicebus-basic.bicepparam
   ```
   It provisions a **Basic** namespace + the `dose-events` queue (queues only — the template has no
   topics, so a Basic-tier violation fails at deploy, which is itself part of the proof).
2. **Get a connection string** — the template outputs the exact
   `az servicebus namespace authorization-rule keys list …` command (quick smoke), or use the
   `namespaceHostName` output with managed identity (*Data Sender*/*Receiver* roles) for the prod path.
3. **Run the PoC against it** instead of the emulator — point the API at the real namespace:
   ```bash
   cd spikes/messaging-poc
   ConnectionStrings__messaging="<the-basic-namespace-connection-string>" \
   ConnectionStrings__messagingdb="<any-postgres>" \
     dotnet run --project src/MessagingPoc.Api
   # then: POST /scheduling/record-dose {"profileId":"…"}  →  GET /adherence/entries?profileId=…
   ```
   **Pass = the adherence entry appears** (round-trip works on Basic) **and no `MessagingEntityNotFound`
   / topic / session errors** in the log. Then record GO on issue #50 and delete the namespace.

### B. Entra — implementation moved to issue [#75](https://github.com/jakubbohm/DoseUp/issues/75)
> We decided **not** to finish spike #62 with a live deployment. The real work — promoting the PoC to
> `infra/identity/`, the `identity.yml` workflow, and the one-time bootstrap steps — is tracked in
> **#75** with per-task sub-issues [#76–#79](https://github.com/jakubbohm/DoseUp/issues/75). The
> original runbook is kept below for reference.

Follow **[`entra-pipeline/README.md`](entra-pipeline/README.md) → "Runbook for tomorrow"**. In short:
create a deploy principal in the External ID tenant → grant it `Application.ReadWrite.OwnedBy` +
admin consent → (Plan A only) elevate + assign an ARM role at `/` → run `entra-pipeline/deploy.ps1`.
The single open question to answer there is **whether GitHub OIDC federates into a CIAM tenant**; if
not, use a client secret (fallback) or the imperative `entra-pipeline/fallback-az-cli.ps1 -Go`.

### C. Record outcomes
Once A and B confirm, the three issues can be closed with the go/no-go note (your call — I left the
issues untouched).
