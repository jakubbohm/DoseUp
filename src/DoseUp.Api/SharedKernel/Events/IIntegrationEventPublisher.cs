namespace DoseUp.Api.SharedKernel.Events;

/// <summary>
/// Transport-free port for publishing integration events — thin, id-only
/// <c>&lt;Module&gt;.Contracts</c> payloads (NFR-5 extends to messages). Only per-module
/// translators and Platform reference it (arch-tested, catalog rule 9); Platform's
/// implementation over the Wolverine outbox arrives with the first integration event (M0+).
/// </summary>
public interface IIntegrationEventPublisher {
  Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
    where TEvent : class;
}