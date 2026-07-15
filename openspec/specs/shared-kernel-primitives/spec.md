# shared-kernel-primitives Specification

## Purpose

Behavioral contracts of the SharedKernel founding seed (PRE-7). Signatures are design-level (member names may be tuned per [domain-rules.md](../../../docs/conventions/domain-rules.md) header); the behaviors below are binding. These primitives are load-bearing for every future module — the unit layer covers them exhaustively (testing.md §2).

## Requirements

### Requirement: Result is a closed union covering the expected-failure taxonomy

The SharedKernel SHALL provide a closed `Result` union whose cases cover exactly the error classes the taxonomy assigns to it (domain-rules.md §1): `Success`, `Validation`, `NotFound`, `RuleViolations`, `Conflict`, `Forbidden`, and `Unexpected`. Expected failures SHALL travel as `Result` values end-to-end; exceptions are reserved for bugs and infrastructure failures.

#### Scenario: Cases are distinguishable by pattern matching
- **WHEN** code receives a `Result` value
- **THEN** each case is distinguishable by pattern matching, and `Success` is distinguishable from every failure case

#### Scenario: Failure payloads survive the union
- **WHEN** a `RuleViolations` result is constructed from failed rule checks
- **THEN** it carries every violation (code and message) without loss or reordering

### Requirement: Validation failures aggregate all field errors

The `Validation` case SHALL aggregate contract-validation failures per field — all of them, never only the first — so a single 400 response can report every shape problem at once.

#### Scenario: Multiple field errors are all carried
- **WHEN** validation produces errors on two different fields
- **THEN** the `Validation` result carries both field entries with their messages

### Requirement: RuleCheck expresses one rule outcome

`RuleCheck` SHALL be a closed union of `Pass` and `Fail`, where `Fail` carries one or more `RuleViolation(Code, Message)` values; a single failed rule is a one-element `Fail`. Codes follow `<aggregate>.<rule>` kebab-case and messages are static rule texts (never interpolating user or dose data — NFR-5).

#### Scenario: A failing rule names itself
- **WHEN** a rule check fails
- **THEN** the outcome is `Fail` carrying exactly that rule's stable code and its static message

#### Scenario: A passing rule carries nothing
- **WHEN** a rule check passes
- **THEN** the outcome is `Pass` and no violations are observable

### Requirement: RuleSet aggregates violations within a stage

`RuleSet` SHALL evaluate all checks belonging to one stage even when earlier checks in that stage fail, and SHALL aggregate their violations in registration order — the user gets every violation of the stage in a single outcome.

#### Scenario: Two failures in one stage both surface
- **WHEN** two pure checks in the same stage both fail
- **THEN** the composed outcome is a failure carrying both violations, in the order the checks were added

### Requirement: RuleSet stages gate later stages and defer async work

A later stage SHALL run only when all previous stages passed. Async checks SHALL NOT begin executing before the set is checked, and SHALL be awaited strictly sequentially in registration order — never in parallel (`DbContext` is not thread-safe; domain-rules.md §5).

#### Scenario: A failed stage stops the pipeline
- **WHEN** a first-stage check fails and a second-stage async check is registered
- **THEN** the async check never executes and the outcome carries only the first stage's violations

#### Scenario: Async checks run sequentially
- **WHEN** multiple async checks are registered in one evaluation
- **THEN** each check begins only after the previous one completed

#### Scenario: Nothing runs before check time
- **WHEN** async checks are added to a `RuleSet` that is never checked
- **THEN** none of them execute

### Requirement: Failed rule outcomes convert to Result

A failed rule outcome SHALL convert to the `Result` `RuleViolations` case carrying the same violations unchanged, so handlers return one channel to the edge.

#### Scenario: Conversion is lossless
- **WHEN** a `Fail` carrying two violations is converted to a `Result`
- **THEN** the result is `RuleViolations` with identical codes and messages in the same order

### Requirement: Entity equality is identity-based

`Entity<TId>` SHALL implement equality by concrete type + id: two entity references are equal iff they are the same entity type and carry the same id. Entities are born with their identity (client-generated ids), so there is no transient-id state.

#### Scenario: Same type and id are equal
- **WHEN** two entity instances of the same type carry the same id
- **THEN** they are equal (and hash equal)

#### Scenario: Different ids are not equal
- **WHEN** two entity instances of the same type carry different ids
- **THEN** they are not equal

#### Scenario: Different types are never equal
- **WHEN** two entities of different types carry ids wrapping the same underlying value
- **THEN** they are not equal

### Requirement: AggregateRoot collects domain events for exactly-once drain

`AggregateRoot<TId>` SHALL implement the non-generic `IAggregateRoot` marker, SHALL collect events raised via its protected raise operation in order, and SHALL expose a drain that yields each raised event exactly once (a second drain yields nothing new).

#### Scenario: Raised events preserve order
- **WHEN** an aggregate raises event A then event B
- **THEN** the drained sequence is exactly A, B

#### Scenario: Draining consumes
- **WHEN** an aggregate's events are drained twice with no raises in between
- **THEN** the second drain yields no events

### Requirement: Typed ids wrap Guid v7 and persist as native uuid

Each aggregate's id SHALL be a distinct readonly value type wrapping a `Guid` (compile-time non-interchangeable across aggregates). New id generation SHALL produce Guid **version 7** (time-ordered). The single generic value converter SHALL map any typed id to provider type `Guid` (native `uuid`, never a string) and round-trip losslessly.

#### Scenario: Generated ids are v7
- **WHEN** a new typed id is generated
- **THEN** the underlying Guid reports version 7

#### Scenario: Converter round-trips losslessly to Guid
- **WHEN** a typed id passes through the generic converter to its provider value and back
- **THEN** the provider value is a `Guid` equal to the wrapped value, and the restored typed id equals the original

### Requirement: The domain-event dispatcher drains to quiescence, depth-guarded

The dispatcher SHALL invoke every registered handler for each event's type, synchronously. Events raised by handlers during dispatch SHALL themselves be dispatched (loop until no new events). A non-terminating cascade SHALL fail fast via a depth guard — a thrown exception, since a runaway cascade is a bug, not an expected failure. An event with no registered handlers dispatches to nobody and is not an error.

#### Scenario: All handlers of an event run
- **WHEN** an event type has two registered handlers and one event is dispatched
- **THEN** both handlers are invoked with that event

#### Scenario: Handler-raised events are dispatched too
- **WHEN** a handler raises a follow-up event during dispatch
- **THEN** the follow-up event's handlers are invoked before dispatch completes

#### Scenario: Runaway cascades hit the depth guard
- **WHEN** handlers keep raising events past the depth limit
- **THEN** dispatch throws instead of looping forever

#### Scenario: Unhandled events are a no-op
- **WHEN** an event with no registered handlers is dispatched
- **THEN** dispatch completes without error

### Requirement: The integration-event publisher port is transport-free

The SharedKernel SHALL expose an `IIntegrationEventPublisher` port whose signature references no transport, framework, or Platform types, accepting a contract event payload for publication. (Its production implementation over the Wolverine outbox is out of scope here; per-module translators are the only intended callers — enforcement owner: architecture-test catalog rule 9.)

#### Scenario: Publications reach the registered implementation unchanged
- **WHEN** a caller publishes an event through the port against a registered (test) implementation
- **THEN** the implementation receives exactly that event instance
