using DoseUp.Api.SharedKernel.Domain;

namespace DoseUp.Api.Modules.Membership.Domain;

/// <summary>The Account aggregate's typed id (conventions § Domain model base types).</summary>
public readonly record struct AccountId(Guid Value) : ITypedId<AccountId> {
  public static AccountId Create() => new(Guid.CreateVersion7());

  public static AccountId From(Guid value) => new(value);
}
