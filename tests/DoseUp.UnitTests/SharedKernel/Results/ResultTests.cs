using DoseUp.Api.SharedKernel.Results;
using DoseUp.Api.SharedKernel.Rules;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Results;

public sealed class ResultTests {
  [Test]
  public void Success_is_distinguishable_from_every_failure_case() {
    Result result = new Result.Success();

    (result is Result.Success).ShouldBeTrue();
    (result is Result.Validation).ShouldBeFalse();
    (result is Result.NotFound).ShouldBeFalse();
    (result is Result.RuleViolations).ShouldBeFalse();
    (result is Result.Conflict).ShouldBeFalse();
    (result is Result.Forbidden).ShouldBeFalse();
    (result is Result.Unexpected).ShouldBeFalse();
  }

  [Test]
  public void Every_case_is_distinguishable_by_pattern_matching() {
    // The switch has no default arm — this compiling at all is the closed-union
    // exhaustiveness guarantee (a missing case is a build error under TreatWarningsAsErrors).
    static string Label(Result result) =>
      result switch {
        Result.Success => "success",
        Result.Validation => "validation",
        Result.NotFound => "not-found",
        Result.RuleViolations => "rule-violations",
        Result.Conflict => "conflict",
        Result.Forbidden => "forbidden",
        Result.Unexpected => "unexpected",
      };

    Label(new Result.Success()).ShouldBe("success");
    Label(new Result.Validation(new Dictionary<string, string[]> { ["f"] = ["e"] }))
      .ShouldBe("validation");
    Label(new Result.NotFound()).ShouldBe("not-found");
    Label(new Result.RuleViolations([new RuleViolation("x.y", "z")])).ShouldBe("rule-violations");
    Label(new Result.Conflict()).ShouldBe("conflict");
    Label(new Result.Forbidden()).ShouldBe("forbidden");
    Label(new Result.Unexpected()).ShouldBe("unexpected");
  }

  [Test]
  public void Rule_violation_payloads_survive_the_union_without_loss_or_reordering() {
    RuleViolation first = new("schedule.not-active", "Only an active schedule can be edited.");
    RuleViolation second = new("schedule.name-taken", "A schedule with this name already exists.");

    Result result = new Result.RuleViolations([first, second]);

    result.ShouldBeRuleViolations().Violations.ShouldBe([first, second]);
  }

  [Test]
  public void Validation_carries_every_fields_errors_at_once() {
    Dictionary<string, string[]> errors = new() {
      ["name"] = ["Name is required."],
      ["timing"] = ["Timing is out of range.", "Timing must be in the future."],
    };

    Result result = new Result.Validation(errors);

    Result.Validation validation = result.ShouldBeValidation();
    validation.Errors["name"].ShouldBe(["Name is required."]);
    validation
      .Errors["timing"]
      .ShouldBe(["Timing is out of range.", "Timing must be in the future."]);
  }

  [Test]
  public void Results_with_the_same_case_are_equal() {
    Result left = new Result.Success();
    Result right = new Result.Success();

    left.Equals(right).ShouldBeTrue();
    (left == right).ShouldBeTrue();
    (left != right).ShouldBeFalse();
    left.GetHashCode().ShouldBe(right.GetHashCode());
  }

  [Test]
  public void Results_with_different_cases_are_not_equal() {
    Result success = new Result.Success();
    Result notFound = new Result.NotFound();

    success.Equals(notFound).ShouldBeFalse();
    (success == notFound).ShouldBeFalse();
    (success != notFound).ShouldBeTrue();
  }
}