using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace DoseUp.ArchitectureTests;

/// <summary>
/// testing.md §2 placement row "EF configuration / migration": one
/// <c>HasPendingModelChanges</c> guard per module context — the compiled model must match
/// the committed snapshot, so a model edit without <c>dotnet ef migrations add</c> fails
/// here instead of at deploy time. Offline via the design-time factory; no database.
/// </summary>
public sealed class MigrationGuardTests {
  [Test]
  public void No_module_context_has_model_changes_missing_from_its_migrations() {
    DbContextDiscovery.ModuleContexts.ShouldNotBeEmpty();

    List<string> offenders = DbContextDiscovery.CollectOffendersAcrossModuleModels(static (_, context) =>
      context.Database.HasPendingModelChanges()
        ? [$"{context.GetType().FullName} has model changes not captured by a migration"]
        : []);

    offenders.ShouldBeEmpty();
  }
}
