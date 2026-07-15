using DoseUp.Api.Platform.ErrorHandling;
using DoseUp.Api.SharedKernel.Results;
using DoseUp.Api.SharedKernel.Rules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace DoseUp.UnitTests.Platform.ErrorHandling;

public sealed class ResultProblemDetailsMapperTests {
  [Test]
  public void Not_found_maps_to_404() {
    Result result = new Result.NotFound();

    result.ToProblemDetails().Status.ShouldBe(404);
  }

  [Test]
  public void Validation_maps_to_400_reporting_every_invalid_field() {
    Result result = new Result.Validation(
      new Dictionary<string, string[]> {
        ["name"] = ["Name is required."],
        ["timing"] = ["Timing is out of range."],
      }
    );

    ProblemDetails problem = result.ToProblemDetails();

    problem.Status.ShouldBe(400);
    HttpValidationProblemDetails validation =
      problem.ShouldBeOfType<HttpValidationProblemDetails>();
    validation.Errors["name"].ShouldBe(["Name is required."]);
    validation.Errors["timing"].ShouldBe(["Timing is out of range."]);
  }

  [Test]
  public void Rule_violations_ride_one_409_with_the_violations_array() {
    RuleViolation first = new("schedule.not-active", "Only an active schedule can be edited.");
    RuleViolation second = new("schedule.name-taken", "A schedule with this name already exists.");
    Result result = new Result.RuleViolations([first, second]);

    ProblemDetails problem = result.ToProblemDetails();

    problem.Status.ShouldBe(409);
    problem.Type.ShouldBe("https://doseup.app/problems/rule-violation");
    problem
      .Extensions["violations"]
      .ShouldBeAssignableTo<IReadOnlyList<RuleViolation>>()
      .ShouldBe([first, second]);
  }

  [Test]
  public void The_two_409_classes_carry_distinct_problem_types() {
    Result ruleViolations = new Result.RuleViolations([new RuleViolation("a.b", "C.")]);
    Result conflict = new Result.Conflict();

    ProblemDetails ruleViolationsProblem = ruleViolations.ToProblemDetails();
    ProblemDetails conflictProblem = conflict.ToProblemDetails();

    ruleViolationsProblem.Status.ShouldBe(409);
    conflictProblem.Status.ShouldBe(409);
    conflictProblem.Type.ShouldNotBe(ruleViolationsProblem.Type);
  }

  [Test]
  public void Forbidden_maps_to_403() {
    Result result = new Result.Forbidden();

    result.ToProblemDetails().Status.ShouldBe(403);
  }

  [Test]
  public void Unexpected_maps_to_500_carrying_no_internals() {
    Result result = new Result.Unexpected();

    ProblemDetails problem = result.ToProblemDetails();

    problem.Status.ShouldBe(500);
    problem.Title.ShouldBe("An unexpected error occurred.");
    problem.Detail.ShouldBeNull();
    problem.Extensions.ShouldBeEmpty();
  }

  [Test]
  public void Mapping_success_is_a_caller_bug_and_throws() {
    Result result = new Result.Success();

    Should.Throw<ArgumentException>(() => result.ToProblemDetails());
  }
}