#requires -Version 7
<#
================================================================================
 Spike #62 - PLAN B (FALLBACK): imperative `az ad` path
================================================================================
 Does the SAME app registration + SP + FIC as appreg.bicep, but imperatively
 through `az ad`, which talks straight to Microsoft Graph with the login token.

 WHY THIS EXISTS: it SIDESTEPS the ARM tenant-root "/" elevation entirely. The
 deploy principal needs ONLY the Microsoft Graph app role
 (Application.ReadWrite.OwnedBy + admin consent) - no "Elevate access", no ARM
 role at "/". That makes it the safer bet if the tenant-scope ARM deployment
 (Plan A) turns out to be blocked on the real CIAM tenant tomorrow.

 COST: you lose declarative idempotency / what-if. Note below how much manual
 "find-or-create" + a Graph PATCH we have to hand-roll for the SPA block that
 Bicep gave us for free.

 GUARD: prints intent and does NOTHING unless you pass -Go (or set
 DOSEUP_APPREG_GO=1). Even then it still needs an authenticated `az` session
 into the External ID tenant.
================================================================================
#>
param(
    [switch]$Go
)
$ErrorActionPreference = 'Stop'

$go = $Go -or ($env:DOSEUP_APPREG_GO -eq '1')

# ---- desired state (mirror of appreg.bicepparam) ---------------------------
$displayName   = 'DoseUp Platform'
$signInAud     = 'AzureADMyOrg'
$identifierUri = 'api://doseup-platform'
$webRedirect   = 'https://doseup.example/signin-oidc'
$spaRedirect   = 'https://doseup.example/authentication/login-callback'
$scopeName     = 'access_as_user'
$ficName       = 'github-actions-production'
$ficSubject    = 'repo:jakubbohm/DoseUp:environment:Production'
$ficIssuer     = 'https://token.actions.githubusercontent.com'

if (-not $go) {
    Write-Host ''
    Write-Host '=== Spike #62 fallback-az-cli.ps1 - DRY RUN (pass -Go or set DOSEUP_APPREG_GO=1 to run) ===' -ForegroundColor Yellow
    Write-Host 'Prerequisite: authenticated `az` session into the External ID tenant with'
    Write-Host '              Graph app role Application.ReadWrite.OwnedBy + admin consent.'
    Write-Host '              NO ARM "/" elevation needed for this path.'
    Write-Host ''
    Write-Host 'Would perform (find-or-create = idempotency hand-rolled):'
    Write-Host "  1. find app by displayName '$displayName'; if absent: az ad app create"
    Write-Host "     --display-name '$displayName' --sign-in-audience $signInAud"
    Write-Host "     --identifier-uris $identifierUri --web-redirect-uris $webRedirect"
    Write-Host '  2. az rest PATCH .../applications/<objId> to set spa.redirectUris + the'
    Write-Host "     exposed api scope '$scopeName' (az ad app has no first-class SPA/scope flags)"
    Write-Host '  3. az ad sp create --id <appId>   (if the SP does not already exist)'
    Write-Host "  4. az ad app federated-credential create --id <appId> --parameters cred.json"
    Write-Host "     (cred.json: subject '$ficSubject')"
    Write-Host ''
    Write-Host 'DRY RUN complete - nothing changed.' -ForegroundColor Yellow
    return
}

Write-Host '(1) find-or-create the app registration...' -ForegroundColor Cyan
$appId = az ad app list --filter "displayName eq '$displayName'" --query '[0].appId' -o tsv
if ([string]::IsNullOrWhiteSpace($appId)) {
    $appId = az ad app create `
        --display-name $displayName `
        --sign-in-audience $signInAud `
        --identifier-uris $identifierUri `
        --web-redirect-uris $webRedirect `
        --query appId -o tsv
    Write-Host "    created app $appId"
} else {
    az ad app update --id $appId `
        --sign-in-audience $signInAud `
        --identifier-uris $identifierUri `
        --web-redirect-uris $webRedirect | Out-Null
    Write-Host "    updated existing app $appId"
}

$objId = az ad app show --id $appId --query id -o tsv

Write-Host '(2) PATCH the SPA redirect + exposed API scope via Graph (az ad has no flags for these)...' -ForegroundColor Cyan
# Stable scope GUID so repeat runs update-in-place instead of duplicating.
$scopeGuid = [guid]::NewGuid().ToString()  # in a real re-run, first read the existing id
$patch = @"
{
  "spa": { "redirectUris": ["$spaRedirect"] },
  "api": {
    "oauth2PermissionScopes": [
      {
        "id": "$scopeGuid",
        "value": "$scopeName",
        "type": "User",
        "isEnabled": true,
        "adminConsentDisplayName": "Access DoseUp as the signed-in user",
        "adminConsentDescription": "Allows the app to call the DoseUp API on behalf of the signed-in user.",
        "userConsentDisplayName": "Access DoseUp on your behalf",
        "userConsentDescription": "Allows the app to call the DoseUp API on your behalf."
      }
    ]
  }
}
"@
$patch | Out-File -Encoding utf8 -FilePath (Join-Path $PSScriptRoot 'app-patch.json')
az rest --method PATCH `
    --uri "https://graph.microsoft.com/v1.0/applications/$objId" `
    --headers 'Content-Type=application/json' `
    --body "@$(Join-Path $PSScriptRoot 'app-patch.json')"

Write-Host '(3) ensure the service principal exists...' -ForegroundColor Cyan
$spId = az ad sp list --filter "appId eq '$appId'" --query '[0].id' -o tsv
if ([string]::IsNullOrWhiteSpace($spId)) {
    az ad sp create --id $appId | Out-Null
    Write-Host '    service principal created'
} else {
    Write-Host "    service principal already exists ($spId)"
}

Write-Host '(4) create the GitHub OIDC federated credential from a here-string cred.json...' -ForegroundColor Cyan
$credPath = Join-Path $PSScriptRoot 'cred.json'
$cred = @"
{
  "name": "$ficName",
  "issuer": "$ficIssuer",
  "subject": "$ficSubject",
  "audiences": ["api://AzureADTokenExchange"],
  "description": "GitHub Actions OIDC for jakubbohm/DoseUp (Production)"
}
"@
$cred | Out-File -Encoding utf8 -FilePath $credPath
# `create` errors if a FIC with this name already exists - in a real pipeline,
# guard with `az ad app federated-credential list` first (idempotency, hand-rolled).
az ad app federated-credential create --id $appId --parameters $credPath

Write-Host "Done. appId=$appId  objectId=$objId" -ForegroundColor Green
