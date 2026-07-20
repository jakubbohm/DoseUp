using DoseUp.Api.SharedKernel.Results;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Results;

/// <summary>
/// Domain-shaped ApiResult assertions (testing.md §6.6) — union unwrapping centralized.
/// Union <c>ToString()</c> returns only the type name, so messages format the case value.
/// </summary>
public static class ApiResultAssertions {
  public static ApiResult.Validation ShouldBeValidation(this ApiResult result) =>
    result is ApiResult.Validation validation ? validation : throw new ShouldAssertException($"Expected Validation but got: {Describe(result)}");

  public static ApiResult.RuleViolations ShouldBeRuleViolations(this ApiResult result) =>
    result is ApiResult.RuleViolations violations ? violations : throw new ShouldAssertException($"Expected RuleViolations but got: {Describe(result)}");

  private static string Describe(ApiResult result) => result.Value?.ToString() ?? "default(ApiResult)";
}