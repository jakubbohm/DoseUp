using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Events;
using DoseUp.Api.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DoseUp.ArchitectureTests;

public sealed class DomainDisciplineTests {
  [Test]
  public void Rule_08_no_csharp_enum_in_domain_namespaces() {
    // conventions § Domain enumerations: "Every closed value set in the domain layer is an
    // Ardalis.SmartEnum; C# enum is banned in domain assemblies." (DTO carve-out lives in Features.)
    IArchRule rule = Types()
      .That()
      .ResideInNamespaceMatching(@"DoseUp\.Api\.Modules\..+\.Domain")
      .Should()
      .NotBeEnums()
      .WithoutRequiringPositiveResults();

    rule.ShouldHold();
  }

  [Test]
  public void Rule_21_domain_side_namespaces_never_reference_the_edge_results_namespace() {
    // #97, generalized by #99 / catalog rule 21: the dependency rule has a direction —
    // domain-side namespaces (module Domain and SharedKernel.Domain, which since the
    // Rules merge holds the whole rule vocabulary) never reference the edge results
    // namespace; conversions live on the edge union (ApiResult.From), which references
    // inward. The edge speaks the API's language (NotFound, Forbidden, Validation …) —
    // meaningless inside an aggregate. The prefix is a typeof anchor, not a string
    // literal: a rename/move of the edge union updates it or breaks compilation, so the
    // rule cannot rot into a vacuous pass.
    string edgeNamespacePrefix = typeof(ApiResult).Namespace + ".";

    List<string> offenders = [
      .. DoseUpArchitecture.Instance.Types
        .Where(static type => Regex.IsMatch(type.FullName, @"^DoseUp\.Api\.(Modules\.[^.]+\.Domain|SharedKernel\.Domain)\."))
        .Where(type => type.Dependencies.Any(dependency =>
          dependency.Target.FullName.StartsWith(edgeNamespacePrefix, StringComparison.Ordinal)))
        .Select(static type => type.FullName),
    ];

    offenders.ShouldBeEmpty();
  }

  [Test]
  public void Rule_09_only_published_language_translators_and_platform_touch_the_publisher_port() {
    // ADR-0002 § Events / catalog rule 9: "Only per-module published-language translators
    // call IIntegrationEventPublisher" — Platform implements it over the outbox.
    string portFullName = typeof(IIntegrationEventPublisher).FullName!;

    List<string> offenders = [
      .. DoseUpArchitecture.Instance.Types.Where(static type =>
          type.FullName.StartsWith("DoseUp.Api", StringComparison.Ordinal)
          && !type.FullName.StartsWith("DoseUp.Api.SharedKernel", StringComparison.Ordinal)
        )
        .Where(type => type.Dependencies.Any(dependency => dependency.Target.FullName == portFullName))
        .Where(static type => !type.FullName.StartsWith("DoseUp.Api.Platform", StringComparison.Ordinal) && !type.Name.EndsWith("PublishedLanguage", StringComparison.Ordinal))
        .Select(static type => type.FullName),
    ];

    offenders.ShouldBeEmpty();
  }

  [Test]
  public void Rule_10_every_mapped_entity_of_every_discovered_db_context_is_an_aggregate_root() {
    // testing.md §5 catalog row 10: "For every DbContext discovered in the API assembly,
    // every mapped entity type implements IAggregateRoot (conventions/README.md § Domain model base types)" — reflection
    // over the offline-built EF models, no database (design.md D11). Discovery itself must
    // be non-vacuous: it finds the module contexts (first subject: MembershipDbContext, c002).
    DbContextDiscovery.AllContexts.ShouldNotBeEmpty();

    List<string> offenders = DbContextDiscovery.CollectOffendersAcrossAllModels(static context =>
      context.Model.GetEntityTypes()
        .Select(static entityType => entityType.ClrType)
        .Where(static clrType => !typeof(IAggregateRoot).IsAssignableFrom(clrType))
        .Select(clrType => $"{context.GetType().FullName} maps {clrType.FullName}"));

    offenders.ShouldBeEmpty();
  }

  [Test]
  public void Rule_17_a_modules_context_maps_only_its_own_modules_domain_types() {
    // ADR-0002 § Persistence is module property / catalog rule 17: "a module's context maps
    // only its own module's Domain types" — reflection over the offline-built EF models,
    // no database. First real subject: MembershipDbContext (c002).
    List<string> offenders = DbContextDiscovery.CollectOffendersAcrossModuleModels(static (module, context) =>
      context.Model.GetEntityTypes()
        .Select(static entityType => entityType.ClrType)
        .Where(clrType => !clrType.FullName!.StartsWith($"DoseUp.Api.Modules.{module}.Domain.", StringComparison.Ordinal))
        .Select(clrType => $"{context.GetType().FullName} maps {clrType.FullName}"));

    offenders.ShouldBeEmpty();
  }

  [Test]
  public void No_context_lives_outside_modules() {
    // ADR-0002 § Persistence is module property: persistence is module property, so every
    // context is module-owned. Rules 17–19 quantify over module contexts only — a context
    // outside Modules/ would escape them entirely; this tripwire closes that escape (the
    // c001 bootstrap placeholder was the one sanctioned exception, deleted by c002/#55).
    List<string> nonModuleContexts = [
      .. DbContextDiscovery.AllContexts
        .Except(DbContextDiscovery.ModuleContexts.Select(static pair => pair.ContextType))
        .Select(static type => type.FullName!),
    ];

    nonModuleContexts.ShouldBeEmpty();
  }

  [Test]
  [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Lowercase is the spec, not normalization: ADR-0002 fixes each module's schema to the lowercase module name.")]
  public void Rule_19_every_mapped_entity_sits_in_the_modules_schema() {
    // ADR-0002 § Persistence is module property / catalog rule 19: "every mapped entity sits
    // in the module's schema" — "one schema per module (HasDefaultSchema, lowercase module
    // name)"; entityType.GetSchema() falls back to the model's default schema. Reflection
    // over the offline-built EF models, no database. First real subject: MembershipDbContext (c002).
    List<string> offenders = DbContextDiscovery.CollectOffendersAcrossModuleModels(static (module, context) => {
      string expectedSchema = module.ToLowerInvariant();

      return context.Model.GetEntityTypes()
        .Where(entityType => (entityType.GetSchema() ?? context.Model.GetDefaultSchema()) != expectedSchema)
        .Select(entityType => $"{context.GetType().FullName} puts {entityType.Name} in schema '{entityType.GetSchema() ?? context.Model.GetDefaultSchema()}'");
    });

    offenders.ShouldBeEmpty();
  }
}