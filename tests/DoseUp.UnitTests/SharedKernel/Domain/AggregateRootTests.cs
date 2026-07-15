using DoseUp.Api.SharedKernel.Domain;
using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Domain;

public sealed class AggregateRootTests {
  [Test]
  public void Raised_events_drain_exactly_once_in_raise_order() {
    TestAggregate aggregate = new(TestId.Create());
    aggregate.RaiseFirst();
    aggregate.RaiseSecond();

    IReadOnlyList<object> drained = ((IAggregateRoot)aggregate).DrainDomainEvents();

    drained.Count.ShouldBe(2);
    drained[0].ShouldBeOfType<FirstThingHappened>();
    drained[1].ShouldBeOfType<SecondThingHappened>();
  }

  [Test]
  public void A_second_drain_with_no_new_raises_yields_nothing() {
    TestAggregate aggregate = new(TestId.Create());
    aggregate.RaiseFirst();
    IAggregateRoot root = aggregate;
    _ = root.DrainDomainEvents();

    root.DrainDomainEvents().ShouldBeEmpty();
  }

  [Test]
  public void Raising_a_null_event_is_a_bug_and_throws() {
    TestAggregate aggregate = new(TestId.Create());

    Should.Throw<ArgumentNullException>(aggregate.RaiseNull);
  }
}