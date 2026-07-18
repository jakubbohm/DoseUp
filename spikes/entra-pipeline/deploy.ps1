#requires -Version 7
<#
================================================================================
 Spike #62 - PLAN A: declarative tenant-scope deploy (Microsoft Graph Bicep)
================================================================================
 Deploys appreg.bicep to the DoseUp Microsoft Entra External ID (CIAM) tenant.

 GUARD: this script is a NO-OP unless the required env vars are set. Run it with
 nothing configured and it just PRINTS the runbook - it never blindly deploys.

 ----------------------------------------------------------------------------
 ONE-TIME HUMAN PREREQUISITES (cannot be automated - pipeline can't self-grant):
 ----------------------------------------------------------------------------
  1. Create a deploy service principal IN the External ID tenant:
        az ad sp create-for-rbac --name "doseup-appreg-deployer" --skip-assignment
     (or an app + FIC for OIDC - see step 4).
  2. Grant it Microsoft Graph app role  Application.ReadWrite.OwnedBy  (least-priv;
     use .All only if it must manage apps it does not own) AND grant ADMIN CONSENT.
        - portal: Enterprise apps > the SP > Permissions > Grant admin consent
        - or: az ad app permission add/admin-consent against Graph app id
          00000003-0000-0000-c000-000000000000
  3. Give it an ARM role at TENANT ROOT scope "/":
        a. A Global Admin does a one-time "Elevate access" (Entra admin center >
           Properties > Access management for Azure resources = Yes).
        b. az role assignment create --assignee <spObjectId> \
             --role Owner  --scope "/"
           (or a custom role limited to Microsoft.Resources/deployments/*).
        NOTE: tenant-scope `az deployment tenant create` requires this ARM role -
        the Graph app role alone is NOT enough for the deployment engine.
  4. Wire non-interactive auth (pick ONE):
        A) GitHub OIDC / workload identity federation (preferred, keyless) - the
           FIC in appreg.bicep, or one created out-of-band. *UNCONFIRMED for CIAM*
           - verify on the real tenant.
        B) Client secret / certificate on the deploy SP (fallback, always works).

 ----------------------------------------------------------------------------
 REQUIRED ENV VARS to actually deploy (guard checks these):
   EXTERNAL_TENANT_ID   - the External ID (CIAM) tenant id or *.onmicrosoft.com
   DEPLOY_CLIENT_ID     - the deploy SP / app client id
   DEPLOY_CLIENT_SECRET - client secret            (omit if using OIDC in CI)
   DEPLOY_LOCATION      - a region for the deployment metadata, e.g. westeurope
 ----------------------------------------------------------------------------
#>

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$template  = Join-Path $scriptDir 'appreg.bicep'
$params    = Join-Path $scriptDir 'appreg.bicepparam'

$tenantId = $env:EXTERNAL_TENANT_ID
$clientId = $env:DEPLOY_CLIENT_ID
$secret   = $env:DEPLOY_CLIENT_SECRET
$location = if ($env:DEPLOY_LOCATION) { $env:DEPLOY_LOCATION } else { 'westeurope' }

if (-not $tenantId -or -not $clientId) {
    Write-Host ''
    Write-Host '=== Spike #62 deploy.ps1 - DRY RUN (env vars not set) ===' -ForegroundColor Yellow
    Write-Host 'Set EXTERNAL_TENANT_ID and DEPLOY_CLIENT_ID (plus DEPLOY_CLIENT_SECRET or use OIDC) to deploy.'
    Write-Host 'See the header comment for the one-time human prerequisites.'
    Write-Host ''
    Write-Host 'Would run:'
    Write-Host '  1. az login --service-principal -u <DEPLOY_CLIENT_ID> -p <secret|cert> --tenant <EXTERNAL_TENANT_ID> --allow-no-subscriptions'
    Write-Host '     (in GitHub Actions instead use azure/login@v2 with OIDC:'
    Write-Host '        with: { client-id, tenant-id, allow-no-subscriptions: true }  # no client-secret)'
    Write-Host "  2. az bicep build --file `"$template`""
    Write-Host "  3. az deployment tenant create --location $location --template-file `"$template`" --parameters `"$params`""
    Write-Host ''
    Write-Host 'DRY RUN complete - nothing deployed.' -ForegroundColor Yellow
    return
}

Write-Host 'Logging in to the External ID tenant (service principal)...' -ForegroundColor Cyan
# --allow-no-subscriptions: a CIAM tenant has no Azure subscription.
if ($secret) {
    az login --service-principal -u $clientId -p $secret --tenant $tenantId --allow-no-subscriptions | Out-Null
} else {
    # In CI, azure/login@v2 (OIDC) has already established the session; assume logged in.
    Write-Host 'No DEPLOY_CLIENT_SECRET set - assuming an OIDC session (azure/login) is active.' -ForegroundColor Cyan
}

Write-Host 'Compiling template (restores the Graph extension if needed)...' -ForegroundColor Cyan
az bicep build --file $template

Write-Host 'Deploying at TENANT scope...' -ForegroundColor Cyan
az deployment tenant create `
    --name "doseup-appreg-$(Get-Date -Format yyyyMMddHHmmss)" `
    --location $location `
    --template-file $template `
    --parameters $params

Write-Host 'Done. Read outputs (appId, servicePrincipalId, apiScope) from the deployment result above.' -ForegroundColor Green
