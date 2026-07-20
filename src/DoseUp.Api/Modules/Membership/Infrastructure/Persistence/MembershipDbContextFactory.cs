using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DoseUp.Api.Modules.Membership.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for the EF tooling (<c>dotnet ef migrations add</c>): the runtime
/// path requires an orchestrator-provided connection string, and <c>migrations add</c>
/// never opens a connection — the placeholder exists only to satisfy the provider.
/// Options come from <see cref="MembershipDbContextOptions"/>, so generated migrations
/// see exactly the runtime model.
/// </summary>
public sealed class MembershipDbContextFactory : IDesignTimeDbContextFactory<MembershipDbContext> {
  public MembershipDbContext CreateDbContext(string[] args) {
    DbContextOptionsBuilder<MembershipDbContext> options = new();
    MembershipDbContextOptions.Apply(options, "Host=localhost;Database=doseupdb-design;Username=design;Password=design");
    return new(options.Options);
  }
}
