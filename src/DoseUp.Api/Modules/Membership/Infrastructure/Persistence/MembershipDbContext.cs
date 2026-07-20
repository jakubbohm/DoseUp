using Ardalis.SmartEnum.EFCore;
using DoseUp.Api.Modules.Membership.Domain;
using DoseUp.Api.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DoseUp.Api.Modules.Membership.Infrastructure.Persistence;

/// <summary>
/// Membership's persistence — module property (ADR-0002 § Persistence is module
/// property): this context is the module's unit of work and data-access API, owns the
/// <c>membership</c> schema and its own migrations history, and maps only Membership
/// Domain types (arch rules 17–19).
/// </summary>
public sealed class MembershipDbContext(DbContextOptions<MembershipDbContext> options) : DbContext(options) {
  /// <summary>
  /// Authored lowercase on purpose — spike #93 rule 1: the naming convention rewrites
  /// only generated names; an explicit schema name passes through verbatim and would
  /// quote into every hand-written statement.
  /// </summary>
  public const string SCHEMA = "membership";

  public DbSet<Account> Accounts => Set<Account>();

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    modelBuilder.HasDefaultSchema(SCHEMA);
    modelBuilder.ApplyConfiguration(new AccountConfiguration());
  }

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) {
    ArgumentNullException.ThrowIfNull(configurationBuilder);

    // Module-scoped on purpose: only Membership's own typed ids register here — anchored
    // on AccountId so a namespace move can't silently widen or miss the scope.
    TypedIdModelConventions.ApplyTypedIdConversions(configurationBuilder, typeof(AccountId).Assembly, typeof(AccountId).Namespace);

    // Explicit per-type SmartEnum registration — the package's reflection sweep is
    // deliberately unused (c002 design D5; the ~4-line hand-rolled converter is the
    // recorded fallback if the dormant package ever breaks on an EF preview).
    configurationBuilder.Properties<AccountStatus>().HaveConversion<SmartEnumConverter<AccountStatus, int>>();
  }
}
