namespace SnakeCaseNaming.Module;

/// A deliberately module-shaped model. Every property name below is multi-word PascalCase
/// so that a missing rename is visible rather than accidentally already-lowercase.
public sealed class UserAccount
{
    public Guid Id { get; set; }

    /// Unique index + alternate key: both produce named constraints we care about.
    public string ExternalUserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// Acronym-adjacent casing — the classic naming-convention edge case (IANATimeZoneId).
    public string IANATimeZoneId { get; set; } = string.Empty;

    /// Owned type: its columns are flattened into this table and must be renamed too.
    public ContactDetails PrimaryContact { get; set; } = new();

    public List<MedicationProfile> MedicationProfiles { get; } = [];
}

public sealed class ContactDetails
{
    public string EmailAddress { get; set; } = string.Empty;

    public string? MobilePhoneNumber { get; set; }
}

public sealed class MedicationProfile
{
    public Guid Id { get; set; }

    /// Explicit FK — exercises foreign key constraint naming.
    public Guid UserAccountId { get; set; }

    public string ProfileDisplayName { get; set; } = string.Empty;

    public int DailyDoseLimit { get; set; }

    public bool IsArchived { get; set; }

    public UserAccount UserAccount { get; set; } = null!;

    public List<DoseLogEntry> DoseLogEntries { get; } = [];
}

public sealed class DoseLogEntry
{
    public Guid Id { get; set; }

    public Guid MedicationProfileId { get; set; }

    public DateTimeOffset TakenAtUtc { get; set; }

    public decimal AmountTaken { get; set; }

    public MedicationProfile MedicationProfile { get; set; } = null!;
}
