using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using FastEndpoints;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DoseUp.ArchitectureTests;

public sealed partial class SliceAnatomyTests {
  [GeneratedRegex(@"^DoseUp\.Api\.Modules\.(?<module>[^.]+)\.Features\.")]
  private static partial Regex FeatureNamespace();

  [Test]
  public void Rule_11_a_slices_endpoint_and_its_collaborators_share_one_namespace() {
    // ADR-0002 § Slices / catalog rule 11: "one use case's endpoint + handler + validator +
    // DTOs share one namespace" — checked as: everything a Features endpoint constructor
    // injects from its own module lives in the endpoint's namespace.
    List<string> violations = [
      .. typeof(DoseUp.Api.SharedKernel.Results.ApiResult).Assembly.GetTypes()
        .Where(static type => typeof(BaseEndpoint).IsAssignableFrom(type) && !type.IsAbstract && FeatureNamespace().IsMatch(type.FullName ?? string.Empty))
        .SelectMany(static endpointType =>
          endpointType.GetConstructors()
            .SelectMany(static constructor => constructor.GetParameters())
            .Where(parameter =>
              FeatureNamespace().Match(parameter.ParameterType.Namespace ?? string.Empty) is { Success: true } parameterModule
              && parameterModule.Groups["module"].Value == FeatureNamespace().Match(endpointType.FullName!).Groups["module"].Value
              && parameter.ParameterType.Namespace != endpointType.Namespace
            )
            .Select(parameter => $"{endpointType.FullName} injects {parameter.ParameterType.FullName} from a different namespace")
        ),
    ];

    violations.ShouldBeEmpty();
  }

  [Test]
  public void Rule_14_endpoint_classes_end_with_endpoint() {
    // testing.md §5 rule 14: "endpoints end `Endpoint`" — non-vacuous from day one
    // (the identity-echo diagnostic already matches).
    IArchRule rule = Classes()
      .That()
      .AreAssignableTo(typeof(BaseEndpoint))
      .And()
      .AreNotAbstract()
      .And()
      .ResideInNamespaceMatching(@"^DoseUp\.Api")
      .Should()
      .HaveNameEndingWith("Endpoint");

    rule.ShouldHold();
  }

  [Test]
  public void Rule_14_validator_named_classes_reside_in_features_namespaces() {
    // testing.md §5 rule 14 (the reverse direction): "*Validator-named classes reside in
    // Features namespaces — catches misplacement". The forward rule (FluentValidation
    // subclasses end `Validator`) activates when the first validator lands (M0/M1).
    IArchRule rule = Classes()
      .That()
      .HaveNameEndingWith("Validator")
      .And()
      .ResideInNamespaceMatching(@"^DoseUp\.Api")
      .Should()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.Modules\..+\.Features")
      .WithoutRequiringPositiveResults();

    rule.ShouldHold();
  }
}