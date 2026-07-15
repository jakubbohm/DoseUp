namespace DoseUp.Api.SharedKernel.Events;

/// <summary>
/// A synchronous-in-UoW reaction to a domain fact. Reserved for reactions that must hold
/// for every producer of the fact — explicit handler orchestration is the default
/// (conventions § Events &amp; side effects). Handlers never publish integration events.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent {
  Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}