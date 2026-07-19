---
paths:
  - "**/*.cs"
  - "**/appsettings*.json"
  - "**/*.bicep"
  - "**/*.bicepparam"
  - "**/.env*"
  - "**/vite.config.*"
---

# Configuration

- **The app reads only `appsettings.json` + environment variables.** No Azure SDK, no Key Vault client, no cloud credential in the configuration path — the container must not be able to tell where a value came from.
- **Every setting is declared in `appsettings.json`**, including ones always overridden elsewhere: that file is the schema *and* the settings inventory. A setting that isn't declared there is invisible.
- **A secret's declared value is the literal `"<secret>"`** — never a real value, never omitted. It is also a startup tripwire: a bound value still equal to `"<secret>"` fails validation.
- Provenance goes in a `//` comment on the key (the JSON config parser skips comments): where the value comes from in prod, and where locally.
- **Never create `appsettings.Production.json`** — the base file *is* production truth; `Development.json` carries local deviations; production-only differences are environment variables declared in `.bicepparam`.
- **Bind with `IOptions<T>`** — `BindConfiguration` + `ValidateDataAnnotations` + `ValidateOnStart`, section named after the options class. **Never inject `IConfiguration`/`IConfigurationSection` outside Platform composition** (arch rule 20).
- Local secrets live in the **AppHost's** user-secrets as Aspire parameters — never a per-service or per-test-project `secrets.json`. The test harness inherits the AppHost's store.
- Production env vars are **authored in Bicep** (from `.bicepparam` or a resource reference), never read off a resource by a workflow step and passed along. Secrets reach the container as Key Vault-sourced ACA secrets exposed as env vars.
- **Never put an environment-specific or secret value in a `VITE_` variable** — Vite inlines them into the shipped bundle at build time, which publishes them permanently and breaks build-once-deploy-everywhere. Deploy-time frontend config lives in a `config.json` the release pipeline writes beside the bundle on the static host and the app fetches from its own origin at startup — **never from the API**, which cannot bootstrap the very address needed to call it ([#87](https://github.com/jakubbohm/DoseUp/issues/87)).
- A change that adds a setting declares it in `appsettings.json` in the same change.

Source of truth: [docs/conventions/configuration.md](../../docs/conventions/configuration.md).
