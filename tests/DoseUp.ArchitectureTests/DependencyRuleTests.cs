using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DoseUp.ArchitectureTests;

/// <summary>
/// ADR-0002 dependency rules 1–4 and 6–7 (rule 5 lives in <see cref="SharedKernelRuleTests"/>).
/// Rules needing a same-module capture (2, 3, 4, 7) walk the ArchUnitNET model directly —
/// the fluent API cannot correlate namespaces. All pass vacuously until modules exist.
/// </summary>
public sealed partial class DependencyRuleTests
{
  [GeneratedRegex(@"^DoseUp\.Api\.Modules\.(?<module>[^.]+)\.(?<area>[^.]+)")]
  private static partial Regex ModuleArea();

  [GeneratedRegex(@"^DoseUp\.Api\.Modules\.(?<module>[^.]+)\.Features\.(?<slice>[^.]+)\.")]
  private static partial Regex FeatureSlice();

  [Test]
  public void Rule_01_domain_references_only_shared_kernel()
  {
    // ADR-0002 rule 1: "Domain references only SharedKernel (never Features, Infrastructure,
    // Platform, FastEndpoints, Wolverine, or data-access libraries — Npgsql, EF Core, …)."
    IArchRule rule = Types()
      .That()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.Modules\..+\.Domain")
      .Should()
      .NotDependOnAnyTypesThat()
      .ResideInNamespaceMatching(
        @"DoseUp\.Api\.Modules\..+\.(Features|Infrastructure)|DoseUp\.Api\.Platform|FastEndpoints|Wolverine|Microsoft\.EntityFrameworkCore|Npgsql"
      )
      .WithoutRequiringPositiveResults();

    rule.ShouldHold();
  }

  [Test]
  public void Rule_02_features_orchestrate_only_their_own_modules_domain()
  {
    // ADR-0002 rule 2: "Features orchestrate their own module's Domain through its ports;
    // they never touch another module's internals."
    List<string> violations =
    [
      .. DoseUpArchitecture
        .Instance.Types.Where(static type =>
          ModuleArea().Match(type.FullName) is { Success: true } match
          && match.Groups["area"].Value == "Features"
        )
        .SelectMany(static type =>
          type.Dependencies.Select(dependency => (Origin: type, dependency.Target))
            .Where(static edge =>
              ModuleArea().Match(edge.Target.FullName) is { Success: true } targetMatch
              && targetMatch.Groups["module"].Value
                != ModuleArea().Match(edge.Origin.FullName).Groups["module"].Value
              && targetMatch.Groups["area"].Value != "Contracts"
            )
            .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}")
        ),
    ];

    violations.ShouldBeEmpty();
  }

  [Test]
  public void Rule_03_cross_module_communication_is_contracts_and_events_only()
  {
    // ADR-0002 rule 3: "Cross-module communication happens only via public contracts and
    // integration events — never direct calls into another module's Domain/Features/Infrastructure."
    List<string> violations =
    [
      .. DoseUpArchitecture
        .Instance.Types.Where(static type => ModuleArea().IsMatch(type.FullName))
        .SelectMany(static type =>
          type.Dependencies.Select(dependency => (Origin: type, dependency.Target))
            .Where(static edge =>
              ModuleArea().Match(edge.Target.FullName) is { Success: true } targetMatch
              && targetMatch.Groups["module"].Value
                != ModuleArea().Match(edge.Origin.FullName).Groups["module"].Value
              && targetMatch.Groups["area"].Value != "Contracts"
            )
            .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}")
        ),
    ];

    violations.ShouldBeEmpty();
  }

  [Test]
  public void Rule_04_infrastructure_adapters_are_seen_only_by_platform()
  {
    // ADR-0002 rule 4: "Infrastructure implements its module's ports; concrete adapters are
    // seen only by Platform (composition root)."
    List<string> violations =
    [
      .. DoseUpArchitecture
        .Instance.Types.Where(static type =>
          type.FullName.StartsWith("DoseUp.Api", StringComparison.Ordinal)
        )
        .SelectMany(static type =>
          type.Dependencies.Select(dependency => (Origin: type, dependency.Target))
            .Where(static edge =>
              ModuleArea().Match(edge.Target.FullName) is { Success: true } targetMatch
              && targetMatch.Groups["area"].Value == "Infrastructure"
              && !edge.Origin.FullName.StartsWith("DoseUp.Api.Platform", StringComparison.Ordinal)
              && !edge.Origin.FullName.StartsWith(
                $"DoseUp.Api.Modules.{targetMatch.Groups["module"].Value}.Infrastructure",
                StringComparison.Ordinal
              )
            )
            .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}")
        ),
    ];

    violations.ShouldBeEmpty();
  }

  [Test]
  public void Rule_06_feature_handlers_and_validators_reference_no_http_types()
  {
    // ADR-0002 rule 6: "endpoint classes contain no use-case logic — they adapt HTTP to the
    // slice's feature handler; feature handlers and validators reference no FastEndpoints/ASP.NET types."
    IArchRule rule = Classes()
      .That()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.Modules\..+\.Features")
      .And()
      .HaveNameMatching(@"(Handler|Validator)$")
      .Should()
      .NotDependOnAnyTypesThat()
      .ResideInNamespaceMatching(@"^FastEndpoints|^Microsoft\.AspNetCore")
      .WithoutRequiringPositiveResults();

    rule.ShouldHold();
  }

  [Test]
  public void Rule_07_use_case_slices_never_reference_sibling_slices()
  {
    // ADR-0002 rule 7: "Use-case slice namespaces inside a module's Features never reference
    // sibling slice namespaces — shared behavior moves down into Domain or up into the
    // module's shared space."
    List<string> violations =
    [
      .. DoseUpArchitecture
        .Instance.Types.Where(static type => FeatureSlice().IsMatch(type.FullName))
        .SelectMany(static type =>
          type.Dependencies.Select(dependency => (Origin: type, dependency.Target))
            .Where(static edge =>
              FeatureSlice().Match(edge.Target.FullName) is { Success: true } targetMatch
              && FeatureSlice().Match(edge.Origin.FullName) is { Success: true } originMatch
              && targetMatch.Groups["module"].Value == originMatch.Groups["module"].Value
              && targetMatch.Groups["slice"].Value != originMatch.Groups["slice"].Value
            )
            .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}")
        ),
    ];

    violations.ShouldBeEmpty();
  }
}
