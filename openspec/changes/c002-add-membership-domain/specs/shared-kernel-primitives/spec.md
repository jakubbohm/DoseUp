# shared-kernel-primitives Specification (delta)

> Delta for [c002](../../proposal.md): ADDED — the `DomainResult` union (the domain half of #38). RENAMED/MODIFIED — the edge union `Result` → `ApiResult` (#98 rename), plus review hardening: every failure payload is construction-guarded and snapshotted.

## ADDED Requirements

### Requirement: DomainResult is the domain layer's result union

The SharedKernel SHALL provide a closed `DomainResult` union with exactly two cases — `Success` and `RuleViolations` (carrying one or more `RuleViolation(Code, Message)` values) — the only two outcomes a rule-guarded domain operation can produce, in two forms: the non-generic `DomainResult` for void-shaped operations, and `DomainResult<T>` whose `Success` carries the operation's value. Domain code SHALL return `DomainResult`, never the edge `ApiResult` union (its request/resource cases are meaningless inside an aggregate; enforcement owner: the architecture-test catalog rule delivered by #97). Bugs still throw; the domain stays synchronous, so no asynchronous variants exist at this layer.

#### Scenario: Cases are distinguishable by pattern matching
- **WHEN** code receives a `DomainResult` value
- **THEN** `Success` and `RuleViolations` are distinguishable by pattern matching, and violations are observable only on `RuleViolations`

#### Scenario: A refused domain operation carries its violations
- **WHEN** a domain operation is refused because a rule check failed
- **THEN** the returned `DomainResult` is `RuleViolations` carrying that rule's stable code and static message

#### Scenario: The value-carrying form yields its value only on success
- **WHEN** code receives a `DomainResult<T>` value
- **THEN** the operation's value is observable exactly when the case is `Success`, and violations exactly when the case is `RuleViolations`

#### Scenario: Violations are snapshotted at construction
- **WHEN** the caller mutates its violations list after constructing any violations-carrying case (`RuleCheck` `Fail`, either `DomainResult` form, `ApiResult` `RuleViolations`)
- **THEN** the constructed value's violations are unchanged

### Requirement: DomainResult converts losslessly at both seams

A `RuleCheck` `Fail` SHALL convert to `DomainResult` `RuleViolations`, and a `DomainResult` (either form) SHALL convert its failure to `ApiResult` `RuleViolations`, each carrying every violation unchanged in order — the aggregate re-asserts in domain vocabulary, the handler maps to the edge in one step, and the wire contract is untouched. (The non-generic `Success` maps to `ApiResult` `Success`; the value-carrying form's `Success` is consumed by pattern matching — the edge union's `Success` carries no payload.) Edge-ward conversions are owned by the edge union (`ApiResult.From` — #99): domain-side types never reference the edge, enforced by the generalized direction rule (arch rule 21).

#### Scenario: Fail converts to DomainResult without loss
- **WHEN** a `Fail` carrying two violations is converted to a `DomainResult`
- **THEN** the result is `RuleViolations` with identical codes and messages in the same order

#### Scenario: DomainResult converts to ApiResult without loss
- **WHEN** a `DomainResult` `RuleViolations` carrying two violations is converted to an `ApiResult`
- **THEN** the result is `ApiResult` `RuleViolations` with identical codes and messages in the same order

#### Scenario: The value-carrying form's failure converts identically
- **WHEN** a `DomainResult<T>` `RuleViolations` carrying two violations is converted to an `ApiResult`
- **THEN** the result is `ApiResult` `RuleViolations` with identical codes and messages in the same order

## RENAMED Requirements

- FROM: `### Requirement: Result is a closed union covering the expected-failure taxonomy`
- TO: `### Requirement: ApiResult is a closed union covering the expected-failure taxonomy`
- FROM: `### Requirement: Failed rule outcomes convert to Result`
- TO: `### Requirement: Failed rule outcomes convert to ApiResult`

## MODIFIED Requirements

### Requirement: ApiResult is a closed union covering the expected-failure taxonomy

The SharedKernel SHALL provide a closed `ApiResult` union (named for the owning layer — the edge; renamed from `Result` by #98) whose cases cover exactly the error classes the taxonomy assigns to it (domain-rules.md §1): `Success`, `Validation`, `NotFound`, `RuleViolations`, `Conflict`, `Forbidden`, and `Unexpected`. Expected failures SHALL travel as `ApiResult` values from handlers to the edge; exceptions are reserved for bugs and infrastructure failures. Failure payloads are construction-guarded — a `RuleViolations` naming no violated rule or a `Validation` naming no field error is a bug and throws — and snapshotted, so later caller mutation cannot alter a constructed result.

#### Scenario: Cases are distinguishable by pattern matching
- **WHEN** code receives an `ApiResult` value
- **THEN** each case is distinguishable by pattern matching, and `Success` is distinguishable from every failure case

#### Scenario: Failure payloads survive the union
- **WHEN** a `RuleViolations` result is constructed from failed rule checks
- **THEN** it carries every violation (code and message) without loss or reordering

#### Scenario: Empty failure payloads are bugs
- **WHEN** a `RuleViolations` is constructed with no violations, or a `Validation` with no field errors or a field carrying a null or empty message array, or either case with a missing payload
- **THEN** construction throws — a failure response that names no cause never leaves the API

### Requirement: Failed rule outcomes convert to ApiResult

A failed rule outcome SHALL convert to the `ApiResult` `RuleViolations` case carrying the same violations unchanged, so handlers return one channel to the edge.

#### Scenario: Conversion is lossless
- **WHEN** a `Fail` carrying two violations is converted to an `ApiResult`
- **THEN** the result is `RuleViolations` with identical codes and messages in the same order
