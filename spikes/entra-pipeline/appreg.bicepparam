// ============================================================================
// Spike #62 - parameter values for appreg.bicep.
// Placeholder redirect URIs (doseup.example) and repo coordinates - swap the
// hosts for the real Azure Container Apps / custom-domain URLs on the real run.
// ============================================================================
using './appreg.bicep'

param appDisplayName = 'DoseUp Platform'
param appUniqueName = 'doseup-platform' // immutable upsert key - do not change

// CIAM/External ID customer app => single-tenant to the External ID tenant.
param signInAudience = 'AzureADMyOrg'

// React PWA (SPA platform, auth-code + PKCE).
param spaRedirectUris = [
  'https://doseup.example/authentication/login-callback'
  'http://localhost:5173/authentication/login-callback' // local Vite dev
]

// Confidential/server signin-oidc redirect(s).
param webRedirectUris = [
  'https://doseup.example/signin-oidc'
]

// API this app exposes + its one delegated scope.
param apiIdentifierUri = 'api://doseup-platform'
param apiScopeName = 'access_as_user'

// GitHub OIDC federation target: this repo, Production environment.
param githubRepo = 'jakubbohm/DoseUp'
param githubEnvironment = 'Production'
