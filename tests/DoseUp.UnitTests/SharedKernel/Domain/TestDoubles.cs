using DoseUp.Api.SharedKernel.Domain;
using DoseUp.Api.SharedKernel.Events;

namespace DoseUp.UnitTests.SharedKernel.Domain;

// Hand-rolled doubles for the base-type and dispatcher suites (testing.md §6.3/§6.7):
// built through the real factories, never reflection or serialization bypass.

public readonly record struct TestId(Guid Value) : ITypedId<TestId> {
  public static TestId Create() => new(Guid.CreateVersion7());

  public static TestId From(Guid value) => new(value);
}

public sealed class FirstEntity(TestId id) : Entity<TestId>(id);

public sealed class SecondEntity(TestId id) : Entity<TestId>(id);

public sealed record FirstThingHappened : IDomainEvent;

public sealed record SecondThingHappened : IDomainEvent;

public sealed class TestAggregate(TestId id) : AggregateRoot<TestId>(id) {
  public void RaiseFirst() => Raise(new FirstThingHappened());

  public void RaiseSecond() => Raise(new SecondThingHappened());

  public void RaiseNull() => Raise(null!);
}