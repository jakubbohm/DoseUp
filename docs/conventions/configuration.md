# DoseUp — Configuration

**Status:** decided 2026-07-19 (configuration interview — resolves [#40](https://github.com/jakubbohm/DoseUp/issues/40)) · part of [conventions](README.md) — docs-first source of truth

The question this answers is not "how do we read settings" but **"where is X configured, and where does its value live?"** — answerable without reading `Program.cs`.

## 1. The one rule

> **The app's only configuration interface is `appsettings.json` + environment variables. Everything platform-specific lives in Bicep.**

Every store below is therefore just an answer to *who produces the environment variable*. The consequence is deliberate and load-bearing: the container cannot tell whether a value came from Key Vault, a `.bicepparam` literal, an Aspire parameter, or your shell — so it runs unchanged on Azure Container Apps, on Kubernetes, or on a laptop. **No Azure SDK ever appears in the configuration path.** That is what keeps the ACA-specific wiring (§3) a deployment detail rather than a dependency: the platform-specific part lives in the resource definition, where platform-specific things belong.

## 2. Classification — which store?

Three questions, asked in order, decide every setting:

1. **Is it secret?** — would leaking it cause harm.
2. **Must it change without rebuilding the image?** — and if so, is a restart acceptable, or does it need a live reload.
3. **Who knows the value, and when?** — the developer at code time, the deployment at deploy time, or a human out-of-band.

| | non-secret | secret |
|---|---|---|
| **known at code time, rebuild on change** | `appsettings.json` | — *(never; a committed secret is a leaked secret)* |
| **local-only deviation** | `appsettings.Development.json` | AppHost user-secrets → Aspire parameter |
| **known at deploy time, restart on change** | env var, authored in Bicep from `.bicepparam` or a resource reference | Key Vault → ACA secret → env var |
| **needs live reload (no restart)** | *deferred — see §10* | *deferred — see §10* |
| **not knowable by the pipeline** | — | Key Vault, hand-provisioned (§9) |

## 3. The stores

**`appsettings.json`** — non-secret settings whose value is part of the code's meaning, and **the declaration of every setting that exists** (§4). Ships inside the image; changing one is a rebuild. This file is production truth.

**`appsettings.Development.json`** — local deviations only (verbose logging, dev-only toggles). Development is also the test harness's environment.

**There is no `appsettings.Production.json`** — see §10.

**Environment variables** — the universal interface. In production they are **authored in Bicep**, sourced either from a `.bicepparam` value or directly from a resource reference in the same deployment. The release workflow supplies only the image tag and which `.bicepparam` to apply; it never reads a value off one resource to hand it to another (that would put secrets through the runner's environment and create cross-job ordering where the deployment already has the symbolic reference). Nested keys use the `__` separator (`Auth__TestAuthority__Issuer`).

**AppHost user-secrets → Aspire parameters** — the local and test channel for secrets, and the mirror of production. `builder.AddParameter("name", secret: true)` in the AppHost resolves from `Parameters:name` in the **AppHost's** user-secrets and is injected into consumers as an environment variable. Per-service `secrets.json` files are not used: one store, one place to look, and the consumer sees exactly what it will see in production.

**The Aspire resource graph** — a configuration store in its own right, and the least discoverable one. `WithReference()` generates `ConnectionStrings__doseupdb`, which is why `GetConnectionString("doseupdb")` resolves against no file anywhere in the repo. **Bicep must produce the identical variable name**; that naming contract is precisely what the procedural AppHost↔Bicep sync ([ADR-0004-delivery-and-process § Infrastructure as code](../adr/0004-delivery-and-process.md)) exists to protect.

**Azure Key Vault** — secrets known at deploy time, plus every secret the pipeline cannot read (§9). The container app declares a secret with `keyVaultUrl` + `identity`, resolved by its own managed identity holding **Key Vault Secrets User**, and a container env var references it with `secretRef` (verified against Microsoft Learn 2026-07-19). The app holds no Key Vault client.

Two mechanics worth knowing, both verified 2026-07-19: a **version-less** secret URI auto-refreshes within 30 minutes and **automatically restarts the revisions referencing it** — so rotation needs no deploy and no in-app vault client; and a *system-assigned* identity cannot be used at container-app creation time (it does not exist yet), so a **user-assigned identity** is the cleaner shape for a single-shot stack deployment.

The **Key Vault configuration provider** — the app reading the vault itself — is deliberately *not* used: it would put an Azure SDK and an Azure credential in the startup path and make the container un-runnable off Azure, which is the entanglement §1 exists to prevent. The rotation story above removes its last remaining argument.

**`infra/*.bicep`** — resource shape and wiring, including which environment variables each container app gets.

**`infra/<env>.bicepparam`** — every environment-specific value (tiers, scale bounds, names, flag overrides). **Never a secret literal**; if a secret ever must be a deployment parameter, the sanctioned route is `az.getSecret(...)` against a `@secure()` parameter.

**GitHub Actions secrets** — pipeline-to-external-system authentication only, and OIDC federated credentials are the required first choice ([ADR-0004-delivery-and-process § Infrastructure as code](../adr/0004-delivery-and-process.md)). **An application secret never lives here** — Key Vault is both more secure and the only store the running app can reach. With OIDC covering Azure, this store should be empty.

**GitHub Actions variables** — see §10; near-rejected, with one narrow carve-out.

**External systems** — the Entra External ID tenant (redirect URIs, user flows, token lifetimes) and the Neon project hold real configuration that lives in neither the repo nor Azure. Entra is the documented exception to the no-portal rule ([ADR-0004-delivery-and-process § Infrastructure as code](../adr/0004-delivery-and-process.md)) until [#75](https://github.com/jakubbohm/DoseUp/issues/75) makes app registrations pipeline-owned; Neon's setup lives in the M0 runbook.

## 4. `appsettings.json` is the inventory

There is no separate settings table anywhere in these docs, deliberately: a table drifts from reality, a file the application actually reads cannot drift as far.

- **Every setting the app consumes appears here** — including settings whose value always comes from somewhere else. The file is the *schema*: the complete shape of what is configurable.
- **A secret's value is the literal string `"<secret>"`** — greppable, obviously not a value, and impossible to mistake for one.
- **Provenance is a comment on the key.** The .NET JSON configuration provider skips `//` comments and tolerates trailing commas, so the answer to "where does this come from in production" sits on the setting itself:

  ```jsonc
  "Neon": {
    // prod: Key Vault (hand-provisioned — §9) · local + test: AppHost user-secrets
    "ConnectionString": "<secret>"
  }
  ```

- **A non-secret value that is supplied per environment still carries its production value here** — the base file is production truth (§3), so it stays readable as the statement of what production runs; the environment variable overrides it locally or in a future second environment. Only *secrets* get a placeholder, because only secrets cannot be written down.
- **`<secret>` is a tripwire, not just a marker.** A shared options validator rejects any bound string still equal to `<secret>`, so a setting that was never supplied fails startup with a message naming the key, instead of the placeholder silently becoming a runtime value. It fires only on options that are actually bound — a declared-but-unbound setting does not fail startup.

## 5. Binding & validation

- **Every consumed setting binds to an options class** registered with `BindConfiguration(...)` + `ValidateDataAnnotations()` + `ValidateOnStart()`, plus the shared `<secret>` validator. **A misconfigured app refuses to start** — fail-fast is what makes configuration discoverable, because the error names the missing key.
- **The configuration section is named after the options class** (`AuthOptions` ↔ `"Auth"`). No indirection to trace.
- **`IConfiguration`/`IConfigurationSection` are not injected outside Platform composition** — the same discipline as the `DateTime.UtcNow` ban ([conventions/README.md § SharedKernel discipline](README.md)): reading configuration ad-hoc makes a setting invisible to both the schema file and the validator. Enforcement owner: ArchUnitNET rule 20 ([conventions/testing.md § 5](testing.md)).
- Options classes live in **Platform** unless a setting is genuinely one module's property, in which case it lives with the module.

Implementation — including migrating the two existing hand-rolled reads in `Program.cs` — is tracked in [#89](https://github.com/jakubbohm/DoseUp/issues/89). No new CI gate is needed for any of this: the harness starts the app in the PR suite and the post-deploy smoke probe starts it in production, so a configuration mistake fails an existing gate on its own.

## 6. Feature flags & kill switches

Flags use **Microsoft.FeatureManagement** ([ADR-0001-platform-and-stack](../adr/0001-platform-and-stack.md)), which reads from `IConfiguration` — so `IFeatureManager` works identically no matter which store below holds the value, and adopting a configuration service later changes one provider registration and nothing else.

- **A flag is declared, with its default, in `appsettings.json`** — it is a setting like any other (§4).
- **A production override is an environment variable declared in `.bicepparam`** (`FeatureManagement__NewThing=false`).

That yields three flip latencies, and the middle one is the standard:

| where the value lives | flipping costs | reversible by |
|---|---|---|
| `appsettings.json` | rebuild + full release pipeline | a commit |
| `.bicepparam` → ACA env var | infra pipeline run, **no rebuild** | a commit |
| break-glass `az containerapp update` | seconds | the reconciliation commit |

**Break-glass is explicitly sanctioned and explicitly bounded.** A kill switch that requires a green pipeline is not a kill switch when the pipeline is what is broken, so an out-of-band environment-variable flip is permitted **only** to disable something in an incident, and the reconciling `.bicepparam` commit is part of the same incident — not a follow-up. This is a deliberate, narrow carve-out from the no-ad-hoc-CLI rule ([ADR-0004-delivery-and-process § Infrastructure as code](../adr/0004-delivery-and-process.md)); the deployment stack surfaces the drift until the commit lands.

Every flag still ships with a removal task ([ADR-0004-delivery-and-process § Feature flags](../adr/0004-delivery-and-process.md)).

## 7. Local development & the test harness

- Local secrets live in the **AppHost's** user-secrets as Aspire parameters (§3) — never in a per-service `secrets.json`.
- **The test harness *is* the AppHost.** `DistributedApplicationTestingBuilder` builds the AppHost project, so test projects get **no secrets store of their own**; whatever the AppHost can resolve, the harness can resolve. Verified in the Aspire + hosting sources (2026-07-19): the testing factory sets the host's `ApplicationName` to the **AppHost** assembly, and the standard host defaults load user secrets keyed off that name — which is exactly why the AppHost's store is the one read, not the test project's.
- **That inheritance is conditional, and fails silently.** User secrets load only when the environment resolves to `Development`, which the testing factory takes from the AppHost's `launchSettings.json` (first profile when `DOTNET_LAUNCH_PROFILE` is unset). If the AppHost ever loses its `launchSettings.json`, or a profile drops `DOTNET_ENVIRONMENT`, the host falls back to `Production` and **secrets are skipped with no error** — the same trap the Aspire docs' own `--environment=Testing` example would spring. DoseUp's AppHost sets `DOTNET_ENVIRONMENT: Development` in both profiles today; treat that file as load-bearing for the harness, not merely as F5 ergonomics.
- In CI, a GitHub Actions secret becomes an environment variable on the test job and is picked up as `Parameters__<name>` — environment variables outrank user-secrets in the configuration chain. Same parameter name, different source; nothing is copied between files.
- **The harness is designed to need zero real secrets**, and that is the stronger rule: Postgres is a container, the async seam runs the ASB emulator and Azurite, and identity is a test-only trust anchor minting self-signed tokens ([conventions/testing.md § 3](testing.md), which rejects real-IdP test users partly *because* they drag in secrets and an external CI dependency). A change proposing a harness secret is a signal that a test is reaching for a real external system — justify it or fix the test. The mechanism above exists so that justification has a sanctioned path, not so it becomes routine.

## 8. Frontend configuration

**Vite inlines `VITE_`-prefixed variables into the bundle at build time.** Two rules follow, and the first is stronger than "no secrets":

- **Nothing goes in a `VITE_` variable that could not be published on a billboard** — it is compiled into shipped JavaScript, permanently and irrevocably. In particular the sign-in flow is a public client with PKCE and **no client secret anywhere near the web app**.
- **Nothing environment-specific goes in a `VITE_` variable either.** Build-time inlining makes the bundle valid for exactly one environment, which breaks the build-once-promote-identical-artifacts property the release pipeline rests on ([ADR-0004-delivery-and-process § CD](../adr/0004-delivery-and-process.md)). It would not bite today with a single environment; it would bite the day a second one appears.

`VITE_` variables are therefore limited to genuinely build-time, environment-invariant values (app name, build SHA). Everything known at **deploy** time — the API address, the sign-in client id, authority and scope, the VAPID public key — travels in a **`config.json` that the release pipeline writes beside the bundle** on the static host, generated from the same Bicep outputs that configure the API. The app fetches it from **its own origin** before bootstrapping, and that is what makes the pattern work at all: a site always knows where it is itself served from, so nothing has to be baked in to find it.

**Fetching this from the API is rejected, not merely dispreferred** — the call needs the API's address, which is the very value being fetched. Nor can a container entrypoint generate the file, the way it could for a server-rendered app: a static host runs no process. On static hosting, the pipeline writing the file is not a compromise, it is the only available mechanism.

Build-once survives at the right granularity: the **bundle** is the artifact promoted unchanged, and `config.json` is deployment metadata — precisely what `.bicepparam` is for the API ([ADR-0004-delivery-and-process § CD](../adr/0004-delivery-and-process.md)).

Consequences that belong in the implementation ([#87](https://github.com/jakubbohm/DoseUp/issues/87)):

- **The web app and the API sit on different origins.** DoseUp serves the PWA from **Azure Static Web Apps Free** while the API stays on Container Apps ([ADR-0001-platform-and-stack § Hosting (web app)](../adr/0001-platform-and-stack.md)); the linked-backend proxy that would restore same-origin requires the Standard plan, and a custom domain would not merge the origins either. So the API needs a CORS allowlist for the static hostname, and the app-open path pays a preflight worth capping with `Access-Control-Max-Age`.
- **`config.json` must not be cached like the bundle.** Hashed assets stay immutably cached; the config file is served with revalidation, and the service worker must exclude it from precache — otherwise a deploy never reaches already-installed clients.
- **Locally the same file exists**, with the API address set to `/api`, which the Vite dev server proxies to the address Aspire assigned. Same mechanism, different value, no special-casing in the app.
- **Validate it at startup** as the backend validates its options (§5): a missing or incomplete `config.json` fails loudly naming what is absent, rather than producing `undefined` inside a URL.

**A runtime config *endpoint*, if one is ever added, carries only what must change without a redeploy** — feature flags and UI-visible toggles. Anything known at build or deploy time belongs in `config.json`; routing it through an API call buys a network dependency and a round-trip for a value that could not have changed since deployment. This is the frontend counterpart of the App Configuration deferral (§10).

Vite's own convention is that `.env` is committed and `.env.local` is ignored; the repo's `.gitignore` currently ignores `.env`, which the web scaffold change should revisit if it wants committed non-secret defaults.

## 9. Hand-provisioned secrets

Two values cannot be produced by Bicep, because they do not come from Azure resources ([ADR-0004-delivery-and-process § Infrastructure as code](../adr/0004-delivery-and-process.md) records both as the non-RBAC exceptions):

- **the Neon connection string** — Neon is not an Azure resource and has no managed-identity path
- **the VAPID key pair** — generated once, out-of-band

Bicep declares the vault, the RBAC assignment, and the container app's reference to the secret **by name**; a human puts the value in. This is the category that silently breaks disaster recovery, so it is written here rather than inferred: a rebuilt-from-git environment comes up with these two secrets missing, and the app will say so at startup (§5).

## 10. Rejected and deferred

**Azure App Configuration — deferred, not adopted** *(reverses the founding pick for now)*. It remains the right store for values that must change without a restart, but nothing in DoseUp currently is that: reminder windows and quiet hours are per-profile rows in Postgres, not configuration. Cost decided the timing — Standard is ≈ €36/month against a €20 NFR-6 ceiling, and the Free tier's **1,000 requests/day** does not survive flag polling (flags are polled separately from the sentinel key, one request per 100 flags per refresh interval, per replica — roughly one request per 86 seconds of total budget). Since `Microsoft.FeatureManagement` reads from `IConfiguration` either way, adopting it later is a provider registration. Re-open when a genuinely reload-required setting exists, or when flag flips at pipeline latency (§6) prove too slow.

**`appsettings.Production.json` — rejected.** With one deployed environment, a "production value" *is* the default, so the file would duplicate `appsettings.json` while making the schema file (§4) show values that are not real. Anything genuinely environment-varying is infrastructure-shaped and belongs in an environment variable — which is also strictly better for flags, since a `.bicepparam` override flips without a rebuild (§6) and is equally committed and reviewable. `Development.json` carries local deviations; the base file stays the single readable statement of what production runs.

**GitHub Actions *variables* — rejected, with one carve-out.** Anything Bicep consumes belongs in `.bicepparam`: versioned, diffable, reviewed. An Actions variable holding the same class of value is an unversioned parallel store — exactly the failure this document exists to prevent. The carve-out is values needed *before* Bicep can run, so the workflow can authenticate at all (subscription id, resource group, environment name); those live on the GitHub Environment.

**Key Vault configuration provider — rejected** (§3): Azure SDK + credential in the startup path, for no benefit over an environment variable the platform already resolves.

**A settings inventory table in these docs — rejected** (§4): it would drift from the file the application actually reads. The schema file is the inventory.
