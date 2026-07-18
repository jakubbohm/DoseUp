using Microsoft.EntityFrameworkCore;

namespace MessagingPoc.Api.Modules.Scheduling;

// ── Aggregate (trivial — the spike proves messaging plumbing, not domain richness) ──

/// <summary>A recorded dose. Lives in the <c>scheduling</c> schema.</summary>
public sealed class DoseRecord {
  public Guid Id { get; set; }
  public Guid ProfileId { get; set; }
  public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>
/// The Scheduling module's own DbContext = its unit of work, mapped to the <c>scheduling</c>
/// Postgres schema (ADR-0002 § Persistence is module property). Its Wolverine ancillary
/// message store lives in the same schema — proven by the topology test.
/// </summary>
public sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : DbContext(options) {
  public DbSet<DoseRecord> DoseRecords => Set<DoseRecord>();

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    modelBuilder.HasDefaultSchema("scheduling");
    modelBuilder.Entity<DoseRecord>(e => {
      e.ToTable("dose_records");
      e.HasKey(x => x.Id);
    });
  }
}

// ── Slice: record a dose ──

/// <summary>Command handled locally inside the Scheduling unit of work.</summary>
public sealed record RecordDose(Guid DoseRecordId, Guid ProfileId);

/// <summary>
/// Wolverine handler. Because it takes the module's <see cref="SchedulingDbContext"/> and the EF
/// transactional middleware is active, Wolverine opens a transaction on that context, persists the
/// aggregate, and writes the outbox envelope for the cascaded <see cref="Contracts.DoseRecorded"/>
/// into the <c>scheduling</c> ancillary store — all in the SAME transaction (spike #50's precondition).
/// After commit the envelope is relayed to Azure Service Bus.
/// </summary>
public static class RecordDoseHandler {
  public static Contracts.DoseRecorded Handle(RecordDose command, SchedulingDbContext db) {
    db.DoseRecords.Add(new DoseRecord {
      Id = command.DoseRecordId,
      ProfileId = command.ProfileId,
      RecordedAt = DateTimeOffset.UtcNow,
    });

    // Returned message is cascaded: published via the transactional outbox, then out to ASB.
    return new Contracts.DoseRecorded(command.DoseRecordId, command.ProfileId);
  }
}

// ── Atomicity probe: proves the outbox envelope joins the aggregate's transaction ──

/// <summary>Adds the aggregate + would cascade the event, then throws inside the unit of work.</summary>
public sealed record RecordDoseThenFail(Guid DoseRecordId, Guid ProfileId);

public static class RecordDoseThenFailHandler {
  public static Contracts.DoseRecorded Handle(RecordDoseThenFail command, SchedulingDbContext db) {
    db.DoseRecords.Add(new DoseRecord {
      Id = command.DoseRecordId,
      ProfileId = command.ProfileId,
      RecordedAt = DateTimeOffset.UtcNow,
    });

    // The transactional middleware rolls back on this throw: neither the aggregate row NOR the
    // outbox envelope is committed, so no DoseRecorded is ever relayed — both share one fate.
    throw new InvalidOperationException("spike atomicity probe — deliberate failure after enqueue");
  }
}
