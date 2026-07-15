using Shouldly;

namespace DoseUp.UnitTests.SharedKernel.Domain;

public sealed class TypedIdTests {
  [Test]
  public void A_minted_id_wraps_a_version_7_guid() {
    TestId id = TestId.Create();

    id.Value.Version.ShouldBe(7);
  }

  [Test]
  public void Minted_ids_are_unique() => TestId.Create().ShouldNotBe(TestId.Create());

  [Test]
  public void From_wraps_the_exact_value() {
    Guid value = Guid.CreateVersion7();

    TestId.From(value).Value.ShouldBe(value);
  }
}