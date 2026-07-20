using Ardalis.SmartEnum;

namespace DoseUp.Api.Modules.Membership.Domain;

/// <summary>
/// The account's lifecycle status (conventions § Domain enumerations). Values are
/// persisted ints: explicit, append-only, never renumbered, never reused — a future
/// admission state (vision OQ-5) is appended, never spliced in.
/// </summary>
public sealed class AccountStatus : SmartEnum<AccountStatus> {
  public static readonly AccountStatus Active = new(nameof(Active), 1);
  public static readonly AccountStatus Disabled = new(nameof(Disabled), 2);

  private AccountStatus(string name, int value) : base(name, value) { }
}
