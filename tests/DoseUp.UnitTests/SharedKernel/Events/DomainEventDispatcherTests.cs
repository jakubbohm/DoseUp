using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Events;
using DoseUp.UnitTests.SharedKernel.Domain;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Events;

public sealed class DomainEventDispatcherTests {
  [Test]
  public async Task Every_registered_handler_of_an_events_type_is_invoked() {
    RecordingHandler<FirstThingHappened> firstHandler = new();
    RecordingHandler<FirstThingHappened> secondHandler = new();
    ServiceCollection services = new();
    services.AddSingleton<IDomainEventHandler<FirstThingHappened>>(firstHandler);
    services.AddSingleton<IDomainEventHandler<FirstThingHappened>>(secondHandler);
    DomainEventDispatcher dispatcher = new(services.BuildServiceProvider());
    TestAggregate aggregate = new(TestId.Create());
    aggregate.RaiseFirst();

    await dispatcher.DispatchAsync([aggregate], CancellationToken.None);

    firstHandler.Seen.ShouldHaveSingleItem();
    secondHandler.Seen.ShouldHaveSingleItem();
  }

  [Test]
  public async Task Events_raised_by_handlers_are_dispatched_before_dispatch_completes() {
    TestAggregate aggregate = new(TestId.Create());
    RecordingHandler<SecondThingHappened> followUpHandler = new();
    ServiceCollection services = new();
    services.AddSingleton<IDomainEventHandler<FirstThingHappened>>(new ChainReactionHandler(aggregate));
    services.AddSingleton<IDomainEventHandler<SecondThingHappened>>(followUpHandler);
    DomainEventDispatcher dispatcher = new(services.BuildServiceProvider());
    aggregate.RaiseFirst();

    await dispatcher.DispatchAsync([aggregate], CancellationToken.None);

    followUpHandler.Seen.ShouldHaveSingleItem();
  }

  [Test]
  public async Task A_runaway_cascade_hits_the_depth_guard_and_throws() {
    TestAggregate aggregate = new(TestId.Create());
    ServiceCollection services = new();
    services.AddSingleton<IDomainEventHandler<FirstThingHappened>>(new RunawayHandler(aggregate));
    DomainEventDispatcher dispatcher = new(services.BuildServiceProvider());
    aggregate.RaiseFirst();

    await Should.ThrowAsync<InvalidOperationException>(() => dispatcher.DispatchAsync([aggregate], CancellationToken.None));
  }

  [Test]
  public async Task An_event_with_no_registered_handlers_dispatches_to_nobody_without_error() {
    ServiceCollection services = new();
    DomainEventDispatcher dispatcher = new(services.BuildServiceProvider());
    TestAggregate aggregate = new(TestId.Create());
    aggregate.RaiseFirst();

    await dispatcher.DispatchAsync([aggregate], CancellationToken.None);

    ((IAggregateRoot)aggregate).DrainDomainEvents().ShouldBeEmpty();
  }

  private sealed class RecordingHandler<TEvent> : IDomainEventHandler<TEvent> where TEvent : IDomainEvent {
    public List<TEvent> Seen { get; } = [];

    public Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken) {
      Seen.Add(domainEvent);
      return Task.CompletedTask;
    }
  }

  private sealed class ChainReactionHandler(TestAggregate aggregate) : IDomainEventHandler<FirstThingHappened> {
    public Task HandleAsync(FirstThingHappened domainEvent, CancellationToken cancellationToken) {
      aggregate.RaiseSecond();
      return Task.CompletedTask;
    }
  }

  private sealed class RunawayHandler(TestAggregate aggregate) : IDomainEventHandler<FirstThingHappened> {
    public Task HandleAsync(FirstThingHappened domainEvent, CancellationToken cancellationToken) {
      aggregate.RaiseFirst();
      return Task.CompletedTask;
    }
  }
}