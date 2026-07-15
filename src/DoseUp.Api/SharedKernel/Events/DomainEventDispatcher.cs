using System.Reflection;
using DoseUp.Api.SharedKernel.Domain;

namespace DoseUp.Api.SharedKernel.Events;

/// <summary>
/// The hand-rolled domain-event dispatcher (ADR-0002 § Unit of work): drains the given
/// aggregates and invokes every registered <see cref="IDomainEventHandler{TEvent}"/>,
/// awaited sequentially inside the unit of work. Events raised by handlers during
/// dispatch are drained on the next pass (loop until quiescent); a cascade deeper than
/// <see cref="MAX_DEPTH"/> is a bug and throws.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider services)
{
  private const int MAX_DEPTH = 10;

  public async Task DispatchAsync(
    IReadOnlyCollection<IAggregateRoot> aggregates,
    CancellationToken cancellationToken
  )
  {
    ArgumentNullException.ThrowIfNull(aggregates);

    for (int depth = 0; ; depth++)
    {
      IDomainEvent[] events = [.. aggregates.SelectMany(static a => a.DrainDomainEvents())];
      if (events.Length == 0)
      {
        return;
      }

      if (depth >= MAX_DEPTH)
      {
        throw new InvalidOperationException(
          $"Domain-event cascade exceeded {MAX_DEPTH} dispatch passes — handlers keep raising new events; this is a bug."
        );
      }

      foreach (IDomainEvent domainEvent in events)
      {
        Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        MethodInfo handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<>.HandleAsync))!;
        foreach (object? handler in services.GetServices(handlerType))
        {
          await (
            (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!
          ).ConfigureAwait(false);
        }
      }
    }
  }
}
