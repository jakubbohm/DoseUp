using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Results;
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
  public void Missing_rule_violations_are_a_bug_and_throw() =>
    Should.Throw<ArgumentNullException>(static () => new ApiResult.RuleViolations(null!));

  [Test]
  public void Rule_violations_naming_no_violated_rule_are_a_bug_and_throw() =>
    Should.Throw<ArgumentOutOfRangeException>(static () => new ApiResult.RuleViolations([]));

  [Test]
  public void Missing_validation_errors_are_a_bug_and_throw() =>
    Should.Throw<ArgumentNullException>(static () => new ApiResult.Validation(null!));

  [Test]
  public void Validation_naming_no_field_error_is_a_bug_and_throws() =>
    Should.Throw<ArgumentOutOfRangeException>(static () => new ApiResult.Validation(new Dictionary<string, string[]>()));

  [Test]
  public void A_field_with_no_error_messages_is_a_bug_and_throws() =>
    Should.Throw<ArgumentOutOfRangeException>(static () => new ApiResult.Validation(new Dictionary<string, string[]> { ["name"] = [] }));

  [Test]
  public void A_field_with_missing_error_messages_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => new ApiResult.Validation(new Dictionary<string, string[]> { ["name"] = null! }));

  [Test]
  public void Converting_a_missing_value_carrying_failure_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => ApiResult.From((DomainResult<int>.RuleViolations)null!));

  [Test]
  public void Converting_a_missing_failed_check_is_a_bug_and_throws() =>
    Should.Throw<ArgumentNullException>(static () => ApiResult.From((RuleCheck.Fail)null!));

  [Test]
  public void Violations_are_snapshotted_against_later_caller_mutation() {
    List<RuleViolation> source = [new("x.y", "Z.")];
    ApiResult.RuleViolations violated = new(source);

    source.Clear();

    violated.Violations.ShouldHaveSingleItem();
  }

  [Test]
  public void Validation_errors_are_snapshotted_against_later_caller_mutation() {
    Dictionary<string, string[]> errors = new() { ["name"] = ["Name is required."] };
    ApiResult.Validation validation = new(errors);

    errors.Clear();

    validation.Errors.Count.ShouldBe(1);
  }

  [Test]
  public void Validation_error_contents_are_snapshotted_against_later_caller_mutation() {
    // The arrays are the mutable leaves — a pair-level dictionary copy alone would alias them.
    string[] messages = ["Name is required."];
    ApiResult.Validation validation = new(new Dictionary<string, string[]> { ["name"] = messages });

    messages[0] = "mutated";

    validation.Errors["name"][0].ShouldBe("Name is required.");
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