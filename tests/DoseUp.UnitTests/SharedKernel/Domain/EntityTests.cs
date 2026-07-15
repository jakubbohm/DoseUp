using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Domain;

public sealed class EntityTests {
  [Test]
  public void Entities_of_the_same_type_with_the_same_id_are_equal_and_hash_equal() {
    TestId id = TestId.Create();
    FirstEntity left = new(id);
    FirstEntity right = new(id);

    left.Equals(right).ShouldBeTrue();
    left.Equals((object)right).ShouldBeTrue();
    left.GetHashCode().ShouldBe(right.GetHashCode());
  }

  [Test]
  public void Entities_of_the_same_type_with_different_ids_are_not_equal() {
    FirstEntity left = new(TestId.Create());
    FirstEntity right = new(TestId.Create());

    left.Equals(right).ShouldBeFalse();
  }

  [Test]
  public void Entities_of_different_types_are_never_equal_even_with_the_same_underlying_value() {
    TestId id = TestId.Create();
    FirstEntity first = new(id);
    SecondEntity second = new(id);

    first.Equals(second).ShouldBeFalse();
    second.Equals(first).ShouldBeFalse();
  }

  [Test]
  public void An_entity_is_not_equal_to_null() {
    FirstEntity entity = new(TestId.Create());

    entity.Equals(null).ShouldBeFalse();
    entity.Equals((object?)null).ShouldBeFalse();
  }
}