#!/usr/bin/env bash
# =============================================================================
# Spike #62 - PLAN A: declarative tenant-scope deploy (Microsoft Graph Bicep)
# =============================================================================
# Deploys appreg.bicep to the DoseUp Microsoft Entra External ID (CIAM) tenant.
#
# GUARD: NO-OP unless the required env vars are set. Run with nothing configured
# and it just PRINTS the runbook - it never blindly deploys.
#
# -----------------------------------------------------------------------------
# ONE-TIME HUMAN PREREQUISITES (cannot be automated - pipeline can't self-grant):
# -----------------------------------------------------------------------------
#  1. Create a deploy service principal IN the External ID tenant:
#        az ad sp create-for-rbac --name "doseup-appreg-deployer" --skip-assignment
#  2. Grant it Microsoft Graph app role  Application.ReadWrite.OwnedBy  (least-priv;
#     .All only if it must manage apps it does not own) AND grant ADMIN CONSENT.
#        (Graph app id 00000003-0000-0000-c000-000000000000)
#  3. Give it an ARM role at TENANT ROOT scope "/":
#        a. A Global Admin does a one-time "Elevate access"
#           (Entra admin center > Properties > Access management for Azure resources = Yes).
#        b. az role assignment create --assignee <spObjectId> --role Owner --scope "/"
#           (or a custom role limited to Microsoft.Resources/deployments/*).
#        NOTE: tenant-scope deployment needs this ARM role - the Graph app role
#        alone is NOT enough for the deployment engine.
#  4. Wire non-interactive auth (pick ONE):
#        A) GitHub OIDC / workload identity federation (preferred, keyless).
#           *UNCONFIRMED for CIAM* - verify on the real tenant.
#        B) Client secret / certificate on the deploy SP (fallback, always works).
#
# -----------------------------------------------------------------------------
# REQUIRED ENV VARS to actually deploy:
#   EXTERNAL_TENANT_ID    - the External ID (CIAM) tenant id or *.onmicrosoft.com
#   DEPLOY_CLIENT_ID      - the deploy SP / app client id
#   DEPLOY_CLIENT_SECRET  - client secret          (omit if using OIDC in CI)
#   DEPLOY_LOCATION       - a region, e.g. westeurope (default below)
# -----------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE="$SCRIPT_DIR/appreg.bicep"
PARAMS="$SCRIPT_DIR/appreg.bicepparam"
LOCATION="${DEPLOY_LOCATION:-westeurope}"

if [[ -z "${EXTERNAL_TENANT_ID:-}" || -z "${DEPLOY_CLIENT_ID:-}" ]]; then
  cat <<EOF

=== Spike #62 deploy.sh - DRY RUN (env vars not set) ===
Set EXTERNAL_TENANT_ID and DEPLOY_CLIENT_ID (plus DEPLOY_CLIENT_SECRET or use OIDC) to deploy.
See the header comment for the one-time human prerequisites.

Would run:
  1. az login --service-principal -u <DEPLOY_CLIENT_ID> -p <secret|cert> \\
       --tenant <EXTERNAL_TENANT_ID> --allow-no-subscriptions
     (in GitHub Actions instead use azure/login@v2 with OIDC:
        with: { client-id, tenant-id, allow-no-subscriptions: true }  # no client-secret)
  2. az bicep build --file "$TEMPLATE"
  3. az deployment tenant create --location $LOCATION \\
       --template-file "$TEMPLATE" --parameters "$PARAMS"

DRY RUN complete - nothing deployed.
EOF
  exit 0
fi

echo "Logging in to the External ID tenant (service principal)..."
# --allow-no-subscriptions: a CIAM tenant has no Azure subscription.
if [[ -n "${DEPLOY_CLIENT_SECRET:-}" ]]; then
  az login --service-principal \
    -u "$DEPLOY_CLIENT_ID" \
    -p "$DEPLOY_CLIENT_SECRET" \
    --tenant "$EXTERNAL_TENANT_ID" \
    --allow-no-subscriptions >/dev/null
else
  echo "No DEPLOY_CLIENT_SECRET set - assuming an OIDC session (azure/login) is active."
fi

echo "Compiling template (restores the Graph extension if needed)..."
az bicep build --file "$TEMPLATE"

echo "Deploying at TENANT scope..."
az deployment tenant create \
  --name "doseup-appreg-$(date +%Y%m%d%H%M%S)" \
  --location "$LOCATION" \
  --template-file "$TEMPLATE" \
  --parameters "$PARAMS"

echo "Done. Read outputs (appId, servicePrincipalId, apiScope) from the deployment result above."
