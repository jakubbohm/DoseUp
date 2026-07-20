# error-contract Specification

## Purpose

The wire error model (domain-rules.md §1/§7 taxonomy, RFC 9457). One owner per error class; the single Platform mapper is the only producer of ApiResult-derived error responses. (The 403 request-class shape activates with M0's `ActiveAccount` and is specced with that change.)

## Requirements

### Requirement: Every non-2xx response is ProblemDetails

Every non-2xx response the API produces — including auth-middleware denials and unhandled-exception responses — SHALL be an RFC 9457 ProblemDetails body with the `application/problem+json` content type. Endpoints SHALL NOT hand-craft error responses.

#### Scenario: Unauthenticated requests get a ProblemDetails body
- **WHEN** a protected endpoint is called without a valid token
- **THEN** the 401 response body is ProblemDetails with `application/problem+json`

### Requirement: The Platform mapper implements the ApiResult-case → status matrix

The single Platform mapper SHALL map `ApiResult` cases to statuses exactly per the conventions matrix: `Validation` → 400, `NotFound` → 404, `RuleViolations` → 409 (rule-violation ProblemDetails `type`), `Conflict` → 409 (distinct ProblemDetails `type`), `Forbidden` → 403, `Unexpected` → 500.

#### Scenario: NotFound maps to 404
- **WHEN** the mapper receives a `NotFound` result
- **THEN** it produces a 404 ProblemDetails response

#### Scenario: The two 409 classes stay distinguishable
- **WHEN** the mapper receives a `RuleViolations` result and a `Conflict` result
- **THEN** both map to status 409 but carry different ProblemDetails `type` URIs

### Requirement: Rule violations ride one 409 ProblemDetails with a violations array

The 409 rule-violation response SHALL be a single ProblemDetails object with `type` `https://doseup.app/problems/rule-violation` and an extension member `violations` — an array of `{ code, message }` — carrying every violation. Codes are stable `<aggregate>.<rule>` kebab-case contract values; messages are static rule texts containing no user or dose data (NFR-5).

#### Scenario: All violations arrive in one response
- **WHEN** a `RuleViolations` result carrying two violations is mapped
- **THEN** the single 409 body's `violations` array carries both `{ code, message }` pairs

### Requirement: Validation failures map to 400 with per-field errors

The `Validation` case SHALL map to 400 with the field errors carried in the ProblemDetails extension member keyed per field (the ASP.NET `ValidationProblemDetails`-style shape), reporting all fields at once.

#### Scenario: Every invalid field is reported
- **WHEN** a `Validation` result carrying errors on two fields is mapped
- **THEN** the 400 body carries both fields' messages

### Requirement: Unexpected failures never leak internals

Unhandled exceptions and the `Unexpected` case SHALL map to a 500 ProblemDetails carrying no exception type, message, stack trace, or other internals.

#### Scenario: Exception details stay server-side
- **WHEN** an unhandled exception escapes a request
- **THEN** the 500 ProblemDetails body contains no exception message or stack information
