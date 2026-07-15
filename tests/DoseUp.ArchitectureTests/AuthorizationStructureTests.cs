using ArchUnitNET.Domain.Dependencies;
using Shouldly;

namespace DoseUp.ArchitectureTests;

public sealed class AuthorizationStructureTests {
  // Rule 12's allowlist IS this data (testing.md §5): adding an anonymous endpoint means
  // editing this list in the same PR. The health/alive probes are not FastEndpoints
  // endpoints — they are the matrix's manual AnonymousAllowed rows (design.md D6).
  private static readonly string[] ANONYMOUS_ENDPOINT_ALLOWLIST = [];

  [Test]
  public void Rule_12_allow_anonymous_is_an_explicit_allowlist() {
    // PRE-10 ring 0 / catalog rule 12: "endpoints are secure by default, AllowAnonymous is
    // an explicit, architecture-tested allowlist."
    List<string> undeclared =
    [
      .. DoseUpArchitecture
        .Instance.Types.Where(static type =>
          type.FullName.StartsWith("DoseUp.Api", StringComparison.Ordinal)
          && type.Name.EndsWith("Endpoint", StringComparison.Ordinal)
        )
        .Where(static type =>
          type.Dependencies.OfType<MethodCallDependency>()
            .Any(static call =>
              call.TargetMember.FullName.Contains("AllowAnonymous", StringComparison.Ordinal)
            )
        )
        .Select(static type => type.FullName)
        .Except(ANONYMOUS_ENDPOINT_ALLOWLIST, StringComparer.Ordinal),
    ];

    undeclared.ShouldBeEmpty();
  }

  [Test]
  public void Rule_13_admin_named_endpoints_join_the_admin_group() {
    // PRE-10 ring 1 / catalog rule 13: "Admin endpoints live in one FastEndpoints group
    // carrying the AdminOnly policy." Directional until M0 lands the group type: any
    // endpoint class named Admin* must configure itself into a group.
    List<string> ungrouped =
    [
      .. DoseUpArchitecture
        .Instance.Types.Where(static type =>
          type.FullName.StartsWith("DoseUp.Api", StringComparison.Ordinal)
          && type.Name.EndsWith("Endpoint", StringComparison.Ordinal)
          && type.Name.StartsWith("Admin", StringComparison.Ordinal)
        )
        .Where(static type =>
          !type
            .Dependencies.OfType<MethodCallDependency>()
            .Any(static call =>
              call.TargetMember.Name.StartsWith("Group", StringComparison.Ordinal)
            )
        )
        .Select(static type => type.FullName),
    ];

    ungrouped.ShouldBeEmpty();
  }
}