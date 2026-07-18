# Spike #62 - Entra app registration from the deployment pipeline

> **Status: spike answered — implementation tracked in [#75](https://github.com/jakubbohm/DoseUp/issues/75).**
> We deliberately did **not** run the live tenant deployment here; the real thing (promoting this
> PoC to `infra/identity/`, the `identity.yml` workflow, and the one-time bootstrap sub-issues
> [#76–#79](https://github.com/jakubbohm/DoseUp/issues/75)) is owned by #75. Two of this spike's
> open risks have since been resolved in our favour against current MS Learn (2026-05): GitHub OIDC
> **into** a CIAM tenant is documented as supported ("same as workforce"), and customer-app admin
> consent **is** IaC via `oauth2PermissionGrants`/`preAuthorizedApplications`. This folder stays as
> the offline-verified proof.

**Question:** Can an Entra app registration (sign-in setup) be created/updated
from the deployment pipeline instead of clicking in the portal?

**Verdict: CONDITIONAL GO.** The app registration *is* codifiable and
pipeline-deployable - declaratively, idempotently, via the **GA Microsoft Graph
Bicep extension** - which fits ADR-0004 (Azure/identity as hand-authored code).
The conditions are structural to CIAM, not blockers:

1. DoseUp's identity is **Microsoft Entra External ID (CIAM)**. A CIAM tenant
   has **no Azure subscription**, so the template must be **Graph-resources-only**
   and deploy at **tenant scope** (`az deployment tenant create`). It cannot be
   co-deployed with the Azure infra Bicep - **two deployments, two tenants**.
2. A **one-time human bootstrap** is unavoidable: create a deploy principal,
   grant it a Graph app role **with admin consent**, and (for the declarative
   path) do a Global-Admin **"Elevate access"** + assign an ARM role at root `/`.
   The pipeline cannot self-consent or self-elevate.
3. "App registration as code" **≠ "whole CIAM tenant as code"**. User flows,
   external IdPs, branding, and custom domains are **not** expressible in v1.0
   Graph Bicep - they stay portal / Graph-beta / EAF work.

This spike ships a **declarative path (Plan A, recommended)** and an
**imperative `az ad` fallback (Plan B)** that avoids the ARM elevation.

---

## Verification done offline tonight (no tenant, no login)

Environment: `az` 2.84.0, bundled **Bicep CLI 0.44.1** (GA Graph extension needs
≥ 0.36.1 ✓; a v0.45.15 upgrade is offered but not required).

| Check | Command | Result |
|---|---|---|
| Bicep version | `az bicep version` | `Bicep CLI version 0.44.1 (28275db947)` |
| **Template compiles** | `az bicep build --file appreg.bicep` | **SUCCESS, exit 0** - emitted a valid `tenantDeploymentTemplate` ARM/Graph JSON |
| Param file compiles | `az bicep build-params --file appreg.bicepparam --outfile <tmp>` | **SUCCESS, exit 0** |

The Graph extension **restored from MCR** and resolved fully offline of any
tenant. The generated JSON confirmed:

- Schema: `https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#` (tenant scope resolved correctly).
- Extension import: `"microsoftGraphV1": { "provider": "MicrosoftGraph", "version": "1.0.0" }`.
- Three resources bound to that import:
  - `Microsoft.Graph/applications@v1.0`
  - `Microsoft.Graph/servicePrincipals@v1.0`
  - `Microsoft.Graph/applications/federatedIdentityCredentials@v1.0`

> A clean `bicep build` producing a tenant-scope ARM/Graph template with the
> extension resolved is the **strong offline proof** that this approach compiles
> and is well-formed. (The generated `appreg.json` is deleted after each run -
> the deploy scripts regenerate it.)

### One caveat on the verification command
`az bicep build-params ... --outfile /dev/null` throws
`Unsupported Windows DOS device path`. That is a **Bicep-on-Windows IO quirk
about the `/dev/null` output target**, *not* a problem with the param file -
pointing `--outfile` at a real temp file succeeds (exit 0). Use a real path.

---

## Proven offline vs needs the real tenant tomorrow

| Proven offline tonight | Still needs the real tenant tomorrow |
|---|---|
| Graph Bicep extension is available and restores (≥0.36.1; we have 0.44.1) | The tenant-scope deployment actually **applies** (`az deployment tenant create`) against a CIAM tenant |
| `targetScope='tenant'` + `extension microsoftGraphV1` compile cleanly | Whether the deploy SP's **ARM role at `/`** is grantable/effective in the External ID tenant |
| App + SP + FIC resource **shapes** are valid (displayName, immutable `uniqueName` upsert key, `signInAudience`, SPA + web redirects, exposed `api` scope, `requiredResourceAccess`, GitHub-OIDC FIC) | **GitHub OIDC / workload-identity federation *into* a CIAM tenant** - validated for workforce tenants, **UNCONFIRMED for CIAM** (main open risk) |
| Param file binds and type-checks | Admin-consent grant for `Application.ReadWrite.OwnedBy` on the CIAM tenant |
| Deterministic scope GUID (`guid(uniqueName,'access_as_user')`) gives in-place updates | Idempotency of a real **re-deploy** (second run should update, not duplicate) |
| Fallback `az ad` command shapes are correct | Client-secret/cert fallback path end-to-end if OIDC-into-CIAM fails |

---

## Runbook for tomorrow (ordered, human steps)

All steps run **against the External ID (CIAM) tenant**, once.

1. **Create the deploy principal** in the External ID tenant
   (`az ad sp create-for-rbac --name doseup-appreg-deployer --skip-assignment`,
   or an app + FIC for OIDC).
2. **Grant Microsoft Graph app role** `Application.ReadWrite.OwnedBy`
   (least-privilege; `.All` only if it must manage apps it does not own) **and
   grant admin consent** (Enterprise apps > SP > Permissions > *Grant admin
   consent*).
3. *(Plan A only)* **Elevate + ARM role at root `/`:** a Global Admin toggles
   *Access management for Azure resources = Yes* (one-time "Elevate access"),
   then `az role assignment create --assignee <spObjectId> --role Owner
   --scope "/"` (or a custom role limited to `Microsoft.Resources/deployments/*`).
4. **Set up non-interactive auth:** Plan A = GitHub OIDC/WIF (keyless,
   **verify it works into CIAM** - this is the crux test); fallback = client
   secret/cert on the deploy SP.
5. **Deploy:** `./deploy.ps1` (or `deploy.sh`) with `EXTERNAL_TENANT_ID` /
   `DEPLOY_CLIENT_ID` (+ secret or OIDC) set. Confirm outputs `appId`,
   `servicePrincipalId`, `apiScope`.
6. **Verify idempotency:** run the deploy a second time - it must **update in
   place**, not create a duplicate app (the `uniqueName` upsert key).
7. **If Plan A is blocked** on the ARM `/` elevation or OIDC-into-CIAM: run
   `./fallback-az-cli.ps1 -Go` (or `fallback-az-cli.sh --go`). It needs **only**
   the Graph app role from steps 1-2, **no ARM elevation**.

---

## Recommendation: Graph Bicep (Plan A), az CLI as the fallback

**Prefer the declarative Graph Bicep path.** It matches ADR-0004
(hand-authored IaC, deployment-stack mindset), gives **idempotent upsert** on the
immutable `uniqueName` (no hand-rolled find-or-create), supports **what-if**, and
keeps the app registration reviewable as code next to the infra Bicep. The
imperative `az ad` script has to hand-roll find-or-create *and* fall back to a
raw Graph `PATCH` for the SPA/scope blocks that `az ad app` has no flags for -
more moving parts, no what-if, weaker idempotency.

**Keep `az ad` (Plan B) as the recorded fallback** for exactly two failure
modes: (a) the ARM root-`/` elevation is refused/unavailable on the CIAM tenant,
or (b) tenant-scope ARM deployment misbehaves there. Plan B talks straight to
Graph with the login token and needs **only** `Application.ReadWrite.OwnedBy` +
admin consent - **no ARM elevation at all**.

### Recorded fallbacks (in priority order)
1. **Auth:** GitHub OIDC/WIF into CIAM → **fallback** client secret/cert on an SP
   created directly in the External ID tenant.
2. **Mechanism:** declarative Graph Bicep (tenant scope) → **fallback** imperative
   `az ad` (sidesteps the ARM `/` elevation entirely).

---

## Honest limits (could NOT be verified without a tenant)

- No deployment was executed; **no `az login`**, no tenant, no GitHub mutation.
- **OIDC federation into a CIAM tenant is unconfirmed** - the single biggest
  thing to prove tomorrow.
- Runtime behaviours unprovable offline: admin-consent effect, ARM `/` role
  effectiveness on CIAM, real re-deploy idempotency, and the SP's ability to
  create objects it will own.
- Scope of "as code" is the **app registration + SP + FIC only**; the rest of the
  CIAM sign-in experience remains portal/beta/EAF work.

---

## Files in this spike

| File | Purpose |
|---|---|
| `bicepconfig.json` | Declares the GA Microsoft Graph Bicep extension alias (`microsoftGraphV1`). |
| `appreg.bicep` | `targetScope='tenant'` template: app registration + service principal + GitHub-OIDC FIC. Heavily commented. |
| `appreg.bicepparam` | Parameter values (placeholder `doseup.example` URIs, repo coordinates). |
| `deploy.ps1` / `deploy.sh` | **Plan A** tenant-scope deploy; guarded (dry-run/prints the runbook unless env vars set). |
| `fallback-az-cli.ps1` / `fallback-az-cli.sh` | **Plan B** imperative `az ad` path; guarded (needs `-Go` / `--go` / `DOSEUP_APPREG_GO=1`). |
| `README.md` | This writeup. |

> Throwaway spike. Nothing here is wired into the main build
> (`spikes/Directory.Build.props` / `.Packages.props` only isolate .NET projects).
