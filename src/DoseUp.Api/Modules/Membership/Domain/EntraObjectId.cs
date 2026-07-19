using DoseUp.Api.SharedKernel.Domain;

namespace DoseUp.Api.Modules.Membership.Domain;

/// <summary>
/// The externally-issued identity reference — Entra's <c>oid</c> claim, the ring-1
/// lookup key (ADR-0002 § Authorization). Typed so it can never be swapped with an
/// <see cref="AccountId"/> at compile time; implements <see cref="ITypedId{TSelf}"/>
/// to ride the one generic uuid converter (c002 design D3). Deliberately no
/// <c>Create()</c> — DoseUp never mints one; Entra does.
/// </summary>
public readonly record struct EntraObjectId(Guid Value) : ITypedId<EntraObjectId> {
  public static EntraObjectId From(Guid value) => new(value);
}
