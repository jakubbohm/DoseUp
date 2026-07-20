# error-contract Specification (delta)

> Delta for [c002](../../proposal.md): rename-only (#98) — the edge union `Result` becomes `ApiResult`; the taxonomy, statuses, ProblemDetails shapes, and the 409 contract are behaviorally unchanged.

## RENAMED Requirements

- FROM: `### Requirement: The Platform mapper implements the Result-case → status matrix`
- TO: `### Requirement: The Platform mapper implements the ApiResult-case → status matrix`

## MODIFIED Requirements

### Requirement: The Platform mapper implements the ApiResult-case → status matrix

The single Platform mapper SHALL map `ApiResult` cases to statuses exactly per the conventions matrix: `Validation` → 400, `NotFound` → 404, `RuleViolations` → 409 (rule-violation ProblemDetails `type`), `Conflict` → 409 (distinct ProblemDetails `type`), `Forbidden` → 403, `Unexpected` → 500.

#### Scenario: NotFound maps to 404
- **WHEN** the mapper receives a `NotFound` result
- **THEN** it produces a 404 ProblemDetails response

#### Scenario: The two 409 classes stay distinguishable
- **WHEN** the mapper receives a `RuleViolations` result and a `Conflict` result
- **THEN** both map to status 409 but carry different ProblemDetails `type` URIs
