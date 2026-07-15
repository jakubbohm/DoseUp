using DoseUp.Api.SharedKernel.Events;

namespace DoseUp.Api.SharedKernel.Domain;

/// <summary>
/// Non-generic marker for aggregate roots — what ArchUnitNET rules and generic constraints
/// key on (e.g. only aggregate roots are exposed as <c>DbSet</c>), and the drain surface
/// the SaveChanges interceptor consumes.
/// </summary>
public interface IAggregateRoot {
  /// <summary>
  /// Yields every raised domain event exactly once, in raise order; a second drain with no
  /// raises in between yields nothing.
  /// </summary>
  IReadOnlyList<IDomainEvent> DrainDomainEvents();
}