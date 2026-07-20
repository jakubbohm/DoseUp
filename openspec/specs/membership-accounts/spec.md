# membership-accounts Specification

## Purpose

The Account aggregate's behavioral contract and its persisted schema. Domain-only as of c002 — no HTTP surface exists yet, so rule denials are specced at the domain layer as stable violation codes in `DomainResult`; their 409 wire shape is error-contract's existing requirement and activates when the first slice exposes it (#54's change).

## Requirements

### Requirement: Signing up creates an active account

`Account.SignUp` SHALL create an account carrying the caller-supplied Entra identity link, display name, email, and creation instant (`DateTimeOffset now` — the domain never reads a clock), with a client-generated version-7 id, in status `Active`. `Active` on creation is the recorded default while admission gating (vision OQ-5) remains open — a later admission decision adds states, it does not renumber or repurpose these.

#### Scenario: A new account lands active with its data
- **WHEN** an account is created via `SignUp` with an identity link, display name, email, and a supplied `now`
- **THEN** the account is `Active`, carries exactly those values, and its creation instant equals the supplied `now`

#### Scenario: The account is born with a v7 identity
- **WHEN** an account is created via `SignUp`
- **THEN** its id is already set (no save required) and the underlying Guid reports version 7

### Requirement: Display name and email are mandatory

Display name and email SHALL be mandatory: `SignUp` receiving a null or whitespace value SHALL throw (bug-class guard — by the time the domain is called, contract validation has enforced user-facing shape; a blank reaching the domain is a programming error, not an expected failure).

#### Scenario: Blank descriptive data is a bug, not a rule violation
- **WHEN** `SignUp` is called with a whitespace display name or email
- **THEN** construction throws and no account is created

### Requirement: A missing status on an affordance is a bug

The static affordances SHALL guard their status argument: a null `AccountStatus` SHALL throw (bug-class guard — the enumeration's null-safe equality would otherwise misreport a programming error as a plausible rule refusal, surfacing to the user as a normal-looking 409).

#### Scenario: Null status is a bug, not a rule refusal
- **WHEN** `CheckCanDisable` or `CheckCanReactivate` is evaluated with a null status
- **THEN** the check throws and no rule outcome is produced

### Requirement: Only an active account can be disabled

The account SHALL protect disabling with a pure rule: permitted only in status `Active`, refused otherwise with the stable violation code `account.not-active` (static message, no user data — NFR-5). `Disable` SHALL re-assert the rule itself (two-layer checking, domain-rules.md §3) and SHALL NOT change state on refusal.

#### Scenario: Disabling an active account succeeds
- **WHEN** `Disable` is called on an `Active` account
- **THEN** the outcome is success and the account's status is `Disabled`

#### Scenario: Disabling a disabled account is refused with its code
- **WHEN** `Disable` is called on a `Disabled` account
- **THEN** the outcome carries exactly the violation `account.not-active` and the status remains `Disabled`

#### Scenario: The affordance is a pure function of status
- **WHEN** `CheckCanDisable` is evaluated for a status value alone (no account instance)
- **THEN** it passes for `Active` and fails with `account.not-active` for `Disabled`

### Requirement: Only a disabled account can be reactivated

The account SHALL protect reactivating with a pure rule: permitted only in status `Disabled`, refused otherwise with the stable violation code `account.not-disabled` (static message, no user data — NFR-5). `Reactivate` SHALL re-assert the rule itself and SHALL NOT change state on refusal.

#### Scenario: Reactivating a disabled account succeeds
- **WHEN** `Reactivate` is called on a `Disabled` account
- **THEN** the outcome is success and the account's status is `Active`

#### Scenario: Reactivating an active account is refused with its code
- **WHEN** `Reactivate` is called on an `Active` account
- **THEN** the outcome carries exactly the violation `account.not-disabled` and the status remains `Active`

### Requirement: One account per Entra identity, guaranteed by the schema

The Entra identity link SHALL be unique across accounts, guaranteed by a database uniqueness constraint (the set-rule backstop, domain-rules.md §6 — advisory checks belong to the future signup slice; the constraint is the guarantee). The constraint's identifier SHALL be snake_case and deterministic so violation mapping can cite it by name.

#### Scenario: A duplicate identity link is rejected by the database
- **WHEN** a second account row carrying an already-stored Entra identity link is inserted
- **THEN** the database rejects it via the uniqueness constraint

### Requirement: Membership owns its schema

Account state SHALL persist in the module-owned `membership` schema with every database identifier snake_case (conventions § Persistence, F-88) and the module's own migrations-history table inside that schema. Migrations SHALL apply through the migration runner before the API serves requests — never at API startup.

#### Scenario: The migrated schema is module-owned and snake_case
- **WHEN** migrations are applied to a fresh database
- **THEN** the `membership` schema contains the accounts table and the module's own migrations-history table, and every generated identifier is snake_case (unquoted-citable)

#### Scenario: Status persists as its stable numeric value
- **WHEN** an account is stored and reloaded
- **THEN** its status round-trips via the stable append-only numeric value (`Active = 1`, `Disabled = 2`)
