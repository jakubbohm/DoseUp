# api-shell — Delta Spec

The minimal API surface this change stands up: secure-by-default posture (PRE-10 ring 0), a DB-free anonymous health probe, and the authenticated identity-echo diagnostic (M0's "response proves identity" endpoint minus the database stage — M0 layers `ActiveAccount`/`CallerContext` on top). The anonymous allowlist's enforcement owner is the architecture-test catalog (rule 12), not this spec.

## ADDED Requirements

### Requirement: Endpoints are secure by default

Every endpoint not explicitly allowlisted as anonymous SHALL require a valid bearer token from a trusted authority; requests failing that SHALL be denied with 401 (ProblemDetails per `error-contract`). Token validation SHALL be local — the unauthenticated path never touches the database (PRE-10).

#### Scenario: Missing token is denied
- **WHEN** the identity-echo endpoint is called with no token
- **THEN** the response is 401

#### Scenario: Untrusted-authority token is denied
- **WHEN** the identity-echo endpoint is called with a token signed by a key no configured authority trusts
- **THEN** the response is 401

### Requirement: The health probe is anonymous and database-free

The API SHALL expose a health probe reachable without authentication that performs no database access — probe traffic (including bot-triggered wakes) never reaches the database (ADR-0001; NFR-6).

#### Scenario: Anonymous probe succeeds
- **WHEN** the health probe is called without a token
- **THEN** the response is 2xx

#### Scenario: Probing executes no database command
- **WHEN** the health probe is called
- **THEN** no database command is executed on its behalf

### Requirement: The identity-echo diagnostic proves the token pipeline

The API SHALL expose an authenticated diagnostic endpoint that returns the caller's token identity — at minimum the `oid` claim value — read purely from the validated token, with no database access.

#### Scenario: A valid token is echoed
- **WHEN** the identity-echo endpoint is called with a valid token carrying an `oid` claim
- **THEN** the response is 200 and echoes that `oid` value
