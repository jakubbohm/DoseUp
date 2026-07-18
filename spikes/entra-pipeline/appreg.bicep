// ============================================================================
// DoseUp - Spike #62: Entra app registration as code (Microsoft Graph Bicep)
// ----------------------------------------------------------------------------
// QUESTION: Can the DoseUp sign-in app registration be created/updated from the
//           deployment pipeline instead of clicking around in the portal?
//
// ANSWER (this file is the proof): YES for the app registration + service
//           principal + federated credential objects, via the GA Microsoft
//           Graph Bicep extension - declaratively, idempotently, from CI.
//
// THE CIAM CRUX (why the spike verdict is *CONDITIONAL*):
//   DoseUp's identity is Microsoft Entra External ID (CIAM). An External ID
//   tenant has NO Azure subscription, so this template can contain ONLY
//   Microsoft.Graph/* resources and MUST deploy at *tenant* scope:
//       az deployment tenant create --location <loc> --template-file appreg.bicep
//   It therefore CANNOT be co-deployed with the Azure infra Bicep (that lives in
//   the Azure subscription / home tenant). Two deployments, two tenants:
//       (1) Azure infra   -> subscription scope, home tenant   (infra/*.bicep)
//       (2) this file      -> tenant scope,       External ID tenant
//
// WHAT IS *NOT* EXPRESSIBLE HERE (still portal / Graph-beta / EAF territory):
//   user flows (authenticationEventsFlows), external identity providers,
//   company branding, custom domains. v1.0 Graph Bicep covers app registration,
//   service principal, federated identity credentials, and groups - nothing
//   more of the CIAM surface. "App registration as code" != "whole CIAM tenant
//   as code".
// ============================================================================

// tenant scope: an External ID / CIAM tenant has no subscription to target.
targetScope = 'tenant'

// Pulls in the GA Microsoft Graph provider. The alias `microsoftGraphV1` is
// defined in bicepconfig.json (same directory). No experimental flag post-GA.
extension microsoftGraphV1

// ---------------------------------------------------------------------------
// Parameters - defaulted so the file compiles standalone (offline proof).
// Real values are supplied by appreg.bicepparam.
// ---------------------------------------------------------------------------

@description('Human-readable name shown in the portal / consent screen.')
param appDisplayName string = 'DoseUp Platform'

@description('''Immutable alternate key. THIS is the idempotency/upsert key:
re-deploying with the same uniqueName UPDATES the existing app instead of
creating a duplicate. Cannot be changed after creation.''')
param appUniqueName string = 'doseup-platform'

@description('''Who can sign in. CIAM/External ID customer apps are single-tenant
to the External ID tenant, so AzureADMyOrg is the correct default here (NOT
AzureADandPersonalMicrosoftAccount, which is a workforce/consumer pattern).''')
@allowed([
  'AzureADMyOrg'
  'AzureADMultipleOrgs'
  'AzureADandPersonalMicrosoftAccount'
])
param signInAudience string = 'AzureADMyOrg'

@description('SPA redirect URI(s) for the React PWA (auth-code + PKCE flow).')
param spaRedirectUris array = [
  'https://doseup.example/authentication/login-callback'
]

@description('Web redirect URI(s) for the confidential/server signin-oidc flow.')
param webRedirectUris array = [
  'https://doseup.example/signin-oidc'
]

@description('''App ID URI base for the API this app exposes. Convention
api://<appUniqueName-or-guid>. Kept as a param so it can be swapped for a
verified custom domain URI later.''')
param apiIdentifierUri string = 'api://doseup-platform'

@description('The delegated scope name the SPA requests to call the API.')
param apiScopeName string = 'access_as_user'

// GitHub OIDC federation coordinates (see federatedIdentityCredentials below).
@description('owner/repo of the GitHub repository allowed to federate in.')
param githubRepo string = 'jakubbohm/DoseUp'

@description('GitHub environment gating the federated token (repo:...:environment:<env>).')
param githubEnvironment string = 'Production'

// ---------------------------------------------------------------------------
// Stable GUID for the exposed OAuth2 scope. Derived deterministically from the
// app's uniqueName + scope name so it is IDENTICAL on every deploy -> the scope
// is updated in place, never duplicated. (We cannot reference app.id here - it
// would be a self-reference - so we seed from the immutable uniqueName.)
// ---------------------------------------------------------------------------
var apiScopeId = guid(appUniqueName, apiScopeName)

// Well-known Microsoft Graph resource + its User.Read delegated scope id.
// Requesting a realistic minimal permission proves the requiredResourceAccess
// shape (this is what the portal's "API permissions" blade edits).
var microsoftGraphAppId = '00000003-0000-0000-c000-000000000000'
var graphUserReadScopeId = 'e1fe6dd8-ba31-4d61-b1c0-6c5df6a4e0c4'

// ===========================================================================
// (1) THE APP REGISTRATION
// Proves: displayName + immutable uniqueName upsert key, signInAudience,
//         SPA + web redirect URIs, an exposed API scope, and requested
//         downstream (Graph) permissions - all declaratively.
// ===========================================================================
resource app 'Microsoft.Graph/applications@v1.0' = {
  displayName: appDisplayName
  uniqueName: appUniqueName // REQUIRED + immutable = the idempotency key
  signInAudience: signInAudience

  // The React PWA: SPA platform => auth-code flow with PKCE, no client secret.
  spa: {
    redirectUris: spaRedirectUris
  }

  // Confidential/server-side sign-in (ASP.NET-style signin-oidc), if used.
  web: {
    redirectUris: webRedirectUris
  }

  // App ID URI(s) under which this app exposes its API scopes.
  identifierUris: [
    apiIdentifierUri
  ]

  // "Expose an API" blade as code: one delegated scope the SPA asks for to call
  // the DoseUp API on the signed-in user's behalf.
  api: {
    oauth2PermissionScopes: [
      {
        id: apiScopeId
        value: apiScopeName
        // type 'User' => users can consent; 'Admin' => admin-only consent.
        type: 'User'
        isEnabled: true
        adminConsentDisplayName: 'Access DoseUp as the signed-in user'
        adminConsentDescription: 'Allows the app to call the DoseUp API on behalf of the signed-in user.'
        userConsentDisplayName: 'Access DoseUp on your behalf'
        userConsentDescription: 'Allows the app to call the DoseUp API on your behalf.'
      }
    ]
  }

  // "API permissions" blade as code: request Microsoft Graph User.Read
  // (delegated). NOTE: declaring the permission != granting admin consent -
  // consent is still a separate one-time human/Graph step.
  requiredResourceAccess: [
    {
      resourceAppId: microsoftGraphAppId
      resourceAccess: [
        {
          id: graphUserReadScopeId
          type: 'Scope' // 'Scope' = delegated; 'Role' = application permission
        }
      ]
    }
  ]
}

// ===========================================================================
// (2) THE SERVICE PRINCIPAL
// The app registration is the global definition; the service principal is its
// instantiation in THIS tenant (what actually enables sign-in / token issuance
// and what role assignments bind to). One-liner because it just references the
// app's generated appId.
// ===========================================================================
resource sp 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: app.appId
}

// ===========================================================================
// (3) FEDERATED IDENTITY CREDENTIAL (GitHub OIDC)
// Proves the *keyless* pipeline story: GitHub Actions presents an OIDC token,
// Entra trusts it via this FIC - no client secret to store or rotate.
//
// CIAM CAVEAT: GitHub OIDC federation INTO an External ID tenant is validated
// for workforce tenants but UNCONFIRMED for CIAM - this compiles fine, but the
// runtime trust must be proven on the real tenant (see README). Fallback is a
// client secret/cert on an SP created directly in the External ID tenant.
//
// Child-resource naming uses the documented Graph pattern
// '<parentUniqueName>/<ficName>' - the FIC's own `name` is its immutable
// alt-key within the app.
// ===========================================================================
resource githubOidcFic 'Microsoft.Graph/applications/federatedIdentityCredentials@v1.0' = {
  name: '${app.uniqueName}/github-actions-${toLower(githubEnvironment)}'
  // subject MUST match the exact claim GitHub mints for this repo+environment.
  subject: 'repo:${githubRepo}:environment:${githubEnvironment}'
  issuer: 'https://token.actions.githubusercontent.com'
  audiences: [
    'api://AzureADTokenExchange'
  ]
  description: 'GitHub Actions OIDC for ${githubRepo} (${githubEnvironment} environment)'
}

// ---------------------------------------------------------------------------
// Outputs - what the rest of the pipeline consumes (e.g. wire appId into the
// SPA config / API audience validation / the Azure infra deployment).
// ---------------------------------------------------------------------------
@description('Client (application) id - goes into the PWA MSAL config & API audience.')
output appId string = app.appId

@description('Directory object id of the application registration.')
output applicationObjectId string = app.id

@description('Object id of the service principal (target of role assignments).')
output servicePrincipalId string = sp.id

@description('Fully-qualified scope the SPA requests, e.g. api://doseup-platform/access_as_user.')
output apiScope string = '${apiIdentifierUri}/${apiScopeName}'
