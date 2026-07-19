using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Results;
using DoseUp.Api.SharedKernel.Rules;

namespace DoseUp.Api.Modules.Membership.Domain;

/// <summary>
/// The member of the circle: one Entra identity, one account row, the root ring-1
/// resolves against (ADR-0002 § Authorization). Lifecycle is <c>Active ⇄ Disabled</c>,
/// self-protected by pure rules (domain-rules.md); authorization capability (the future
/// permission model) is deliberately not modeled here. Raises no domain events —
/// nothing reacts to membership facts yet (ADR-0002 § Events: explicit orchestration
/// is the default).
/// </summary>
public sealed class Account : AggregateRoot<AccountId> {
  private Account(AccountId id, EntraObjectId entraObjectId, string displayName, string email, AccountStatus status, DateTimeOffset createdAt) : base(id) {
    EntraObjectId = entraObjectId;
    DisplayName = displayName;
    Email = email;
    Status = status;
    CreatedAt = createdAt;
  }

  public EntraObjectId EntraObjectId { get; }

  public string DisplayName { get; private set; }

  public string Email { get; private set; }

  public AccountStatus Status { get; private set; }

  public DateTimeOffset CreatedAt { get; }

  /// <summary>
  /// Creates the account for a signed-in identity — called by the complete-signup slice
  /// (FR-1). Blank name/email is a bug: contract validation owns user-facing shape, so
  /// the domain only guards. Rule-free by design: the one set rule (one account per
  /// identity) needs the database and belongs to the handler + unique constraint
  /// (domain-rules.md §6) — an infallible factory returns the aggregate, not a result.
  /// </summary>
  public static Account SignUp(EntraObjectId entraObjectId, string displayName, string email, DateTimeOffset now) {
    ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
    ArgumentException.ThrowIfNullOrWhiteSpace(email);

    // Active is the recorded OQ-5 default: admission gating beyond creation is
    // deliberately open (vision OQ-5); a later decision appends states, never
    // repurposes this landing.
    return new Account(AccountId.Create(), entraObjectId, displayName, email, AccountStatus.Active, now);
  }

  public static RuleCheck CheckCanDisable(AccountStatus status) =>
    status == AccountStatus.Active
      ? new RuleCheck.Pass()
      : new RuleCheck.Fail("account.not-active", "Only an active account can be disabled.");

  public RuleCheck CheckCanDisable() => CheckCanDisable(Status);

  public DomainResult Disable() {
    if (CheckCanDisable() is RuleCheck.Fail fail)
      return fail.ToDomainResult();

    Status = AccountStatus.Disabled;
    return new DomainResult.Success();
  }

  public static RuleCheck CheckCanReactivate(AccountStatus status) =>
    status == AccountStatus.Disabled
      ? new RuleCheck.Pass()
      : new RuleCheck.Fail("account.not-disabled", "Only a disabled account can be reactivated.");

  public RuleCheck CheckCanReactivate() => CheckCanReactivate(Status);

  public DomainResult Reactivate() {
    if (CheckCanReactivate() is RuleCheck.Fail fail)
      return fail.ToDomainResult();

    Status = AccountStatus.Active;
    return new DomainResult.Success();
  }
}
