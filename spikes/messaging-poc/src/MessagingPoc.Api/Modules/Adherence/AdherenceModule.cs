using MessagingPoc.Api.Modules.Scheduling.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MessagingPoc.Api.Modules.Adherence;

// ── Read-model row owned by the Adherence module, in the `adherence` schema ──

/// <summary>What Adherence records when it observes a <see cref="DoseRecorded"/> fact.</summary>
public sealed class AdherenceEntry {
  public Guid Id { get; set; }
  public Guid DoseRecordId { get; set; }
  public Guid ProfileId { get; set; }
  public DateTimeOffset ObservedAt { get; set; }
}

/// <summary>
/// Adherence's own DbContext = its unit of work, mapped to the <c>adherence</c> schema. Its Wolverine
/// ancillary store (outbox + <b>inbox/idempotency</b>) lives in the same schema — the second half of
/// spike #50's per-module precondition.
/// </summary>
public sealed class AdherenceDbContext(DbContextOptions<AdherenceDbContext> options) : DbContext(options) {
  public DbSet<AdherenceEntry> AdherenceEntries => Set<AdherenceEntry>();

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    modelBuilder.HasDefaultSchema("adherence");
    modelBuilder.Entity<AdherenceEntry>(e => {
      e.ToTable("adherence_entries");
      e.HasKey(x => x.Id);
      // Business-key uniqueness — the durable backstop behind the idempotent consumer below.
      e.HasIndex(x => x.DoseRecordId).IsUnique();
    });
  }
}

// ── Cross-module consumer: reacts to Scheduling's fact, arriving via Azure Service Bus ──

/// <summary>
/// Wolverine consumer for <see cref="DoseRecorded"/>. Runs inside the Adherence unit of work
/// (its DbContext + inbox in the <c>adherence</c> schema). Explicitly idempotent by the business key
/// (re-query, no-op on repeat) — the project's "consumers are idempotent" rule — on top of Wolverine's
/// envelope-level inbox dedup. Delivering the same dose twice yields exactly one entry.
/// </summary>
public static class DoseRecordedHandler {
  public static async Task Handle(DoseRecorded message, AdherenceDbContext db, CancellationToken cancellationToken) {
    bool alreadyObserved = await db.AdherenceEntries
      .AnyAsync(e => e.DoseRecordId == message.DoseRecordId, cancellationToken)
      .ConfigureAwait(false);

    if (alreadyObserved)
      return;

    db.AdherenceEntries.Add(new AdherenceEntry {
      Id = Guid.NewGuid(),
      DoseRecordId = message.DoseRecordId,
      ProfileId = message.ProfileId,
      ObservedAt = DateTimeOffset.UtcNow,
    });
    // The EF transactional middleware calls SaveChangesAsync after the handler and commits.
  }
}
