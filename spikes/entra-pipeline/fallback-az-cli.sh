#!/usr/bin/env bash
# =============================================================================
# Spike #62 - PLAN B (FALLBACK): imperative `az ad` path
# =============================================================================
# Does the SAME app registration + SP + FIC as appreg.bicep, but imperatively
# through `az ad`, which talks straight to Microsoft Graph with the login token.
#
# WHY THIS EXISTS: it SIDESTEPS the ARM tenant-root "/" elevation entirely. The
# deploy principal needs ONLY the Microsoft Graph app role
# (Application.ReadWrite.OwnedBy + admin consent) - no "Elevate access", no ARM
# role at "/". Safer bet if the tenant-scope ARM deploy (Plan A) is blocked on
# the real CIAM tenant tomorrow.
#
# COST: you lose declarative idempotency / what-if - note how much manual
# "find-or-create" + a Graph PATCH we hand-roll for the SPA/scope block Bicep
# gave us for free.
#
# GUARD: prints intent and does NOTHING unless you pass --go (or set
# DOSEUP_APPREG_GO=1). Even then it still needs an authenticated `az` session
# into the External ID tenant.
# =============================================================================
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

GO=0
[[ "${1:-}" == "--go" || "${DOSEUP_APPREG_GO:-}" == "1" ]] && GO=1

# ---- desired state (mirror of appreg.bicepparam) ----------------------------
DISPLAY_NAME='DoseUp Platform'
SIGNIN_AUD='AzureADMyOrg'
IDENTIFIER_URI='api://doseup-platform'
WEB_REDIRECT='https://doseup.example/signin-oidc'
SPA_REDIRECT='https://doseup.example/authentication/login-callback'
SCOPE_NAME='access_as_user'
FIC_NAME='github-actions-production'
FIC_SUBJECT='repo:jakubbohm/DoseUp:environment:Production'
FIC_ISSUER='https://token.actions.githubusercontent.com'

if [[ "$GO" -ne 1 ]]; then
  cat <<EOF

=== Spike #62 fallback-az-cli.sh - DRY RUN (pass --go or set DOSEUP_APPREG_GO=1 to run) ===
Prerequisite: authenticated \`az\` session into the External ID tenant with
              Graph app role Application.ReadWrite.OwnedBy + admin consent.
              NO ARM "/" elevation needed for this path.

Would perform (find-or-create = idempotency hand-rolled):
  1. find app by displayName '$DISPLAY_NAME'; if absent: az ad app create
     --display-name '$DISPLAY_NAME' --sign-in-audience $SIGNIN_AUD
     --identifier-uris $IDENTIFIER_URI --web-redirect-uris $WEB_REDIRECT
  2. az rest PATCH .../applications/<objId> to set spa.redirectUris + the
     exposed api scope '$SCOPE_NAME' (az ad app has no first-class SPA/scope flags)
  3. az ad sp create --id <appId>   (if the SP does not already exist)
  4. az ad app federated-credential create --id <appId> --parameters cred.json
     (cred.json: subject '$FIC_SUBJECT')

DRY RUN complete - nothing changed.
EOF
  exit 0
fi

echo "(1) find-or-create the app registration..."
APP_ID="$(az ad app list --filter "displayName eq '$DISPLAY_NAME'" --query '[0].appId' -o tsv)"
if [[ -z "$APP_ID" ]]; then
  APP_ID="$(az ad app create \
    --display-name "$DISPLAY_NAME" \
    --sign-in-audience "$SIGNIN_AUD" \
    --identifier-uris "$IDENTIFIER_URI" \
    --web-redirect-uris "$WEB_REDIRECT" \
    --query appId -o tsv)"
  echo "    created app $APP_ID"
else
  az ad app update --id "$APP_ID" \
    --sign-in-audience "$SIGNIN_AUD" \
    --identifier-uris "$IDENTIFIER_URI" \
    --web-redirect-uris "$WEB_REDIRECT" >/dev/null
  echo "    updated existing app $APP_ID"
fi

OBJ_ID="$(az ad app show --id "$APP_ID" --query id -o tsv)"

echo "(2) PATCH the SPA redirect + exposed API scope via Graph (az ad has no flags for these)..."
# Stable scope GUID so repeat runs update in place; on a real re-run read the existing id first.
SCOPE_GUID="$(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen)"
cat > "$SCRIPT_DIR/app-patch.json" <<EOF
{
  "spa": { "redirectUris": ["$SPA_REDIRECT"] },
  "api": {
    "oauth2PermissionScopes": [
      {
        "id": "$SCOPE_GUID",
        "value": "$SCOPE_NAME",
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
EOF
az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications/$OBJ_ID" \
  --headers 'Content-Type=application/json' \
  --body "@$SCRIPT_DIR/app-patch.json"

echo "(3) ensure the service principal exists..."
SP_ID="$(az ad sp list --filter "appId eq '$APP_ID'" --query '[0].id' -o tsv)"
if [[ -z "$SP_ID" ]]; then
  az ad sp create --id "$APP_ID" >/dev/null
  echo "    service principal created"
else
  echo "    service principal already exists ($SP_ID)"
fi

echo "(4) create the GitHub OIDC federated credential from a heredoc cred.json..."
cat > "$SCRIPT_DIR/cred.json" <<EOF
{
  "name": "$FIC_NAME",
  "issuer": "$FIC_ISSUER",
  "subject": "$FIC_SUBJECT",
  "audiences": ["api://AzureADTokenExchange"],
  "description": "GitHub Actions OIDC for jakubbohm/DoseUp (Production)"
}
EOF
# `create` errors if a FIC with this name already exists - in a real pipeline
# guard with `az ad app federated-credential list` first (idempotency, hand-rolled).
az ad app federated-credential create --id "$APP_ID" --parameters "$SCRIPT_DIR/cred.json"

echo "Done. appId=$APP_ID  objectId=$OBJ_ID"
