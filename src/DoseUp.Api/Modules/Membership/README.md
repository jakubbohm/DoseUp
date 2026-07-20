# Membership

The circle: accounts and (from M1) their profiles — the bounded context behind
`ActiveAccount` resolution and the profile-scoped language of ring 2
([ADR-0002-architecture-style](../../../../docs/adr/0002-architecture-style.md)).

**Declared grade (ADR-0002 § Per-module rigor): full domain discipline, small surface.**
The domain is small but not CRUD — the account lifecycle is a real state machine with
self-protecting rules — so aggregates, typed ids, SmartEnum, and the two-layer rule
model apply in full.

This declaration lives in a README because `MembershipModule.cs`'s shape *is* the open
module-registration decision (d18 — [#39](https://github.com/jakubbohm/DoseUp/issues/39));
it migrates there when d18 resolves.
