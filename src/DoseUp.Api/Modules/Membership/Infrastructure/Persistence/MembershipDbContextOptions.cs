using Microsoft.EntityFrameworkCore;

namespace DoseUp.Api.Modules.Membership.Infrastructure.Persistence;

/// <summary>
/// The one place Membership's provider options are authored — runtime composition, the
/// migration runner, and the design-time factory all call this, so every path builds
/// the identical model: snake_case identifiers (conventions § Persistence, F-88) and the
/// module-owned, hand-renamed migrations-history table (spike #93 rule 3 — EF names it
/// explicitly, so the naming convention would leave it quoted PascalCase).
/// </summary>
public static class MembershipDbContextOptions {
  public static DbContextOptionsBuilder Apply(DbContextOptionsBuilder options, string connectionString) {
    ArgumentNullException.ThrowIfNull(options);

    return options
      .UseNpgsql(connectionString, static npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MembershipDbContext.SCHEMA))
      .UseSnakeCaseNamingConvention();
  }
}
