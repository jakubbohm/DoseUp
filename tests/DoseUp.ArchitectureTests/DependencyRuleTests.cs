using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DoseUp.ArchitectureTests;

/// <summary>
/// ADR-0002 dependency rules 1–4 and 6–7 (rule 5 lives in <see cref="SharedKernelRuleTests"/>),
/// plus persistence boundary rule 18 (§ Persistence is module property, 2026-07-15).
/// Rules needing a same-module capture (2, 3, 4, 7, 18) walk the ArchUnitNET model directly —
/// the fluent API cannot correlate namespaces. All pass vacuously until modules exist.
/// </summary>
public sealed partial class DependencyRuleTests {
  [GeneratedRegex(@"^DoseUp\.Api\.Modules\.(?<module>[^.]+)\.(?<area>[^.]+)")]
  private static partial Regex ModuleArea();

  [GeneratedRegex(@"^DoseUp\.Api\.Modules\.(?<module>[^.]+)\.Features\.(?<slice>[^.]+)\.")]
  private static partial Regex FeatureSlice();

  [Test]
  public void Rule_01_domain_references_only_shared_kernel() {
    // ADR-0002 rule 1: "Domain references only SharedKernel (never Features, Infrastructure,
    // Platform, FastEndpoints, Wolverine, or data-access libraries — Npgsql, EF Core, …)."
    IArchRule rule = Types()
      .That()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.Modules\..+\.Domain")
      .Should()
      .NotDependOnAnyTypesThat()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.Modules\..+\.(Features|Infrastructure)|DoseUp\.Api\.Platform|FastEndpoints|Wolverine|Microsoft\.EntityFrameworkCore|Npgsql")
      .WithoutRequiringPositiveResults();

    rule.ShouldHold();
  }

  [Test]
  public void Rule_02_features_orchestrate_only_their_own_modules_domain() {
    // ADR-0002 rule 2: "Features orchestrate their own module's Domain through its ports;
    // they never touch another module's internals."
    List<string> violations = [
      .. DoseUpArchitecture.Instance.Types.Where(static type => ModuleArea().Match(type.FullName) is { Success: true } match && match.Groups["area"].Value == "Features")
        .SelectMany(static type =>
          type.Dependencies.Select(dependency => (Origin: type, dependency.Target))
            .Where(static edge =>
              ModuleArea().Match(edge.Target.FullName) is { Success: true } targetMatch
              && targetMatch.Groups["module"].Value != ModuleArea().Match(edge.Origin.FullName).Groups["module"].Value
              && targetMatch.Groups["area"].Value != "Contracts"
            )
            .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}")
        ),
    ];

    violations.ShouldBeEmpty();
  }

  [Test]
  public void Rule_03_cross_module_communication_is_contracts_and_events_only() {
    // ADR-0002 rule 3: "Cross-module communication happens only via public contracts and
    // integration events — never direct calls into another module's Domain/Features/Infrastructure."
    List<string> violations = [
      .. DoseUpArchitecture.Instance.Types.Where(static type => ModuleArea().IsMatch(type.FullName))
        .SelectMany(static type =>
          type.Dependencies.Select(dependency => (Origin: type, dependency.Target))
            .Where(static edge =>
              ModuleArea().Match(edge.Target.FullName) is { Success: true } targetMatch
              && targetMatch.Groups["module"].Value != ModuleArea().Match(edge.Origin.FullName).Groups["module"].Value
              && targetMatch.Groups["area"].Value != "Contracts"
            )
            .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}")
        ),
    ];

    violations.ShouldBeEmpty();
  }

  [Test]
  public void Rule_04_module_db_context_is_consumed_only_by_its_own_module_other_infrastructure_only_by_platform() {
    // ADR-0002 rule 4 (revised 2026-07-15): "The module's DbContext is the module's
    // data-access API — consumed directly by its own Features, and only them
    // (repository-free); every other Infrastructure type — port adapters, the published-language
    // translator — is seen only by the composition root ..., or by nothing at all."
    // For the context the walk allows the owning module's Features (the carve-out) and its
    // own Infrastructure (the design-time factory's mechanical reference) plus the
    // composition root; every other Infrastructure type allows only same-module
    // Infrastructure and the composition root. Trailing dots keep the prefixes exact.
    HashSet<string> moduleContextNames = [.. DbContextDiscovery.ModuleContexts.Select(static pair => pair.ContextType.FullName!)];

    List<string> violations = [
      .. DoseUpArchitecture.Instance.Types.Where(static type => type.FullName.StartsWith("DoseUp.Api.", StringComparison.Ordinal))
        .SelectMany(type =>
          type.Dependencies.Select(dependency => (Origin: type, dependency.Target))
            .Where(edge => {
              if (ModuleArea().Match(edge.Target.FullName) is not { Success: true } targetMatch || targetMatch.Groups["area"].Value != "Infrastructure")
                return false;

              string module = targetMatch.Groups["module"].Value;
              string origin = edge.Origin.FullName;

              bool allowed = origin.StartsWith("DoseUp.Api.Platform.", StringComparison.Ordinal)
                || origin.StartsWith($"DoseUp.Api.Modules.{module}.Infrastructure.", StringComparison.Ordinal)
                || (moduleContextNames.Contains(edge.Target.FullName) && origin.StartsWith($"DoseUp.Api.Modules.{module}.Features.", StringComparison.Ordinal));

              return !allowed;
            })
            .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}")
        ),
    ];

    violations.ShouldBeEmpty();
  }

  [Test]
  public void Rule_06_feature_handlers_and_validators_reference_no_http_types() {
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
  public void Rule_07_use_case_slices_never_reference_sibling_slices() {
    // ADR-0002 rule 7: "Use-case slice namespaces inside a module's Features never reference
    // sibling slice namespaces — shared behavior moves down into Domain or up into the
    // module's shared space."
    List<string> violations = [
      .. DoseUpArchitecture.Instance.Types.Where(static type => FeatureSlice().IsMatch(type.FullName))
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

  [Test]
  public void Rule_18_a_modules_context_is_consumed_only_by_its_own_module_and_the_composition_root() {
    // ADR-0002 § Persistence is module property / catalog rule 18: "a module's context is
    // consumed only by its own module and the composition root — cross-module context
    // injection fails the build."
    Dictionary<string, string> moduleByContextName = DbContextDiscovery.ModuleContexts.ToDictionary(static pair => pair.ContextType.FullName!, static pair => pair.Module);

    List<string> violations = [
      .. DoseUpArchitecture.Instance.Types
        .SelectMany(static type => type.Dependencies.Select(dependency => (Origin: type, dependency.Target)))
        .Where(edge =>
          moduleByContextName.TryGetValue(edge.Target.FullName, out string? module)
          && !edge.Origin.FullName.StartsWith($"DoseUp.Api.Modules.{module}.", StringComparison.Ordinal)
          && !edge.Origin.FullName.StartsWith("DoseUp.Api.Platform.", StringComparison.Ordinal)
        )
        .Select(static edge => $"{edge.Origin.FullName} -> {edge.Target.FullName}"),
    ];

    violations.ShouldBeEmpty();
  }
}