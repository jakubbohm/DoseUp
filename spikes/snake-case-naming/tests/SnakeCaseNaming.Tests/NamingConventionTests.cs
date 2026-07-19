using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using SnakeCaseNaming.Module;

namespace SnakeCaseNaming.Tests;

/// Asserts the model that EFCore.NamingConventions 10.0.1 produces when it runs on the
/// EF Core 11 previews. These read the finalized relational model — the same source the
/// migration generator reads — so a green run means migrations come out snake_case.
public sealed class NamingConventionTests
{
    private static readonly Regex SnakeCase = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.Compiled);

    /// The DESIGN-TIME model on purpose: that is the one `dotnet ef migrations add` reads.
    /// The runtime model is read-optimized and drops metadata such as check constraints.
    private static IModel BuildModel()
    {
        using var context = new MembershipDbContext(MembershipDbContextOptions.Build());
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Test]
    public async Task ModelBuildsAtAllOnEfCore11()
    {
        // The headline risk: the package targets EF 10 and plugs into EF's convention
        // infrastructure. If that broke on 11, this throws rather than fails.
        var model = BuildModel();

        model.GetEntityTypes().ShouldNotBeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task EveryTableNameIsSnakeCase()
    {
        foreach (var entityType in BuildModel().GetEntityTypes())
        {
            var table = entityType.GetTableName();
            table.ShouldNotBeNull();
            SnakeCase.IsMatch(table).ShouldBeTrue($"table '{table}' is not snake_case");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task EveryColumnNameIsSnakeCase()
    {
        foreach (var entityType in BuildModel().GetEntityTypes())
        {
            var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
            storeObject.ShouldNotBeNull();

            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName(storeObject.Value);
                column.ShouldNotBeNull();
                SnakeCase.IsMatch(column).ShouldBeTrue(
                    $"column '{entityType.DisplayName()}.{column}' is not snake_case");
            }
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task EveryKeyNameIsSnakeCase()
    {
        foreach (var key in BuildModel().GetEntityTypes().SelectMany(e => e.GetKeys()))
        {
            var name = key.GetName();
            name.ShouldNotBeNull();
            SnakeCase.IsMatch(name).ShouldBeTrue($"key '{name}' is not snake_case");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task EveryForeignKeyConstraintNameIsSnakeCase()
    {
        foreach (var fk in BuildModel().GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            var name = fk.GetConstraintName();
            name.ShouldNotBeNull();
            SnakeCase.IsMatch(name).ShouldBeTrue($"foreign key '{name}' is not snake_case");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task EveryIndexNameIsSnakeCase()
    {
        foreach (var index in BuildModel().GetEntityTypes().SelectMany(e => e.GetIndexes()))
        {
            var name = index.GetDatabaseName();
            name.ShouldNotBeNull();
            SnakeCase.IsMatch(name).ShouldBeTrue($"index '{name}' is not snake_case");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task OwnedTypeColumnsAreFlattenedAndRenamed()
    {
        var account = BuildModel().FindEntityType(typeof(UserAccount));
        account.ShouldNotBeNull();

        var owned = account.GetNavigations()
            .Single(n => n.Name == nameof(UserAccount.PrimaryContact))
            .TargetEntityType;

        // Owned-type columns share the principal's table and must be renamed with their prefix.
        owned.GetTableName().ShouldBe("user_accounts");

        var storeObject = StoreObjectIdentifier.Create(owned, StoreObjectType.Table)!.Value;
        var columns = owned.GetProperties().Select(p => p.GetColumnName(storeObject)).ToList();

        columns.ShouldContain("primary_contact_email_address");
        columns.ShouldContain("primary_contact_mobile_phone_number");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ConsecutiveCapitalsInAcronymsAreHandled()
    {
        var account = BuildModel().FindEntityType(typeof(UserAccount));
        account.ShouldNotBeNull();

        var storeObject = StoreObjectIdentifier.Create(account, StoreObjectType.Table)!.Value;
        var column = account.GetProperty(nameof(UserAccount.IANATimeZoneId)).GetColumnName(storeObject);

        // Documents ACTUAL behaviour for the acronym edge case, whatever it is.
        column.ShouldBe("iana_time_zone_id");
        await Task.CompletedTask;
    }

    [Test]
    public async Task QueryTranslationAlsoEmitsSnakeCase()
    {
        // The migration generator reads the design-time model; LINQ translation reads the
        // read-optimized RUNTIME model. They are built by different pipelines, so a convention
        // that lands in one but not the other would ship a schema no query could address.
        // No database needed — ToQueryString() renders the SQL EF would send.
        using var context = new MembershipDbContext(MembershipDbContextOptions.Build());

        var sql = context.MedicationProfiles
            .Where(p => !p.IsArchived && p.DailyDoseLimit > 2)
            .Select(p => new { p.ProfileDisplayName, p.UserAccountId })
            .ToQueryString();

        sql.ShouldContain("membership.medication_profiles");
        sql.ShouldContain("profile_display_name");
        sql.ShouldContain("daily_dose_limit");
        sql.ShouldContain("is_archived");
        sql.ShouldNotContain("\"MedicationProfiles\"");
        sql.ShouldNotContain("\"DailyDoseLimit\"");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ExplicitCheckConstraintNamesAndSqlPassThroughVerbatim()
    {
        var profile = BuildModel().FindEntityType(typeof(MedicationProfile));
        profile.ShouldNotBeNull();

        var check = profile.GetCheckConstraints().Single();

        // KNOWN LIMITATION, pinned here so a future package version that changes it fails loudly:
        // the package renames names it OWNS, never a name the developer set explicitly, and it
        // cannot rewrite the raw SQL of the constraint's expression. Both must be authored
        // snake_case by hand — the naive PascalCase form emits DDL Postgres rejects
        // (evidence/01-naive-authoring-fails.txt).
        check.Name.ShouldBe("ck_medication_profiles_daily_dose_limit_positive");
        check.Sql.ShouldBe("daily_dose_limit > 0");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ExplicitSchemaNamesPassThroughVerbatim()
    {
        // Same rule: HasDefaultSchema(...) is an explicit value and survives verbatim, so a
        // PascalCase schema would need quoting forever. Author module schemas lowercase.
        BuildModel().GetDefaultSchema().ShouldBe("membership");
        await Task.CompletedTask;
    }
}
