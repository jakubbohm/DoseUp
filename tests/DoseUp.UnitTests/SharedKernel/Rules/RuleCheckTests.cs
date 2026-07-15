using DoseUp.Api.SharedKernel.Rules;
using DoseUp.UnitTests.SharedKernel.Results;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Rules;

public sealed class RuleCheckTests
{
  [Test]
  public void A_failing_rule_names_itself_with_its_stable_code_and_static_message()
  {
    RuleCheck check = new RuleCheck.Fail(
      "schedule.not-active",
      "Only an active schedule can be edited."
    );

    RuleCheck.Fail fail = check.ShouldBeFail();
    RuleViolation violation = fail.Violations.ShouldHaveSingleItem();
    violation.Code.ShouldBe("schedule.not-active");
    violation.Message.ShouldBe("Only an active schedule can be edited.");
  }

  [Test]
  public void A_passing_rule_exposes_no_violations()
  {
    RuleCheck check = new RuleCheck.Pass();

    check.ShouldBePass();
    (check is RuleCheck.Fail).ShouldBeFalse();
  }

  [Test]
  public void A_fail_carries_multiple_violations_in_order()
  {
    RuleViolation first = new("a.one", "One.");
    RuleViolation second = new("a.two", "Two.");

    RuleCheck check = new RuleCheck.Fail([first, second]);

    check.ShouldBeFail().Violations.ShouldBe([first, second]);
  }

  [Test]
  public void A_fail_without_violations_is_a_bug_and_throws() =>
    Should.Throw<ArgumentOutOfRangeException>(static () => new RuleCheck.Fail([]));

  [Test]
  public void A_fail_with_null_violations_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => new RuleCheck.Fail(null!));

  [Test]
  public void Conversion_to_result_is_lossless()
  {
    RuleViolation first = new("schedule.not-active", "Only an active schedule can be edited.");
    RuleViolation second = new("schedule.name-taken", "A schedule with this name already exists.");
    RuleCheck.Fail fail = new([first, second]);

    fail.ToResult().ShouldBeRuleViolations().Violations.ShouldBe([first, second]);
  }
}
