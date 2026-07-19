using DoseUp.Api.SharedKernel.Results;
using DoseUp.Api.SharedKernel.Rules;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Results;

public sealed class ApiResultTests {
  [Test]
  public void Success_is_distinguishable_from_every_failure_case() {
    ApiResult result = new ApiResult.Success();

    (result is ApiResult.Success).ShouldBeTrue();
    (result is ApiResult.Validation).ShouldBeFalse();
    (result is ApiResult.NotFound).ShouldBeFalse();
    (result is ApiResult.RuleViolations).ShouldBeFalse();
    (result is ApiResult.Conflict).ShouldBeFalse();
    (result is ApiResult.Forbidden).ShouldBeFalse();
    (result is ApiResult.Unexpected).ShouldBeFalse();
  }

  [Test]
  public void Every_case_is_distinguishable_by_pattern_matching() {
    // The switch has no default arm — this compiling at all is the closed-union
    // exhaustiveness guarantee (a missing case is a build error under TreatWarningsAsErrors).
    static string Label(ApiResult result) => result switch {
      ApiResult.Success => "success",
      ApiResult.Validation => "validation",
      ApiResult.NotFound => "not-found",
      ApiResult.RuleViolations => "rule-violations",
      ApiResult.Conflict => "conflict",
      ApiResult.Forbidden => "forbidden",
      ApiResult.Unexpected => "unexpected",
    };

    Label(new ApiResult.Success()).ShouldBe("success");
    Label(new ApiResult.Validation(new Dictionary<string, string[]> { ["f"] = ["e"] })).ShouldBe("validation");
    Label(new ApiResult.NotFound()).ShouldBe("not-found");
    Label(new ApiResult.RuleViolations([new RuleViolation("x.y", "z")])).ShouldBe("rule-violations");
    Label(new ApiResult.Conflict()).ShouldBe("conflict");
    Label(new ApiResult.Forbidden()).ShouldBe("forbidden");
    Label(new ApiResult.Unexpected()).ShouldBe("unexpected");
  }

  [Test]
  public void Rule_violation_payloads_survive_the_union_without_loss_or_reordering() {
    RuleViolation first = new("schedule.not-active", "Only an active schedule can be edited.");
    RuleViolation second = new("schedule.name-taken", "A schedule with this name already exists.");

    ApiResult result = new ApiResult.RuleViolations([first, second]);

    result.ShouldBeRuleViolations().Violations.ShouldBe([first, second]);
  }

  [Test]
  public void Validation_carries_every_fields_errors_at_once() {
    Dictionary<string, string[]> errors = new() {
      ["name"] = ["Name is required."],
      ["timing"] = ["Timing is out of range.", "Timing must be in the future."],
    };

    ApiResult result = new ApiResult.Validation(errors);

    ApiResult.Validation validation = result.ShouldBeValidation();
    validation.Errors["name"].ShouldBe(["Name is required."]);
    validation.Errors["timing"].ShouldBe(["Timing is out of range.", "Timing must be in the future."]);
  }

  [Test]
  public void Results_with_the_same_case_are_equal() {
    ApiResult left = new ApiResult.Success();
    ApiResult right = new ApiResult.Success();

    left.Equals(right).ShouldBeTrue();
    (left == right).ShouldBeTrue();
    (left != right).ShouldBeFalse();
    left.GetHashCode().ShouldBe(right.GetHashCode());
  }

  [Test]
  public void Results_with_different_cases_are_not_equal() {
    ApiResult success = new ApiResult.Success();
    ApiResult notFound = new ApiResult.NotFound();

    success.Equals(notFound).ShouldBeFalse();
    (success == notFound).ShouldBeFalse();
    (success != notFound).ShouldBeTrue();
  }
}