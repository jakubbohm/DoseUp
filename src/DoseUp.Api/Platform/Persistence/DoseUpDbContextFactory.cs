using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DoseUp.Api.Platform.Persistence;

/// <summary>
/// Design-time factory for the EF tooling (<c>dotnet ef migrations add</c>): the Program
/// path requires an orchestrator-provided connection string, and <c>migrations add</c>
/// never opens a connection — the placeholder exists only to satisfy the provider.
/// </summary>
public sealed class DoseUpDbContextFactory : IDesignTimeDbContextFactory<DoseUpDbContext> {
  public DoseUpDbContext CreateDbContext(string[] args) =>
    new(
      new DbContextOptionsBuilder<DoseUpDbContext>()
        .UseNpgsql("Host=localhost;Database=doseupdb-design;Username=design;Password=design")
        .Options
    );
}