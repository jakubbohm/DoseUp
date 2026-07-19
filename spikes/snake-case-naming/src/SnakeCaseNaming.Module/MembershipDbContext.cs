using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SnakeCaseNaming.Module;

/// Module-style context: owns its own schema and configures its own aggregates,
/// exactly as a real DoseUp module context would.
public sealed class MembershipDbContext(DbContextOptions<MembershipDbContext> options) : DbContext(options)
{
    /// SPIKE FINDING 2: authored lowercase deliberately. The package renames only the names it
    /// generates; an explicit schema name is passed through verbatim, so "Membership" would have
    /// stayed PascalCase and required quoting in every hand-written statement.
    public const string Schema = "membership";

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<MedicationProfile> MedicationProfiles => Set<MedicationProfile>();

    public DbSet<DoseLogEntry> DoseLogEntries => Set<DoseLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<UserAccount>(account =>
        {
            account.HasKey(a => a.Id);

            // Alternate key -> AK_ constraint.
            account.HasAlternateKey(a => a.ExternalUserId);

            // Unique index -> IX_ constraint, and the set-rule DB backstop cites this name.
            account.HasIndex(a => a.DisplayName).IsUnique();

            account.OwnsOne(a => a.PrimaryContact);
        });

        modelBuilder.Entity<MedicationProfile>(profile =>
        {
            profile.HasKey(p => p.Id);

            profile.HasOne(p => p.UserAccount)
                .WithMany(a => a.MedicationProfiles)
                .HasForeignKey(p => p.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite, filtered index — the shape a real "one active profile per name" rule uses.
            profile.HasIndex(p => new { p.UserAccountId, p.ProfileDisplayName })
                .IsUnique()
                .HasFilter(null);

            // SPIKE FINDING 3: BOTH halves are hand-authored snake_case on purpose.
            // The name is explicit, so the package leaves it alone; the expression is raw SQL,
            // which the package cannot rewrite at all. Authoring this the C# way — name
            // "CK_MedicationProfile_DailyDoseLimit_Positive", expression "DailyDoseLimit" > 0 —
            // produces DDL that Postgres REJECTS: the column is daily_dose_limit.
            // See evidence/01-naive-authoring-fails.txt.
            profile.ToTable(t => t.HasCheckConstraint(
                "ck_medication_profiles_daily_dose_limit_positive",
                "daily_dose_limit > 0"));
        });

        modelBuilder.Entity<DoseLogEntry>(entry =>
        {
            entry.HasKey(e => e.Id);

            entry.HasOne(e => e.MedicationProfile)
                .WithMany(p => p.DoseLogEntries)
                .HasForeignKey(e => e.MedicationProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entry.HasIndex(e => new { e.MedicationProfileId, e.TakenAtUtc });

            entry.Property(e => e.AmountTaken).HasPrecision(9, 3);
        });
    }
}

/// Lets `dotnet ef` build the context with no host/startup project.
/// The connection string is never opened — migrations are generated, not applied.
public sealed class MembershipDbContextFactory : IDesignTimeDbContextFactory<MembershipDbContext>
{
    public MembershipDbContext CreateDbContext(string[] args) =>
        new(MembershipDbContextOptions.Build());
}

public static class MembershipDbContextOptions
{
    public static DbContextOptions<MembershipDbContext> Build() =>
        new DbContextOptionsBuilder<MembershipDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=doseup_spike;Username=spike;Password=spike",
                npgsql => npgsql
                    // SPIKE FINDING 4: EF names its own history table explicitly, so the package
                    // leaves it as "__EFMigrationsHistory" (quoted, PascalCase) while renaming its
                    // COLUMNS to snake_case — an inconsistent table. Rename it by hand.
                    .MigrationsHistoryTable("__ef_migrations_history", MembershipDbContext.Schema))
            .UseSnakeCaseNamingConvention() // <- the entire subject of this spike
            .Options;
}
