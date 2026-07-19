using DoseUp.Api.Modules.Membership.Domain;
using DoseUp.Api.SharedKernel.Results;
using DoseUp.Api.SharedKernel.Rules;
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
  public void Blank_display_name_or_email_is_a_bug_not_a_rule_violation() {
    EntraObjectId oid = EntraObjectId.From(Guid.CreateVersion7());

    Should.Throw<ArgumentException>(() => Account.SignUp(oid, " ", "jakub@example.test", Now));
    Should.Throw<ArgumentException>(() => Account.SignUp(oid, "Jakub", " ", Now));
    Should.Throw<ArgumentNullException>(() => Account.SignUp(oid, null!, "jakub@example.test", Now));
    Should.Throw<ArgumentNullException>(() => Account.SignUp(oid, "Jakub", null!, Now));
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
