using DoseUp.Api.SharedKernel.Events;

namespace DoseUp.Api.SharedKernel.Domain;

/// <summary>
/// Base for aggregate roots: owns the domain-event collection. Mutating methods call
/// <see cref="Raise"/> at the point of state change; the SaveChanges interceptor drains
/// via <see cref="IAggregateRoot.DrainDomainEvents"/> and dispatches inside the unit of
/// work (ADR-0002 § Events).
/// </summary>
public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id), IAggregateRoot
  where TId : struct, IEquatable<TId> {
  private readonly List<IDomainEvent> _domainEvents = [];

  protected void Raise(IDomainEvent domainEvent) {
    ArgumentNullException.ThrowIfNull(domainEvent);
    _domainEvents.Add(domainEvent);
  }

  IReadOnlyList<IDomainEvent> IAggregateRoot.DrainDomainEvents() {
    if (_domainEvents.Count == 0)
      return [];

    IDomainEvent[] drained = [.. _domainEvents];
    _domainEvents.Clear();
    return drained;
  }
}