using System.Runtime.CompilerServices;
using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Results;
using DoseUp.UnitTests.SharedKernel.Results;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Domain;

public sealed class DomainResultTests {
  [Test]
  public void Success_and_rule_violations_are_distinguishable_by_pattern_matching() {
    DomainResult success = new DomainResult.Success();
    DomainResult violated = new DomainResult.RuleViolations("account.not-active", "Only an active account can be disabled.");

    (success is DomainResult.Success).ShouldBeTrue();
    (success is DomainResult.RuleViolations).ShouldBeFalse();
    (violated is DomainResult.RuleViolations).ShouldBeTrue();
    (violated is DomainResult.Success).ShouldBeFalse();
  }

  [Test]
  public void Violations_are_observable_only_on_the_rule_violations_case() {
    RuleViolation violation = new("account.not-active", "Only an active account can be disabled.");

    DomainResult result = new DomainResult.RuleViolations([violation]);

    DomainResult.RuleViolations violated = result.Value.ShouldBeOfType<DomainResult.RuleViolations>();
    violated.Violations.ShouldBe([violation]);
  }

  [Test]
  public void The_value_carrying_form_yields_its_value_exactly_on_success() {
    DomainResult<string> success = new DomainResult<string>.Success("the value");
    DomainResult<string> violated = new DomainResult<string>.RuleViolations("x.y", "Z.");

    success.Value.ShouldBeOfType<DomainResult<string>.Success>().Value.ShouldBe("the value");
    (violated is DomainResult<string>.Success).ShouldBeFalse();
    violated.Value.ShouldBeOfType<DomainResult<string>.RuleViolations>().Violations.Count.ShouldBe(1);
  }

  [Test]
  public void A_failed_rule_check_converts_to_domain_result_without_loss() {
    RuleViolation first = new("account.not-active", "Only an active account can be disabled.");
    RuleViolation second = new("account.other", "Another rule text.");
    RuleCheck.Fail fail = new([first, second]);

    DomainResult result = fail.ToDomainResult();

    result.Value.ShouldBeOfType<DomainResult.RuleViolations>().Violations.ShouldBe([first, second]);
  }

  [Test]
  public void Domain_success_converts_to_api_success() {
    DomainResult result = new DomainResult.Success();

    ApiResult.From(result).Value.ShouldBeOfType<ApiResult.Success>();
  }

  [Test]
  public void Domain_violations_convert_to_api_result_without_loss() {
    RuleViolation first = new("account.not-active", "Only an active account can be disabled.");
    RuleViolation second = new("account.other", "Another rule text.");

    DomainResult result = new DomainResult.RuleViolations([first, second]);

    ApiResult.From(result).ShouldBeRuleViolations().Violations.ShouldBe([first, second]);
  }

  [Test]
  public void The_value_carrying_forms_failure_converts_identically() {
    RuleViolation first = new("account.not-active", "Only an active account can be disabled.");
    RuleViolation second = new("account.other", "Another rule text.");

    DomainResult<int>.RuleViolations violated = new([first, second]);

    ApiResult.From(violated).ShouldBeRuleViolations().Violations.ShouldBe([first, second]);
  }

  [Test]
  public void Missing_violations_are_a_bug_and_throw() =>
    Should.Throw<ArgumentNullException>(static () => new DomainResult.RuleViolations(null!));

  [Test]
  public void Empty_violations_are_a_bug_and_throw() =>
    Should.Throw<ArgumentOutOfRangeException>(static () => new DomainResult.RuleViolations([]));

  [Test]
  public void Missing_violations_on_the_value_carrying_form_are_a_bug_and_throw() =>
    Should.Throw<ArgumentNullException>(static () => new DomainResult<int>.RuleViolations(null!));

  [Test]
  public void Violations_are_snapshotted_against_later_caller_mutation() {
    List<RuleViolation> source = [new("x.y", "Z.")];
    DomainResult.RuleViolations violated = new(source);

    source.Clear();

    violated.Violations.ShouldHaveSingleItem();
  }

  [Test]
  public void Violations_on_the_value_carrying_form_are_snapshotted_against_later_caller_mutation() {
    List<RuleViolation> source = [new("x.y", "Z.")];
    DomainResult<int>.RuleViolations violated = new(source);

    source.Clear();

    violated.Violations.ShouldHaveSingleItem();
  }

  // c001 design: default union values are never constructed — a runaway default is bug
  // class 8 and the crash is the designed behavior; a catch-all arm would erase the
  // closed-union exhaustiveness build error, so none may ever be added.
  [Test]
  public void A_defaulted_union_is_a_bug_and_throws_on_conversion() =>
    Should.Throw<SwitchExpressionException>(static () => ApiResult.From(default(DomainResult)));

  [Test]
  public void Empty_violations_on_the_value_carrying_form_are_a_bug_and_throw() =>
    Should.Throw<ArgumentOutOfRangeException>(static () => new DomainResult<int>.RuleViolations([]));

  [Test]
  public void Domain_results_with_the_same_case_are_equal() {
    DomainResult left = new DomainResult.Success();
    DomainResult right = new DomainResult.Success();

    left.Equals(right).ShouldBeTrue();
    (left == right).ShouldBeTrue();
    (left != right).ShouldBeFalse();
    left.GetHashCode().ShouldBe(right.GetHashCode());
  }
}
