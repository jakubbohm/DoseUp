# Messaging PoC — spikes #50 & #51 (go/no-go)

Throwaway proof-of-concept for the two M0 messaging spikes. Isolated under `spikes/`; not
referenced by `DoseUp.slnx`, the root `Directory.Packages.props`, or any production build.

| Spike | Question | Verdict |
|---|---|---|
| [#50](https://github.com/jakubbohm/DoseUp/issues/50) | Does Wolverine run on the .NET 11 preview stack against ASB **Basic**, keeping its outbox/inbox state **per module, in each module's own schema**, with envelope writes joining the module's transaction? | **GO** — proven locally end-to-end **and confirmed against a real ASB _Basic_ namespace** on 2026-07-18 (see below). |
| [#51](https://github.com/jakubbohm/DoseUp/issues/51) | Can messaging tests run Wolverine against the **local ASB emulator** inside the Aspire harness? | **GO** — the round-trip test below runs green against the emulator, no cloud connection. |

**Bottom line:** Wolverine stays. Both preconditions hold on the exact preview pins, with recorded
fallbacks intact. Four findings below are worth folding into the M0 messaging design.

---

## What runs, and what it proves

One small Aspire app: Postgres + the ASB emulator + an API hosting **two modules** —
`Scheduling` (publishes `DoseRecorded`) and `Adherence` (consumes it). Four TUnit slice tests
(`tests/MessagingPoc.IntegrationTests/SpikeTests.cs`) drive it over HTTP through the same
`DistributedApplicationTestingBuilder` harness the production suite uses.

```
Test run summary: Passed!   total: 4   failed: 0   succeeded: 4      (1m 27s, cold)
```
(full log: [`evidence/dotnet-test-green.log`](evidence/dotnet-test-green.log))

| Test | Proves |
|---|---|
| `Recording_a_dose_delivers_the_event_across_modules_through_the_service_bus_emulator` | #51 + #50 end-to-end: `Scheduling` publishes via its outbox → real ASB emulator → `Adherence` consumes and persists. No cloud. |
| `Each_module_keeps_its_wolverine_store_in_its_own_schema` | #50's hard precondition: per-module outbox **and** inbox live in the module's own Postgres schema. |
| `Delivering_the_same_dose_twice_produces_a_single_adherence_entry` | #50: consumer idempotency (inbox in the `adherence` schema + business-key re-query). |
| `A_failed_unit_of_work_rolls_back_the_aggregate_and_the_outbox_together` | #50: the outbox envelope shares the aggregate's transaction — a failed unit of work relays nothing. |

### The per-module topology (the #50 money shot)

Live `CREATE TABLE` topology from an instrumented run
([`evidence/per-module-topology.txt`](evidence/per-module-topology.txt)) — **each module owns its
own outbox _and_ inbox in its own schema**, with a single Main control store isolated in `wolverine`:

```
scheduling.dose_records                     ← aggregate
scheduling.wolverine_outgoing_envelopes     ← Scheduling's OUTBOX
scheduling.wolverine_incoming_envelopes     ← Scheduling's INBOX
adherence.adherence_entries                 ← read model
adherence.wolverine_outgoing_envelopes      ← Adherence's OUTBOX
adherence.wolverine_incoming_envelopes      ← Adherence's INBOX  (idempotency)
wolverine.wolverine_nodes / _node_assignments / _control_queue / …   ← single Main control store
```

Wiring (`src/MessagingPoc.Api/Program.cs`): one `MessageStoreRole.Main` store in the `wolverine`
schema, plus one `MessageStoreRole.Ancillary` store **per module** enrolled to that module's
`DbContext` — Wolverine's officially documented modular-monolith pattern.

### The ASB round-trip on the emulator (the #51 money shot)

From [`evidence/asb-roundtrip.txt`](evidence/asb-roundtrip.txt):

```
ServiceBus Emulator is launching with config … "Queues":[{"Name":"dose-events" …
Creating queue: dose-events
Started message listening at asb://queue/dose-events
Successfully processed message …Scheduling.Contracts.DoseRecorded#… from asb://queue/dose-events
```

---

## Findings worth carrying into the M0 messaging design

These are the reason the spike exists — each cost an iteration to find and each changes how the
real thing is wired.

1. **Wolverine 6.20 removed the runtime Roslyn compiler from core** ([GH-2876](https://github.com/JasperFx/wolverine/issues/2876)).
   With the default `TypeLoadMode.Dynamic`, startup throws *"no IAssemblyGenerator (Roslyn) is
   registered"* until you either reference `WolverineFx.RuntimeCompilation` **or** adopt **static
   codegen** (`codegen write` + `TypeLoadMode.Static`). The spike uses the runtime package for speed;
   **production should adopt static codegen — which [ADR-0002](../../docs/adr/0002-architecture-style.md)
   already anticipates** ("`codegen write` for reviewable generated code"). This was not in any docs
   the research pass found; it's a genuine spike catch.

2. **`UseEntityFrameworkCoreTransactions()` alone is not enough.** Without
   `opts.Policies.AutoApplyTransactions()`, handlers add entities that are **never saved** and cascaded
   events publish **outside the outbox** (messages appeared "successfully processed" while *zero* rows
   and *zero* envelopes were written). Both calls are required. This is the single most important
   line to get right in the real wiring.

3. **The EF Core 11 preview.5 runtime compatibility is confirmed** — the green round-trip exercised
   Wolverine's Weasel-on-EF-Core-10 integration against the EF Core 11 preview.5 + Npgsql runtime with
   no `MissingMethodException`. Restore also unifies cleanly (Wolverine's `>= 10.0.2` floors resolve up
   to the preview). The recorded **EF Core 10 GA fallback pin stays valid** but is not needed today.

4. **Aspire's ASB emulator is queue-only-friendly but has two rough edges** (both handled, both worth
   knowing): (a) it emits a *recoverable* validation warning because it wants the namespace named
   `sbemulatorns` while Aspire names it after the resource (`messaging`) — it proceeds and creates the
   queue anyway; (b) `AddDatabase` doesn't guarantee the DB exists before the app connects, so early
   `database "messagingdb" does not exist` blips need a short retry (the spike retries; **production
   provisions through the `MigrationService`**, so this is a test-harness-only concern). Wolverine runs
   with **`AutoProvision` OFF** and `SystemQueuesAreEnabled(false)` — the queue is pre-seeded by the
   AppHost, mirroring the prod "data-plane-only identity" posture exactly.

---

## Run it yourself

```bash
cd spikes/messaging-poc
dotnet test tests/MessagingPoc.IntegrationTests/MessagingPoc.IntegrationTests.csproj
# or explore interactively:
aspire run --project src/MessagingPoc.AppHost   # then POST /scheduling/record-dose, GET /adherence/entries, GET /diag/topology
```
Requires Docker (Postgres + `servicebus-emulator:2.0.0` + its `mssql/server` sidecar — all pre-pulled).

---

## The real-namespace smoke — ✅ DONE 2026-07-18

The emulator emulates the **Standard**-tier surface, so it **cannot catch a Basic-tier violation**
(it will happily allow topics/sessions locally). That last check is now done: Jakub created a real
**Basic** namespace (`doseup-spike-sb-basic`, rg `doseup-spikes`) and the unchanged PoC — `AutoProvision`
off, `SystemQueuesAreEnabled(false)`, queue-only — did a clean send→receive round-trip against it in
~2s, with **no** topic/subscription/session/system-queue errors and no `MessagingEntityNotFound`.
Notably Wolverine keeps its reply endpoint **in-memory** (`stub://replies/`), so it needs no ASB
system queue — which is precisely why Basic tier suffices. Full proof, secret-free:
[`evidence/asb-basic-real-namespace.txt`](evidence/asb-basic-real-namespace.txt). **#50 is now fully
confirmed.** (Reproduce with the Bicep + steps in [`../README.md`](../README.md); remaining
housekeeping is deleting the namespace, Jakub's call.)

## Recorded fallbacks (unchanged, all still valid)
- Runtime break on the EF preview → pin EF Core + Npgsql.EFCore to **10.0.x GA** (Wolverine's tested floor).
- Emulator too heavy/flaky in CI → the standalone `servicebus-emulator` via docker-compose, or the
  recorded **stub transport for slice tests + deployed-env smoke** ([testing.md §3d](../../docs/conventions/testing.md)).
- Basic tier too restrictive → **ASB Standard** (reopens topic/session options) — recorded in ADR-0001.
