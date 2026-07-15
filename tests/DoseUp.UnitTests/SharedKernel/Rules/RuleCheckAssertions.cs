using DoseUp.Api.SharedKernel.Rules;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Rules;

/// <summary>
/// Domain-shaped Shouldly extensions (testing.md §6.6) — tests read as spec and union
/// unwrapping stays centralized. Union <c>ToString()</c> returns only the type name, so
/// failure messages format the case value explicitly.
/// </summary>
public static class RuleCheckAssertions {
  public static void ShouldBePass(this RuleCheck check) =>
    (check is RuleCheck.Pass).ShouldBeTrue($"Expected Pass but got: {Describe(check)}");

  public static RuleCheck.Fail ShouldBeFail(this RuleCheck check) =>
    check is RuleCheck.Fail fail
      ? fail
      : throw new ShouldAssertException($"Expected Fail but got: {Describe(check)}");

  public static void ShouldBeFailWith(this RuleCheck check, params string[] codes) =>
    check.ShouldBeFail().Violations.Select(static v => v.Code).ShouldBe(codes);

  private static string Describe(RuleCheck check) =>
    check.Value?.ToString() ?? "default(RuleCheck)";
}