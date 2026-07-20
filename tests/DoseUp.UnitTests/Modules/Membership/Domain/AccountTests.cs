using DoseUp.Api.Modules.Membership.Domain;
using DoseUp.Api.SharedKernel.Domain;
using Shouldly;

namespace DoseUp.UnitTests.Modules.Membership.Domain;

public sealed class AccountTests {
  private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.FromHours(2));

  private static Account SignUp() => Account.SignUp(EntraObjectId.From(Guid.CreateVersion7()), "Jakub", "jakub@example.test", Now);

  [Test]
  public void A_new_account_lands_active_with_its_data() {
    EntraObjectId oid = EntraObjectId.From(Guid.CreateVersion7());

    Account account = Account.SignUp(oid, "Jakub", "jakub@example.test", Now);

    account.Status.ShouldBe(AccountStatus.Active);
    account.EntraObjectId.ShouldBe(oid);
    account.DisplayName.ShouldBe("Jakub");
    account.Email.ShouldBe("jakub@example.test");
    account.CreatedAt.ShouldBe(Now);
  }

  [Test]
  public void The_account_is_born_with_a_version_7_identity() {
    Account account = SignUp();

    account.Id.Value.ShouldNotBe(Guid.Empty);
    account.Id.Value.Version.ShouldBe(7);
  }

  [Test]
  public void Whitespace_display_name_is_a_bug_and_throws() =>
    Should.Throw<ArgumentException>(static () => Account.SignUp(EntraObjectId.From(Guid.CreateVersion7()), " ", "jakub@example.test", Now));

  [Test]
  public void Whitespace_email_is_a_bug_and_throws() =>
    Should.Throw<ArgumentException>(static () => Account.SignUp(EntraObjectId.From(Guid.CreateVersion7()), "Jakub", " ", Now));

  [Test]
  public void Missing_display_name_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => Account.SignUp(EntraObjectId.From(Guid.CreateVersion7()), null!, "jakub@example.test", Now));

  [Test]
  public void Missing_email_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => Account.SignUp(EntraObjectId.From(Guid.CreateVersion7()), "Jakub", null!, Now));

  [Test]
  public void Status_values_are_the_stable_persistence_contract() {
    // membership-accounts spec: status round-trips via the stable append-only numeric
    // value — pinned here so a renumbering fails a test, not a database.
    AccountStatus.Active.Value.ShouldBe(1);
    AccountStatus.Disabled.Value.ShouldBe(2);
  }

  [Test]
  public void Disabling_an_active_account_succeeds() {
    Account account = SignUp();

    DomainResult result = account.Disable();

    result.Value.ShouldBeOfType<DomainResult.Success>();
    account.Status.ShouldBe(AccountStatus.Disabled);
  }

  [Test]
  public void Disabling_a_disabled_account_is_refused_with_its_code_and_changes_nothing() {
    Account account = SignUp();
    account.Disable();

    DomainResult result = account.Disable();

    RuleViolation violation = result.Value.ShouldBeOfType<DomainResult.RuleViolations>().Violations.ShouldHaveSingleItem();
    violation.Code.ShouldBe("account.not-active");
    violation.Message.ShouldBe("Only an active account can be disabled.");
    account.Status.ShouldBe(AccountStatus.Disabled);
  }

  [Test]
  public void The_disable_affordance_is_a_pure_function_of_status() {
    Account.CheckCanDisable(AccountStatus.Active).Value.ShouldBeOfType<RuleCheck.Pass>();

    RuleCheck.Fail fail = Account.CheckCanDisable(AccountStatus.Disabled).Value.ShouldBeOfType<RuleCheck.Fail>();
    fail.Violations.ShouldHaveSingleItem().Code.ShouldBe("account.not-active");
  }

  [Test]
  public void Missing_status_on_the_disable_affordance_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => Account.CheckCanDisable(null!));

  [Test]
  public void Missing_status_on_the_reactivate_affordance_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => Account.CheckCanReactivate(null!));

  [Test]
  public void Reactivating_a_disabled_account_succeeds() {
    Account account = SignUp();
    account.Disable();

    DomainResult result = account.Reactivate();

    result.Value.ShouldBeOfType<DomainResult.Success>();
    account.Status.ShouldBe(AccountStatus.Active);
  }

  [Test]
  public void Reactivating_an_active_account_is_refused_with_its_code_and_changes_nothing() {
    Account account = SignUp();

    DomainResult result = account.Reactivate();

    RuleViolation violation = result.Value.ShouldBeOfType<DomainResult.RuleViolations>().Violations.ShouldHaveSingleItem();
    violation.Code.ShouldBe("account.not-disabled");
    violation.Message.ShouldBe("Only a disabled account can be reactivated.");
    account.Status.ShouldBe(AccountStatus.Active);
  }

  [Test]
  public void The_reactivate_affordance_is_a_pure_function_of_status() {
    Account.CheckCanReactivate(AccountStatus.Disabled).Value.ShouldBeOfType<RuleCheck.Pass>();

    RuleCheck.Fail fail = Account.CheckCanReactivate(AccountStatus.Active).Value.ShouldBeOfType<RuleCheck.Fail>();
    fail.Violations.ShouldHaveSingleItem().Code.ShouldBe("account.not-disabled");
  }

  [Test]
  public void Instance_affordances_delegate_to_the_static_rule() {
    Account account = SignUp();

    account.CheckCanDisable().Value.ShouldBeOfType<RuleCheck.Pass>();
    account.CheckCanReactivate().Value.ShouldBeOfType<RuleCheck.Fail>();
  }
}
