using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using DoseUp.Api.SharedKernel.Results;
using Shouldly;

namespace DoseUp.ArchitectureTests;

/// <summary>
/// One shared architecture over the Api assembly (design.md D11) — loaded once, used by
/// every catalog rule.
/// </summary>
public static class DoseUpArchitecture {
  public static Architecture Instance { get; } = new ArchLoader().LoadAssemblies(typeof(Result).Assembly, typeof(FastEndpoints.BaseEndpoint).Assembly).Build();

  /// <summary>
  /// Evaluates the rule against the shared architecture and fails with the rule's own
  /// violation descriptions. (Deliberately not the TngTech.ArchUnitNET.TUnit adapter —
  /// it would pull TUnit.Assertions transitively, which testing.md §6.6 keeps out.)
  /// </summary>
  public static void ShouldHold(this IArchRule rule) {
    ArgumentNullException.ThrowIfNull(rule);

    List<string> violations = [.. rule.Evaluate(Instance).Where(static result => !result.Passed).Select(static result => result.Description)];

    violations.ShouldBeEmpty();
  }
}