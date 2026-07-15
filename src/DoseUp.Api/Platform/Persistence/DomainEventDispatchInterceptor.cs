using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Events;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DoseUp.Api.Platform.Persistence;

/// <summary>
/// Drains every tracked aggregate's domain events through the dispatcher <b>before</b>
/// the save, inside the same unit of work (ADR-0002 § Unit of work) — handler-made
/// changes join the same transaction.
/// </summary>
public sealed class DomainEventDispatchInterceptor(DomainEventDispatcher dispatcher)
  : SaveChangesInterceptor {
  public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(eventData);

    if (eventData.Context is { } context) {
      IAggregateRoot[] aggregates = [.. context.ChangeTracker.Entries().Select(static entry => entry.Entity).OfType<IAggregateRoot>()];
      await dispatcher.DispatchAsync(aggregates, cancellationToken).ConfigureAwait(false);
    }

    return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
  }

  // A silent sync save would skip domain-event dispatch — fail loudly instead (bug class 8).
  public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
    throw new NotSupportedException("DoseUp saves through SaveChangesAsync — the domain-event dispatch interceptor is async-only.");
}