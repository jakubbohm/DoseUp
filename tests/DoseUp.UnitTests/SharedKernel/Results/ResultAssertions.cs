using DoseUp.Api.SharedKernel.Results;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Results;

/// <summary>
/// Domain-shaped Result assertions (testing.md §6.6) — union unwrapping centralized.
/// Union <c>ToString()</c> returns only the type name, so messages format the case value.
/// </summary>
public static class ResultAssertions {
  public static Result.Validation ShouldBeValidation(this Result result) =>
    result is Result.Validation validation
      ? validation
      : throw new ShouldAssertException($"Expected Validation but got: {Describe(result)}");

  public static Result.RuleViolations ShouldBeRuleViolations(this Result result) =>
    result is Result.RuleViolations violations
      ? violations
      : throw new ShouldAssertException($"Expected RuleViolations but got: {Describe(result)}");

  private static string Describe(Result result) => result.Value?.ToString() ?? "default(Result)";
}