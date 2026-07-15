using DoseUp.Api.SharedKernel.Events;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Events;

public sealed class IntegrationEventPublisherTests {
  [Test]
  public async Task A_publication_reaches_the_registered_implementation_unchanged() {
    RecordingPublisher recording = new();
    IIntegrationEventPublisher publisher = recording;
    SomethingHappened contractEvent = new(Guid.CreateVersion7());

    await publisher.PublishAsync(contractEvent, CancellationToken.None);

    recording.Published.ShouldHaveSingleItem().ShouldBeSameAs(contractEvent);
  }

  private sealed record SomethingHappened(Guid Id);

  private sealed class RecordingPublisher : IIntegrationEventPublisher {
    public List<object> Published { get; } = [];

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
      where TEvent : class {
      Published.Add(integrationEvent);
      return Task.CompletedTask;
    }
  }
}