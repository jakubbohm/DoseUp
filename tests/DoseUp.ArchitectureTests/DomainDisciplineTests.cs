using ArchUnitNET.Fluent;
using DoseUp.Api.Platform.Persistence;
using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Events;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DoseUp.ArchitectureTests;

public sealed class DomainDisciplineTests
{
  [Test]
  public void Rule_08_no_csharp_enum_in_domain_namespaces()
  {
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
  public void Rule_09_only_published_language_translators_and_platform_touch_the_publisher_port()
  {
    // ADR-0002 § Events / catalog rule 9: "Only per-module published-language translators
    // call IIntegrationEventPublisher" — Platform implements it over the outbox.
    string portFullName = typeof(IIntegrationEventPublisher).FullName!;

    List<string> offenders =
    [
      .. DoseUpArchitecture
        .Instance.Types.Where(static type =>
          type.FullName.StartsWith("DoseUp.Api", StringComparison.Ordinal)
          && !type.FullName.StartsWith("DoseUp.Api.SharedKernel", StringComparison.Ordinal)
        )
        .Where(type =>
          type.Dependencies.Any(dependency => dependency.Target.FullName == portFullName)
        )
        .Where(static type =>
          !type.FullName.StartsWith("DoseUp.Api.Platform", StringComparison.Ordinal)
          && !type.Name.EndsWith("PublishedLanguage", StringComparison.Ordinal)
        )
        .Select(static type => type.FullName),
    ];

    offenders.ShouldBeEmpty();
  }

  [Test]
  public void Rule_10_every_db_set_entity_is_an_aggregate_root()
  {
    // conventions § Domain model base types: IAggregateRoot "is what ArchUnitNET rules and
    // generic constraints key on (e.g., only aggregate roots exposed as DbSet)".
    // Reflection over the offline-built EF model — no database (design.md D11).
    using DoseUpDbContext context = new DoseUpDbContextFactory().CreateDbContext([]);

    List<string> offenders =
    [
      .. context
        .Model.GetEntityTypes()
        .Select(static entityType => entityType.ClrType)
        .Where(static clrType => !typeof(IAggregateRoot).IsAssignableFrom(clrType))
        .Select(static clrType => clrType.FullName!),
    ];

    offenders.ShouldBeEmpty();
  }
}
